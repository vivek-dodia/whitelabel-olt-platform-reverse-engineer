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

## What Changed

> Initial assumption was "UDP → HTTP". After decompiling the new app, the real picture is:
> the binary protocol **stayed** and gained per-frame authentication, AND the firmware
> **added** a standalone web UI. See "Two Independent Management Paths" below.

| | v1 (old firmware) | v2 (new firmware) |
|-|-------------------|-------------------|
| Binary protocol | UDP 64219/64218, no per-frame auth | Same family + 4-byte keyed hash per frame; dynamic reply port |
| Web interface | None | HTTP on port 128 (served by firmware, independent of app) |
| Discovery | ARP broadcast with `"OLT"` marker | Same, plus `OLT_login_send` (cmd 128) text announce |
| Windows app | .NET 8 WinForms, binary UDP | .NET 8 WinForms, binary UDP + hash auth (`FS PON Manager V1.1.0`) |
| Optics command | Raw Ethernet, often failed (the bug) | Same channel (cmd 65/0x41) but now hash-authenticated → works |
| Vendor key | None | `fsoptics` (from `appmanagement.ini`) |
| Status reports | Binary only | Binary + ASCII text reports (cmd 128–132) |
| Authentication | Fixed key UDP handshake (cmd 66) | Per-frame keyed hash + web login (admin/operator/viewer) |
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

## Two Independent Management Paths

A key structural finding from decompiling the new app: there are **two separate management
interfaces**, not one.

1. **The Windows app (`FS PON Manager V1.1.0`)** still speaks the binary UDP/raw-Ethernet
   protocol — same family as v1, but with a new per-frame authentication layer (below). This is
   how the app auto-discovers and manages OLTs on the LAN.
2. **The HTTP web server on port 128** is baked into the OLT firmware itself and is fully
   independent of the Windows app. The browser UI you log into is served by the OLT.

So "v1 → v2" is not "UDP → HTTP". It is: the binary protocol gained authentication, AND the
firmware additionally exposes a standalone web UI. Both work simultaneously.

---

## V2 Binary Protocol (decompiled from `APP_OLT_Stick_V2_new.cs`)

The new Windows app is namespace `APP_OLT_Stick_Eth`. Decompiled clean (no obfuscation, 11,801
lines) — see [decompiled/APP_OLT_Stick_V2_new.cs](decompiled/APP_OLT_Stick_V2_new.cs). It still
uses SharpPcap for raw Ethernet + UDP. The wire protocol changed in three significant ways.

### Change 1 — Per-frame keyed hash authentication (this was the bug)

Every command frame now carries a **4-byte keyed hash header** prepended to the payload.
This is almost certainly why v1 commands (including `get_onu_optics`) failed against the new
firmware — the new firmware rejects frames without a valid hash.

```
V2 frame payload = [4-byte hash] + [cmd_code(1)] + [seq(2)] + [data...]

hash = Hashcaculation(OLT_SN, vendor_key, payload[0:4])
```

The hash (`SN_Change.Hashcaculation`, line ~5430) is a custom DJB2-variant over three inputs in
order — the OLT serial number, the vendor key, and the first 4 bytes of the payload:

```python
def hashcaculation(sn: bytes, key: bytes, first4: bytes) -> bytes:
    num = 5381
    M = 0xFFFFFFFF
    def mix(buf):
        nonlocal num
        for i, b in enumerate(buf):
            num = ((num << 5) + num + b) & M       # num*33 + b
            num ^= num >> 13
            num = (num + (num << 7)) & M
            num ^= (b << (i % 4 * 8)) & M
        num ^= num >> 16
        num = (num + (num << 3)) & M
        num ^= num >> 4
    mix(sn); mix(key); mix(first4)
    return bytes([(num >> 24) & 0xFF, (num >> 16) & 0xFF,
                  (num >> 8) & 0xFF, num & 0xFF])     # big-endian
```

### Change 2 — Vendor key is the literal string `fsoptics`

The app loads its vendor keys from `appmanagement.ini`, shipped inside the MSI (saved as
[v2-app-files/appmanagement.ini](v2-app-files/appmanagement.ini)):

```
fsoptics                  # line 0 = Vendor_Read_Key
fsoptics                  # line 1 = Vendor_Write_Key
FS PON Manager V1.1.0     # line 2 = APP_version
```

Both read and write keys are `fsoptics`. This string is fed to `Hashcaculation` as the `key`
argument for every frame.

### Change 3 — Dynamic UDP port + 16-byte OLT SN embedded per packet

v1 used fixed UDP ports 64219/64218. v2 negotiates the reply port from the OLT's announced
source port, and embeds the 16-byte OLT serial in every received frame. Received-frame layout
(`Content[]` offsets, line ~1160):

```
[0:6]    Sender MAC
[20:24]  Sender IP (from IP header)
[28:30]  Source UDP port (uint16 BE) — used as DEST port for replies (dynamic, not fixed)
[32:34]  UDP length (uint16 BE)
[36:52]  OLT serial number (16 bytes ASCII)
[52]     Command code
[53:55]  Sequence / upd_ID (uint16 BE)
[52:]    Payload
```

The default `dest_UDP_port` is still 64218 until the OLT announces otherwise.

### New command codes (text-report family)

The `Command_Code` enum gained a 0x80+ range, and command 65 (`0x41`, ASCII `A`) is now used
for ONU optics responses. These carry **ASCII text payloads** (starting at offset 4), which the
app keyword-matches — a major shift from v1's binary-only responses.

| Code | Name | Notes |
|------|------|-------|
| 65 (0x41) | (optics / status text) | ONU optics response channel, ASCII payload |
| 128 (0x80) | OLT_login_send | OLT login/announce — carries `oltiSN` for discovery |
| 129 (0x81) | OLT_Status_report | OLT-level optics/status (see `ParseOltParameters`) |
| 130 (0x82) | OLT_Alarm_report | OLT alarm text |
| 131 (0x83) | ONU_Status_report | ONU status; enqueued for optics parsing |
| 132 (0x84) | ONU_Alarm_report | ONU alarm text |

The old reset commands (69 `OLT_Reset_Master`, 70 `OLT_Reset_Slave`, 71 `OLT_Softreset`) are
**gone** from the v2 enum.

### Text-report keyword dispatch (line ~1243)

The async text reports are matched against substrings to drive ONU/OLT state:

| Keyword in payload | Meaning |
|--------------------|---------|
| `on_line` | ONU ON_LINE |
| `off_line` | ONU OFF_LINE |
| `dying_gasp` | ONU DYING_GASP |
| `reset_ok` | ONU CONFIGURE (post-reset) |
| `upload_ok` | ONU UP_LOAD |
| `LOSLOFalarm` | ONU LOFi (loss of signal/frame) |
| `Rogue_ONU_Alarm` | Rogue ONU detected |
| `oltiSN` | OLT info/login report (discovery) |
| `Neighbor` | LLDP neighbor / port info (`get_port_info`) |
| `Get PN:` | OLT part number (`get_olt_pn`) |
| `access result` | Password/access result |

### ONU optics parsing (`ParseOnuParameters`, line ~3160)

Optics response is an ASCII string split on `;` and `:`, yielding 5 floats:
```
voltage ; temperature ; bias ; tx_pwr ; rx_pwr
```
This matches the ONU List performance string format exactly. The OLT-level variant
(`ParseOltParameters`) additionally computes `tx_pwr = 10*log10(raw)` and extracts a `pn=` field.

---

## HTTP API on port 128 — Status: FULLY REVERSE ENGINEERED

The firmware's standalone web UI was captured and decoded, and a working Python client was
built and tested live. Full documentation + client: **[web-api/](web-api/)**
(see [web-api/README.md](web-api/README.md)).

Summary of what it is:
- **Auth:** `GET /api/challenge` → `md5(password + nonce)` → `POST /api/login` → `token`,
  passed as `?token=` on every call. Roles: admin/operator/viewer.
- **Realtime over SSE:** device info, ONU status, and terminal-command output are pushed on
  `GET /api/sse` — the `*/refresh` and `/api/cmd` POSTs only trigger the work.
- **Two gotchas:** the embedded server (1) rejects JSON with whitespace (send compact JSON),
  and (2) drops the first one or two triggers on a cold SSE stream (re-fire until events arrive;
  use a separate connection for triggers).
- **Endpoints** cover device/config/user, ONU list, terminal commands, whitelist (binary
  up/download), firmware upgrade, and password management — full table in the web-api README.
- Verified live against OLT SN `C2604649438` (100.64.2.147:128): login, device optics, ONU
  optics, and `get_olt_pn()` all working.

This means the grid-agent now has **two** fully-understood ways to drive v2 OLTs: the binary
protocol above, or this HTTP API. The HTTP API is the simpler integration (plain JSON + SSE,
no per-frame hashing, no raw sockets).

---

## Files Added for v2

```
fs/
├── FS_GPON_OLT_STICK_V2_Setup-new.msi          # New installer (FS PON Manager V1.1.0)
├── FIRMWARE_V2_FINDINGS.md                      # This file
├── decompiled/
│   └── APP_OLT_Stick_V2_new.cs                 # Decompiled new app (11,801 lines, clean)
├── web-api/                                    # Port-128 HTTP API: docs + working Python client
│   ├── README.md                               # Full HTTP/SSE API reference
│   ├── olt_web_client.py                       # Tested Python client
│   └── captures/                               # HAR, analysis, web UI source
├── v2-app-files/                               # Files extracted from the new MSI
│   ├── appmanagement.ini                       # Vendor keys (fsoptics) + version
│   ├── service_type_template.txt               # Service profile template w/ field docs
│   └── whitelist_template.txt                  # Bulk whitelist template
└── official-docs/
    └── GPON OLT STICK WEB User Guide-new.docx  # New web UI user guide
```

## Reverse Engineering Method (v2)

1. Extracted the MSI: OLE compound doc → CAB payload → 30 files (`olefile` + `7z`/`cabextract`)
2. Identified the managed assembly (`APP_OLT_Stick_V2.dll`) among the extracted files
3. Installed .NET 8 SDK to `~/.dotnet` (no sudo) + `ICSharpCode.Decompiler 8.2.0.7535`
4. Resolved WinForms refs via the `Microsoft.WindowsDesktop.App.Ref` NuGet package
5. `DecompileWholeModuleAsString()` → clean C# (same toolchain as v1)
6. Traced the auth/optics/login paths to recover the wire protocol and the `fsoptics` key
