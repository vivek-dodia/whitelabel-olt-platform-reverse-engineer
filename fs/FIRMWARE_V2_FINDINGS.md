# FS OLT Stick — Firmware V2 Findings

This documents the second generation of the FS GPON-SFP-OLT-MAC-I hardware and software.
The v1 findings (UDP protocol, C# decompile) are in [README.md](README.md).

## Background

The v1 OLT sticks had a firmware/hardware incompatibility that broke ONU optical parameter
reporting (`get_onu_optics`) and some management commands. A support case was opened with FS,
who sent two replacement sticks with newer firmware and a new Windows app installer
(`FS_GPON_OLT_STICK_V2_Setup-new.msi`, `FS PON Manager V1.1.0`).

The new firmware is not just a patch — it is a fundamentally different management architecture.

---

## What Changed: UDP → HTTP

| | v1 (old firmware) | v2 (new firmware) |
|-|-------------------|-------------------|
| Management transport | UDP 64219/64218 | HTTP on port 128 |
| Discovery | ARP broadcast with `"OLT"` marker | Same (ARP-based, auto-discovered by app) |
| Interface | Windows app speaks binary UDP directly | Windows app + web browser at `http://OLT_IP:128` |
| Terminal commands | Sent via proprietary UDP frames (raw Ethernet for optics) | Entered in web UI Terminal tab, REST API behind it |
| Authentication | Fixed key UDP handshake | HTTP login (admin/operator/viewer roles) |
| OLT addressing | Static IP (default 100.64.2.200) | Static or DHCP (set via `set_ip_addr_mode`) |
| Default IP | Varies | 192.168.1.3 (factory) |
| Default CAPWAP IP | N/A | 192.168.1.100 |

---

## New MSI Analysis (`FS_GPON_OLT_STICK_V2_Setup-new.msi`)

Extracted the installer (OLE/CAB structure) and identified all components:

| Component | Details |
|-----------|---------|
| Main app | `APP_OLT_Stick_V2.dll` + `APP_OLT_Stick_Eth.exe` — .NET 8 WinForms, same stack as v1 |
| Runtime | `net8.0`, `Microsoft.WindowsDesktop.App 8.0.0` |
| New dependencies | `SharpPcap 6.3.0`, `Dapper 2.1.66`, `EntityFramework 6.4.4` |
| SQLite library | ELF x86-64 shared object, **not stripped, debug symbols included** |
| SQLite database | Pre-populated with 478 whitelist entries (sample/demo data) |
| Config templates | Service profile template (TXT) and whitelist template (TXT) |
| Version string | `FS PON Manager V1.1.0` (up from V1.0.0) |

SharpPcap is still a dependency — raw packet capture is still used for some functionality
(likely ARP discovery and possibly the Ethernet-frame-based optics path as fallback).

### SQLite Database Schema (v2)

New columns added to `OLT_Performance` vs v1:

```sql
OLT_Performance:
  olt_pn      TEXT   -- OLT part number (new)
  olt_swport  TEXT   -- Switch port identifier (new)

ONU_Performance:
  OnuRssi     TEXT   -- RSSI field (new)
```

### Service Profile Config Format

Shipped config template clarifies the service profile wire format:
```
max_BD    uint16  Max bandwidth, unit = 20 Kbit/s  (5000 = 100Mbps)
fix_BD    uint16  Fixed bandwidth, unit = 20 Kbit/s
ass_BD    uint16  Assured bandwidth, unit = 20 Kbit/s
type      uint8   Service type (1–5)
priority  uint8   Priority 0–7, 0 = top
weight    uint8   Weight 1–255, 255 = top
valid     uint8   1 = enable, 0 = disable
vlan1_id  uint16  First VLAN TCI (0 = no VLAN, 32768 = PPPoE passthrough)
vlan2–5         Additional VLANs
```

---

## Web Interface (port 128)

### Access

```
http://OLT_IP:128
```

Default credentials (factory):

| Role | Username | Password |
|------|----------|----------|
| Administrator | admin | abcd1@ |
| Operator | operator | op1234 |
| Viewer | viewer | view123 |

### Tabs

| Tab | Function |
|-----|---------|
| Device | OLT info — IP, MAC, serial, firmware version, port status |
| ONU List | Live ONU list with state, uptime, and optical performance |
| Terminal | Interactive terminal — run any command (see list below) |
| Firmware | OLT firmware upgrade |
| Settings | Network config, VLAN, DHCP mode |
| ONU Whitelist | Whitelist management GUI |
| System Log | Event log |
| History | Historical performance data |

### ONU List Performance Format

Performance column is a semicolon/label delimited string:
```
3.20 V;68.4 c;17.7 mA;tp=1.5 dBm;rp=-6.2 dBm
```
Fields: `Voltage (V)` ; `Temperature (°C)` ; `Bias current (mA)` ; `tx_power (dBm)` ; `rx_power (dBm)`

Sentinel value for "no reading" = **-99.0 dBm** (ONU doesn't expose DDM optics).

---

## Terminal Commands (v2 full list)

All commands use function-call syntax: `command_name(args)`.
Run in the web UI Terminal tab, or via whatever HTTP endpoint the web UI POSTs to.

### Diagnostics
| Command | Description | Example |
|---------|-------------|---------|
| `show_onu` | List ONU status | `show_onu()` |
| `get_onu_optics("SN")` | ONU optical parameters (DDM) | `get_onu_optics("TPLG31A11C1A")` |
| `onu_offline_cause("SN")` | Why did an ONU go offline | `onu_offline_cause("TPLG31A11C1A")` |
| `get_port_info` | LLDP port information | `get_port_info()` |
| `show_dhcp_client` | Current OLT IP details | `show_dhcp_client()` |
| `show_ip` | Query IP setting | `show_ip()` |
| `show_mac` | Query OLT MAC | `show_mac()` |
| `show_gateway` | Query gateway | `show_gateway()` |
| `show_subnet_mask` | Query mask | `show_subnet_mask()` |
| `show_capwapip` | Query CAPWAP IP | `show_capwapip()` |
| `show_macip` | Query static IP | `show_macip()` |

### Whitelist Management
| Command | Description | Example |
|---------|-------------|---------|
| `find_onu("SN")` | Check if ONU is whitelisted | `find_onu("WSTO12345678")` |
| `get_whitelst_number` | Count whitelist entries | `get_whitelst_number()` |
| `add_one_onu("SN", type)` | Add ONU with service profile | `add_one_onu("WSTO12345678", 1)` |
| `del_one_onu("SN")` | Delete ONU from whitelist | `del_one_onu("WSTO12345678")` |
| `remove_one_onu("SN")` | Remove ONU | `remove_one_onu("WSTO12345678")` |
| `clear_white_list` | Clear all whitelist entries | `clear_white_list()` |
| `set_white_list_type(mode)` | Set whitelist (0x57) / graylist (0x47) | `set_white_list_type(0x57)` |
| `get_white_list_type` | Query current mode | `get_white_list_type()` |
| `PloamUserSetSN` | Enable/disable individual ONU | — |

### Service Profiles
| Command | Description | Example |
|---------|-------------|---------|
| `get_service_type(N)` | Query service template 1–5 | `get_service_type(1)` |
| `set_service_type` | Set service template | see full docs |
| `del_service_type(N)` | Delete template 2–5 | `del_service_type(2)` |

### OLT Configuration
| Command | Description | Example |
|---------|-------------|---------|
| `set_ip_addr_mode(N)` | 0=static, 1=DHCP | `set_ip_addr_mode(1)` |
| `get_ip_addr_mode` | Query IP mode | `get_ip_addr_mode()` |
| `set_ipmaskgate("ip","mask","gw")` | Set static IP | `set_ipmaskgate("192.168.1.3","255.255.255.0","192.168.1.1")` |
| `set_capwap_ip("ip")` | Set CAPWAP IP | `set_capwap_ip("192.168.1.100")` |
| `set_dhcp_vlan(N)` | Management channel VLAN | `set_dhcp_vlan(3000)` |
| `get_dhcp_vlan` | Query VLAN | `get_dhcp_vlan()` |
| `set_host_name("name")` | Set OLT hostname (in DHCP) | `set_host_name("site1-olt1")` |
| `get_host_name` | Query hostname | `get_host_name()` |
| `get_olt_pn` | OLT part number | `get_olt_pn()` |
| `get_stick_pn` | OLT PN alias | `get_stick_pn()` |
| `set_stick_pn("name")` | Set PN alias | `set_stick_pn("building_one_olt")` |
| `set_mac_addr("mac")` | Change OLT MAC | `set_mac_addr("4C4F19241112")` |
| `set_nego(N)` | Port auto-negotiation 0/1 | `set_nego(1)` |
| `get_nego` | Query negotiation | `get_nego()` |
| `set_GEor2GE(N)` | Set uplink to GE(1) / 2GE(0) | `set_GEor2GE(1)` |
| `get_GEor2GE` | Query port speed | `get_GEor2GE()` |
| `set_arp_proxy(N)` | ARP proxy 0/1 | `set_arp_proxy(1)` |
| `get_arp_proxy` | Query ARP proxy | `get_arp_proxy()` |
| `reset_system` | Reboot OLT | `reset_system()` |
| `reboot_onu("SN")` | Reboot individual ONU | `reboot_onu("TPLG31A11C1A")` |

---

## Live Deployment (as of 2026-06-08)

Router: `000001.001.R01`, ZeroTier IP `100.64.2.1`
Network: `100.64.2.0/24` (DHCP bridge, both OLTs and management PC on same segment)

| OLT | SFP Port | IP | ONUs |
|-----|----------|----|------|
| OLT-A | SFP7 | 100.64.2.148 | 0 |
| OLT-B | SFP10 | 100.64.2.149 | 3 (via 1:8 splitter) |

### ONUs on OLT-B (100.64.2.149)

| ID | SN | State | Voltage | Temp | Bias | TX | RX | Notes |
|----|-----|-------|---------|------|------|----|----|-------|
| 0 | TPLG31A11C1A | ON_LINE | 3.20 V | 68.4 °C | 17.7 mA | +1.5 dBm | -6.2 dBm | TP-Link, good optics |
| 1 | TPLG8E22D418 | ON_LINE | 3.28 V | 39.6 °C | 17.0 mA | +2.5 dBm | -4.5 dBm | TP-Link, good optics |
| 2 | UBNTF915FB50 | ON_LINE | 0.00 V | 0.0 °C | 0.0 mA | -99.0 dBm | -99.0 dBm | Ubiquiti — no DDM support |

**Note on UBNTF915FB50:** The Ubiquiti ONU is online and passing traffic but reports -99.0 dBm
for all optical parameters. This is the "no reading" sentinel value — Ubiquiti ONUs do not
expose DDM (Digital Diagnostic Monitoring) optics data. This is not an error or firmware issue.

### First-Time Provisioning Procedure

When a new OLT arrives from factory (default IP 192.168.1.3):

1. Add `192.168.1.100/24` to the management bridge (the CAPWAP default)
2. `ping 192.168.1.3` to verify connectivity
3. Browse to `http://192.168.1.3:128`, login `admin` / `abcd1@`
4. Go to Terminal tab, run: `set_ip_addr_mode(1)` — OLT immediately acquires DHCP lease
5. Find the new IP from DHCP server leases
6. OLT is now reachable at `http://<dhcp-ip>:128`
7. (Optional) `set_host_name("site-olt-N")` to identify it in DHCP leases

**Limitation:** All OLTs ship with the same default IP `192.168.1.3`, so only one can be
provisioned at a time on a flat network. Provision them one at a time, switching to DHCP mode
before plugging in the next one.

---

## HTTP API — Status: Unknown (next step)

The web UI at port 128 makes REST calls that drive all management functions. These have not
yet been captured or documented. The grid-agent currently uses the v1 UDP protocol and will
not work with these new firmware OLTs.

**Next step:** Capture browser traffic against `http://100.64.2.149:128/` with the HAR reverse
engineering tool to map all API endpoints, then update the grid-agent with an HTTP client
for v2 firmware OLTs.

Endpoints expected (inferred from web UI tabs and terminal commands):
- `GET /api/onus` or similar — ONU list with performance
- `POST /api/terminal` or similar — terminal command execution
- `GET /api/device` — OLT device info
- `GET /api/whitelist` — whitelist read
- `POST /api/whitelist` — whitelist write
- `GET /api/system-log` — event log

---

## Files Added for v2

```
fs/
├── FS_GPON_OLT_STICK_V2_Setup-new.msi     # New installer (FS PON Manager V1.1.0)
├── official-docs/
│   └── GPON OLT STICK WEB User Guide-new.docx  # New web UI user guide
└── FIRMWARE_V2_FINDINGS.md                # This file
```
