# White-Label OLT Platform — Reverse Engineering

Reverse engineering of GPON OLT SFP stick management platforms and building open-source replacements. These are pluggable SFP modules that turn any switch/router with an SFP port into a GPON OLT, managed via proprietary Windows software. This repo decodes the protocols and provides Linux-native management tools.

## Platforms

### [VOLT / Ainopol](volt/)
- **Model:** ZH-VOLT32 (32 ONU, Class B+, 8km)
- **Protocol:** Fully decoded — raw L2 Ethernet, EtherType `0x88B6`, proprietary frame format
- **Status:** Complete RE, wire format confirmed with Wireshark, protocol spec documented
- **Manufacturer:** AINOPOL (智慧光迅), Sichuan, China. Also sold as HT-VOLT32, CatvScope VOLT-32.

### [FS.com](fs/)
- **Model:** GPON-SFP-OLT-MAC-I (128 ONU, Class C+, 20km)
- **Protocol:** Fully decoded — UDP/IP (ports 64219/64218), ARP discovery with `"OLT"` marker
- **Status:** Complete RE, full C# source decompiled, management tools built and tested live
- **Vendor:** FS.com
- **Tools built:**
  - **[grid-agent](fs/grid-agent/)** — Go site agent, runs on local subnet, exposes REST API
  - **[grid-pon-manager](fs/grid-pon-manager/)** — Next.js dashboard, connects to agents remotely

## Architecture

```
grid-pon-manager (Next.js)            grid-agent (Go, per site)
  Dashboard UI + Central API            Local OLT management
         |                                     |
         |-- REST over ZeroTier/WAN --|         |-- UDP 64219/64218 (local)
         |                                     |
    +---------+                          +-----+-----+
    | Site A  |                          | OLT1  OLT2|
    | Site B  |                          | OLT3  OLT4|
    +---------+                          +-----------+
```

## Repo Structure

```
├── volt/                        # VOLT/Ainopol OLT SFP stick (complete RE)
│   ├── binaries/                # Original Windows executables
│   ├── extracted/               # PyInstaller-extracted bundles
│   ├── decrypted/               # PyArmor-decrypted bytecode
│   ├── firmware/                # FPGA bitstream firmware images
│   ├── scripts/                 # RE scripts, protocol decoders, network probes
│   ├── pcap/                    # Wireshark captures
│   └── docs/                    # Protocol spec, RE report, research
│
├── fs/                          # FS.com OLT SFP stick (complete RE + tools)
│   ├── grid-agent/              # Go site agent (REST API + UDP OLT client)
│   ├── grid-pon-manager/        # Next.js central management dashboard
│   ├── decompiled/              # Full C# source (10,954 lines, no obfuscation)
│   ├── official-docs/           # FS datasheets and config guides
│   ├── pcap/                    # Wireshark captures
│   └── parse_fs_protocol.py     # Protocol decoder
│
└── README.md
```

## Key Findings

### VOLT Protocol (fully decoded)
```
Ether(dst=broadcast, type=0x88B6) / magic(0xB958D63A) + seq + len + msg_group + param_id + target + data
Response: msg_group + 0x0100, sequence echoed, unicast back to requester
```
- MikroTik switches silently drop `0x88B6` frames — requires media converter for management

### FS Protocol (fully decoded)
```
Discovery: ARP broadcast with "OLT" marker (L2, local subnet only)
Commands:  UDP port 64219 (PC->OLT), port 64218 (OLT->PC)
Payload:   [cmd_code(1)][sequence(2)][data(19)]
Auth:      cmd=66, fixed key, response 0x77=write 0x72=read granted
```
- 18 command codes: shake_hand, whitelist, service profiles, alarms, firmware upgrade
- Works through switches (standard IP/UDP)
- Remote management via NAT rules (shake_hand + auth work; data queries need local agent)

### Remote Management (confirmed working)
Two NAT rules per site router enable remote shake_hand and auth over ZeroTier:
```
/ip firewall nat add chain=srcnat src-address=<zt-subnet> dst-address=<olt-subnet> action=src-nat to-addresses=<router-ip>
/ip firewall nat add chain=dstnat src-address=<olt-subnet> dst-address=<router-ip> dst-port=64218 protocol=udp action=dst-nat to-addresses=<mgmt-ip>
```
Full OLT management (ONU queries, alarms, whitelist) requires the grid-agent running on the local subnet.

## Methodology

1. PyInstaller extraction + PyArmor v52.6.11 decryption via `sys.settrace()` runtime hooking (VOLT)
2. .NET 8 decompilation with ICSharpCode.Decompiler + PDB symbols (FS)
3. Wireshark capture analysis of bidirectional traffic
4. Complete wire format decoded for both platforms
5. Live testing against real OLT hardware over ZeroTier
