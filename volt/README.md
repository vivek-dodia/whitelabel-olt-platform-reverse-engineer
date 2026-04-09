# VOLT / Ainopol OLT SFP Stick — Reverse Engineering

Reverse engineering of the VOLT GPON OLT management platform (GDT v6.6) and firmware (v9.1).

**Manufacturer:** AINOPOL (智慧光迅 / sczhgx.com), Sichuan, China. Model ZH-VOLT32.
**Management:** Windows-only PyQt5 app protected with PyArmor, communicates via raw L2 Ethernet (EtherType 0x88B6).

## Protocol Summary

- **EtherType:** `0x88B6` (IEEE 802.1 Local Experimental)
- **Magic:** `0xB958D63A`
- **Transport:** Raw Ethernet broadcast, no IP, no VLAN
- **Response:** Unicast to requesting MAC, msg_group + 0x0100
- **Host requirement:** Dumb media converter or direct SFP — MikroTik switches filter 0x88B6 frames

See [docs/PROTOCOL_SPEC.json](docs/PROTOCOL_SPEC.json) for the complete wire format.

## Directory Structure

```
volt/
├── binaries/          # Original Windows executables (LFS)
├── extracted/         # PyInstaller-extracted bundles
│   ├── VOLT_Tool/
│   ├── Neutral_Tool/
│   └── Ainopol_UPG/
├── decrypted/         # PyArmor-decrypted bytecode + disassembly
├── firmware/          # FPGA bitstream firmware images + docs
├── scripts/           # RE scripts, protocol decoders, network probes
├── pcap/              # Wireshark captures (request-only + bidirectional)
└── docs/              # Protocol spec, RE report, session log, research
```

## Key Files

| File | Description |
|------|-------------|
| `docs/PROTOCOL_SPEC.json` | Complete decoded wire protocol |
| `docs/REVERSE_ENGINEERING_REPORT.json` | Full technical RE report |
| `docs/research.json` | Protocol, hardware, ecosystem research |
| `pcap/working-olt-pcap.pcapng` | Bidirectional capture with working OLT |
| `scripts/parse_working_olt.py` | Protocol decoder for working capture |
