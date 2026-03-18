# VOLT OLT Platform — Reverse Engineered

Reverse engineering of the VOLT GPON OLT management platform (GDT v6.6) and firmware (v9.1). The original tools are Windows-only PyQt5 apps protected with PyArmor. This repo contains the original binaries, extracted source, decrypted protocol definitions, and a full technical report.

## What is this?

VOLT is a white-label GPON OLT (Optical Line Terminal) sold under multiple brands — Ainopol, Nislight, Sirivision, Middle, Wowgrup. The management software communicates with OLT hardware over **raw Layer 2 Ethernet** using a custom protocol. There is no public documentation for the protocol.

The platform appears to be closely related to (or a clone of) the **Tibit MicroPlug OLT** architecture used by Juniper's Unified PON / MicroClimate Management System (MCMS). See the [Juniper MCMS Installation Guide](https://www.juniper.net/documentation/us/en/software/unified-pon/unified-pon-microclimate-installation-guide.pdf) and [TIBIT_MCMS_FINDINGS.json](TIBIT_MCMS_FINDINGS.json) for details on the management protocol (IEEE 1904.2 over VLAN 4090).

**Manufacturer:** AINOPOL (智慧光迅 / sczhgx.com), Sichuan, China. Model ZH-VOLT32. Also sold as HT-VOLT32 and other OEM rebrandings. Comes with the free Windows VOLT management tool. The SFP stick works in any switch/router with an SFP port — management is done via L2 Ethernet frames through the host device's bridge.

This repo cracks open the entire stack:
- Protocol message format (7 message types, 84 message IDs)
- OMCI support (ITU-T G.988 compliant, 23 message types)
- Firmware upgrade sequence (14-step dual-image flash process)
- Authentication system (AES + CMAC)
- Hardware details (custom FPGA ASIC, BoB transceiver chips)

## Repo Structure

```
├── REVERSE_ENGINEERING_REPORT.json   # Full technical report
├── decrypted_source/                # Decrypted Python bytecode + disassembly
├── VOLT_Tool_V6.6.exe_extracted/    # Extracted PyInstaller bundle (main tool)
├── Neutral_Tool_V6.6.exe_extracted/ # Extracted neutral/unbranded variant
├── Ainopol UPG V6.6.exe_extracted/  # Extracted Ainopol upgrade tool
├── extracted_modules/               # Raw .pyc modules from PYZ archive
├── decompiled/                      # Intermediate decompilation artifacts
├── 20251111-VOLT_ALL_9.1/           # Firmware binaries + docs
├── VOLT-Upgrades_and_tutorials/     # Upgrade tool + tutorials
├── VOLT_Tool_V6.6.exe              # Original management tool (LFS)
├── Neutral_Tool_V6.6.exe           # Original neutral tool (LFS)
├── decrypt_*.py                     # Decryption/hooking scripts
├── protocol_capture.py              # Scapy hook for live packet capture
├── dump_bytecode.py                 # Runtime bytecode dumper
└── trace_hook.py                    # sys.settrace hook for function tracing
```

## Protocol Overview

Communication happens over **raw Ethernet frames** (Layer 2, not TCP/IP) using **EtherType 0x88B6** (IEEE 802.1 Local Experimental). The tool broadcasts to `ff:ff:ff:ff:ff:ff` to discover OLTs on the local network segment.

### Wire Format (confirmed via Wireshark capture)

```
Ethernet: DST=ff:ff:ff:ff:ff:ff SRC=<host> Type=0x88B6
Payload (50 bytes):
  [0:4]   Magic:      0xB958D63A (uint32 BE, constant)
  [4:6]   Sequence:   uint16 BE (auto-incrementing)
  [6:8]   Length:      uint16 BE (data length, typically 0x000C)
  [8:10]  Message ID:  uint16 BE (see EnumMessageId)
  [10]    Sub-field:   byte (type/flags)
  [11]    Target:      byte (0xFF=broadcast, 0x00-0x1F=ONU index)
  [12:50] Data:        request/response payload
```

### Message Types

| Value | Type        | Description              |
|-------|-------------|--------------------------|
| -1    | Init        | Discovery/initialization |
| 0     | Get         | Query a parameter        |
| 1     | GetResponse | Response to query        |
| 2     | Set         | Set a parameter          |
| 3     | SetResponse | Response to set          |
| 4     | Notify      | Async event notification |
| 5     | OMCI        | OMCI message passthrough |

### Key Message IDs

84 total. Grouped by function:

- **System**: OltVersion, OltUpTime, OltTemperature, OltIntVoltage, FpgaDna, Reboot
- **Flash**: FlashRead, FlashErase, FlashWrite, FlashErase64K, FlashProtect, FlashWrite1K
- **Auth**: LoginStat, OltPassword, OltLogout, ChangePassword, ResetPassword
- **Optical**: OpticalTxEnable, OpticalVoltage, OpticalCurrent, OpticalPower, OpticalTemperature, OpticalModuleIIC
- **ONU Mgmt**: OnuStatus, OnuGponSn, OnuTxPower, OnuRxPower, ActivateOnu, DeactivateOnu, DisableSN, MaxONU
- **Counters**: BroadcastTx/Rx, MulticastTx/Rx, UnicastTx/Rx, BipErrorCnt, MacTabelAddCnt
- **Config**: LoidAuthSwitch, RogueOnuDetectSwitch, DiscoverOnuSwitch, P2PSwitch, EthAnSwitch, OmciMode
- **Events**: OnuStatusChange, RogueOnuDetected, DiscoverOnuGponSn

Full list in [REVERSE_ENGINEERING_REPORT.json](REVERSE_ENGINEERING_REPORT.json).

## Using the Extracted Source

### What you have

The `decrypted_source/` directory contains disassembled bytecode for every module. The key files:

| File pattern | Contains |
|---|---|
| `_frozen com_def__*` | All protocol enums, message IDs, data structures |
| `_frozen olt_socket__*` | Layer 2 socket implementation (Ether + LLC framing) |
| `_frozen gdt__*` | Main app logic, AES/CMAC crypto, firmware upgrade |
| `_frozen OltInfoWidget__*` | OLT card UI + upgrade thread with flash read/write/erase |
| `_frozen OnuInfoWidget__*` | ONU management (activate, deactivate, optical stats) |
| `_frozen OmciMsgWidget__*` | OMCI message display |
| `_frozen PasswordWidget__*` | Auth flow (login, change password, reset) |

### Reading the disassembly

Each `.txt` file contains Python bytecode disassembly with full symbol tables:

```
============================================================
CODE: <module> (file: <frozen com_def>)
  names: ('datetime', 'enum', 'Enum', 'IntEnum', 'GdtVersion', 'AesKey', ...)
  consts: [None, 0, 1, 2, 'Nislight', 'Ainopol', ...]

  1           0 LOAD_CONST               0 (0)
              2 IMPORT_NAME              0 (datetime)
              ...
```

The `names` list tells you every symbol the code references. The `consts` list has string literals, numeric constants, and enum values. The `varnames` list has local variable names. Between these three you can reconstruct the logic.

### Building on top of this

1. **Reconstruct the protocol** — The wire format is now fully decoded from Wireshark captures (`pcap/volt.pcapng`). Frame is `Ether(dst=broadcast, type=0x88B6) / payload` where payload is: magic (4B) + sequence (2B) + length (2B) + message_id (2B) + sub_field (1B) + target (1B) + data (38B). Use `parse_protocol.py` to analyze captures.

2. **Replicate the socket layer** — `olt_socket` uses Scapy's `sendp()`/`srp()` for Layer 2 communication. The `OltListenThread` listens for incoming frames, `oltSocketThread` handles sends. Key helpers: `twos_comp()` for signed optical power values, `char2hex()`/`hex2char()` for encoding.

3. **Implement auth** — AES key is in `com_def.AesKey`. Auth uses `Crypto.Cipher.AES` + `Crypto.Hash.CMAC`. Password is 8 chars, sent via `OltPassword` message ID, with `LoginStat` to check state.

4. **Implement firmware upgrade** — The 14-step sequence is in `oltUpgradeThread.run()`. It uses Reed-Solomon (`reedsolo.RSCodec`) for error correction. Flash layout: dual-image at `FirstAddr` and `SecondAddr_4M`/`SecondAddr_8M`.

5. **Capture live traffic** — Run `protocol_capture.py` alongside the original Windows tool talking to a real OLT. It hooks Scapy's `sendp`/`srp` to log every packet in JSON format. This gives you the exact frame structure.

### Hardware

- FPGA ASIC with IDCODE `0x0222C143` (Xilinx 7-series config interface, non-standard manufacturer)
- BoB transceivers: GN25L99, NRG6533 (with dedicated I2C register configs)
- UX3326 transceiver chip
- Firmware is a raw FPGA bitstream (~1.5 MB), sync word `0xAA995566`

## Extraction Methodology

1. Identified PyInstaller bundles → extracted with `pyinstxtractor.py`
2. Found PyArmor v52.6.11 protection on all 18 app modules
3. Downloaded Python 3.9 64-bit to match `pytransform.pyd` ABI
4. Used `sys.settrace()` to hook into PyArmor's runtime decryption
5. Captured 161 decrypted code objects with full symbol tables and bytecode
6. Extracted all runtime enum values by importing `com_def` through pytransform
7. Captured actual VOLT tool traffic via Wireshark (pcap/volt.pcapng)
8. Decoded wire format: EtherType 0x88B6, magic 0xB958D63A, 50-byte payload
