# FS.com OLT SFP Stick — Reverse Engineering

Reverse engineering of the FS.com GPON OLT SFP stick (GPON-SFP-OLT-MAC-I) and FS PON Manager V1.0.0.

## Status: FULLY DECODED

The FS PON Manager is a .NET 8 WinForms app with **zero obfuscation** and **PDB debug symbols included**. Full C# source decompiled in one pass using ICSharpCode.Decompiler.

## Hardware

| Spec | Value |
|------|-------|
| Model | GPON-SFP-OLT-MAC-I |
| Vendor | FS.com |
| Max ONUs | 128 |
| Max TCONTs | 1024 |
| Max GEM-Ports | 4096 |
| Class | C+ (32dB link budget) |
| Distance | 20km |
| Downstream | 2.488 Gbps |
| Upstream | 1.244 Gbps |
| Wavelength | 1490nm TX / 1310nm RX |
| Connector | SC/UPC |
| Uplink | SGMII / HSGMII (2.5Gbps full duplex) |
| Config Interface | I2C / UART |
| Power | 3.3V, 800mA max |
| Temperature | -40 to 85°C |
| OLT MAC OUI | 00:00:50 |
| Default IP subnet | 100.64.2.x |

## Protocol

### Discovery (L2 — local subnet only)
OLT periodically broadcasts an **ARP Request** with a special `"OLT"` marker appended after the standard ARP payload:
```
ARP Request:
  Sender IP:  <OLT IP>  (e.g., 100.64.2.200)
  Sender MAC: <OLT MAC> (e.g., 00:00:50:00:20:ee)
  Target IP:  0.0.0.0
  Extra data: 4F 4C 54 00 ... (ASCII "OLT" + padding)
```
The management app listens for these ARP packets to auto-discover OLTs.

### Communication (L3 — routable UDP)
All commands use standard **UDP/IP** — works through routers, VPNs, ZeroTier.
```
PC → OLT:  UDP src=64219, dst=64219
OLT → PC:  UDP src=64218, dst=64218  (NOTE: asymmetric ports!)
```

### Packet Format
```
UDP Payload:
  [0]     Command Code  (uint8, see Command Codes table)
  [1:3]   Sequence      (uint16 BE, auto-incrementing)
  [3+]    Command-specific data
```

### Command Codes
| Code | Name | Description |
|------|------|-------------|
| 1 | shake_hand | Handshake / keepalive |
| 2 | ip_configuration | Change OLT IP address |
| 3 | cpe_white_list_send | Push ONU whitelist entry |
| 4 | cpe_white_list_read_qty | Read whitelist count |
| 5 | ONU_WLIST_RPT | Whitelist report/readback |
| 6 | cpe_illegal_CPE_report | Unauthorized ONU report |
| 7 | cpe_alarm_report | ONU alarm report |
| 8 | olt_alarm_report | OLT alarm report |
| 9 | cpe_service_type_send | Push service profile config |
| 10 | cpe_white_list_del | Delete whitelist entry |
| 11 | cpe_opt_para_report | ONU optical parameters (DDM) |
| 12 | cpe_sn_status | ONU serial number + status |
| 13 | SERVICE_CONFIG_RPT | Service config report/readback |
| 66 | Password_cmd | Authentication (read/write enable) |
| 67 | Password_check_cmd | Password verification |
| 68 | OLT_Update_BIN_cmd | Firmware upgrade |
| 69 | OLT_Reset_Master_cmd | Reset master |
| 70 | OLT_Reset_Slave_cmd | Reset slave |
| 71 | OLT_Softreset_cmd | Soft reset |

### Connection Sequence
```
1. ARP discovery (or skip if OLT IP is known)
2. shake_hand (cmd=1) → OLT responds with MAC + IP + Serial Number
3. Password_cmd (cmd=66, data prefix 0x57) → "w" = Write GRANTED
4. Password_cmd (cmd=66, data prefix 0x52) → "r" = Read GRANTED
5. Continuous shake_hand keepalive (~every few seconds)
6. Poll: cpe_sn_status (cmd=12) for ONU status
7. Poll: olt_alarm_report (cmd=8) for alarms
```

### Authentication
The auth payload is identical for all OLTs: `5774b87337454200d4d33f80c4663dc5e5` (Write) and `5274b87337454200d4d33f80c4663dc5e5` (Read). The first byte is `0x57` = ASCII `W` or `0x52` = ASCII `R`, followed by a fixed key. Response byte `0x77` = `w` (granted) or `0x72` = `r` (granted).

### FCmd CLI Commands (via Send_FCmd in the app)
```
set_white_list_type(0x57,1)      — Enable whitelist mode
set_white_list_type(0x47,1)      — Enable graylist mode (default)
get_white_list_type()            — Query current mode (W/G)
add_one_onu("SN", 1-5)          — Add ONU to whitelist with service profile
del_one_onu("SN")               — Remove ONU from whitelist
delete_onu_whlist()              — Clear all whitelist entries
get_whitelst_number()           — Count whitelist entries
find_onu("SN")                  — Check if ONU is in whitelist
get_onu_optics("SN")            — Query ONU DDM (optical parameters)
del_service_type(1-5)           — Delete a service profile
reset_system()                  — Reboot OLT
```

### Operation Modes
- **Graylist (default):** All ONUs auto-register, uniform bandwidth profile (service type 1)
- **Whitelist:** Only provisioned ONUs register, per-ONU service profiles (1-5)
- Up to **5 service profiles** with **5 sub-service flows** each
- Bulk provisioning via TXT template files

## Remote Management

**Confirmed working** over ZeroTier with two NAT rules on the site router:

```
# Outbound: management server → OLT (translate source to local IP)
/ip firewall nat add chain=srcnat src-address=<mgmt-zerotier-subnet> \
  dst-address=<olt-subnet> action=src-nat to-addresses=<router-local-ip>

# Inbound: OLT response → management server (redirect asymmetric port)
/ip firewall nat add chain=dstnat src-address=<olt-subnet> \
  dst-address=<router-local-ip> dst-port=64218 protocol=udp \
  action=dst-nat to-addresses=<mgmt-server-zerotier-ip>
```

Successfully tested: shake_hand, write auth, read auth, ONU status queries — all remotely over ZeroTier from a different network.

## Observed OLTs

| OLT | IP | MAC | Serial |
|-----|-----|-----|--------|
| OLT1 | 100.64.2.200 | 00:00:50:00:20:ee | C2603236285 |
| OLT2 | 100.64.2.225 | 00:00:50:00:20:da | C2603236286 |

ONU connected: SN `TPLGD092299A` on OLT1.

## Key Differences from VOLT/Ainopol

| Feature | VOLT (Ainopol) | FS.com |
|---------|---------------|--------|
| Transport | Raw L2 (EtherType 0x88B6) | UDP/IP (ports 64219/64218) |
| Discovery | L2 broadcast | ARP with `"OLT"` marker |
| Remote mgmt | Not possible (L2 only) | Yes — standard IP routing + 2 NAT rules |
| OLT addressing | MAC only | IP address |
| Max ONUs | 32 | 128 |
| Class | B+ (8km) | C+ (20km, 32dB) |
| App protection | PyArmor (heavy obfuscation) | None (PDB symbols included) |
| App framework | Python 3.9 + PyQt5 | .NET 8 + WinForms |
| Switch compat | Media converter only | Works through any switch |
| Auth | AES + CMAC | Simple fixed key over UDP |
| Database | None | SQLite (local history + config) |
| Service profiles | N/A | 5 profiles, 5 sub-flows each |
| Whitelist/Graylist | N/A | Both modes supported |

## Directory Structure
```
fs/
├── README.md                  # This file
├── FS_PON_OLT_STICK_Setup.msi # Original installer (LFS)
├── official-docs/             # FS documentation
│   ├── cn_fs-pon-manager-app-configuration-guide-*.pdf
│   └── gpon-sfp-olt-mac-i-datasheet-*.pdf
├── decompiled/                # Full decompiled C# source
│   └── APP_OLT_Stick_V2.cs   # 10,954 lines, complete source
├── decompile/                 # Decompiler project
│   ├── Program.cs
│   └── decompile.csproj
├── fs-olt1.pcapng             # First capture (14,523 packets)
├── fs-olt2.pcapng             # Second capture (9,448 packets)
└── parse_fs_protocol.py       # Protocol decoder script
```
