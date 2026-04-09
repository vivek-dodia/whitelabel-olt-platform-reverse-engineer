# White-Label OLT Platform — Reverse Engineering

Reverse engineering of GPON OLT SFP stick management platforms. These are pluggable SFP modules that turn any switch/router with an SFP port into a GPON OLT, managed via proprietary Windows software. This repo decodes the protocols and builds toward an open-source, Linux-native management tool.

## Platforms

### [VOLT / Ainopol](volt/)
- **Model:** ZH-VOLT32 (32 ONU, Class B+, 8km)
- **Protocol:** Fully decoded — raw L2 Ethernet, EtherType `0x88B6`, proprietary frame format
- **Status:** Complete RE, wire format confirmed with Wireshark, protocol spec documented
- **Manufacturer:** AINOPOL (智慧光迅), Sichuan, China. Also sold as HT-VOLT32, CatvScope VOLT-32.

### [FS.com](fs/)
- **Model:** GPON-SFP-OLT-MAC-I (128 ONU, Class C+, 20km)
- **Protocol:** IP-based management (ARP discovery + TCP/UDP), reverse engineering in progress
- **Status:** FS PON Manager V1.0.0 captured, initial analysis done
- **Vendor:** FS.com

## Repo Structure

```
├── volt/                    # VOLT/Ainopol OLT SFP stick (complete RE)
│   ├── binaries/            # Original Windows executables
│   ├── extracted/           # PyInstaller-extracted bundles
│   ├── decrypted/           # PyArmor-decrypted bytecode
│   ├── firmware/            # FPGA bitstream firmware images
│   ├── scripts/             # RE scripts, protocol decoders, network probes
│   ├── pcap/                # Wireshark captures
│   └── docs/                # Protocol spec, RE report, research
│
├── fs/                      # FS.com OLT SFP stick (in progress)
│
└── README.md                # This file
```

## Key Findings

### VOLT Protocol (fully decoded)
```
Ether(dst=broadcast, type=0x88B6) / magic(0xB958D63A) + seq + len + msg_group + param_id + target + data
Response: msg_group + 0x0100, sequence echoed, unicast back to requester
```

### FS Protocol (initial analysis)
```
ARP-based discovery with code=OLT, IP-based management (192.168.15.x), CLI-style commands
```

### Critical Discovery
MikroTik switches (CRS354, CCR2004) silently drop EtherType `0x88B6` frames even with HW offload disabled. A dumb media converter (L1 PHY) is required for VOLT OLT management. FS.com OLT sticks use IP-based management which avoids this issue entirely.

## Methodology

1. PyInstaller extraction + PyArmor v52.6.11 decryption via `sys.settrace()` runtime hooking
2. All 84 message IDs + enum values extracted from runtime
3. Wireshark capture of bidirectional traffic with working OLT
4. Complete wire format decoded with parameter map
