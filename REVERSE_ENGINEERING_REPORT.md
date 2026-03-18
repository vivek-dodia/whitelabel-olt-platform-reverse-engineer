# VOLT OLT Controller - Complete Reverse Engineering Report

## 1. SYSTEM OVERVIEW

**Product:** VOLT GPON OLT (Optical Line Terminal) Management System
**Application Name:** GDT (GPON Debug Tool) v6.6
**Firmware Version:** 9.1
**Build Technology:** Python 3.9 + PyQt5, bundled with PyInstaller, protected with PyArmor v52.6.11

### Hardware Variants
| Model | Ports | Speed | Firmware Size |
|-------|-------|-------|---------------|
| VOLT16 | 16 PON | 1G GPON | 1.51 MB |
| VOLT16 | 16 PON | 2.5G GPON | 1.52 MB |
| VOLT32 | 32 PON | 1G GPON | 1.52 MB |
| VOLT32 | 32 PON | 2.5G GPON | 1.52 MB |

### OEM/White-Label Customers
- **Nislight** (0)
- **Ainopol** (1)
- **Sirivision** (2)
- **Middle** (3)
- **Wowgrup** (4)

### Three Tool Variants
1. **VOLT_Tool_V6.6.exe** - Branded VOLT management tool
2. **Neutral_Tool_V6.6.exe** - Unbranded/neutral variant (7 bytes size difference, ~4.9M differing bytes)
3. **Ainopol UPG V6.6.exe** - Ainopol-branded firmware upgrade utility

---

## 2. APPLICATION ARCHITECTURE

### Module Map
```
gdt.py                  <- Main application entry point (GponDebugToolWindow)
gdt_form.py             <- Qt Designer UI form (Ui_GponDebugTool)
olt_socket.py           <- Layer 2 Ethernet communication protocol
com_def.py              <- Protocol constants, enums, data structures
OltInfoWidget.py        <- OLT device card widget + firmware upgrade logic
OnuInfoWidget.py        <- ONU (subscriber) information widget
OmciMsgWidget.py        <- OMCI message viewer/sender
PasswordWidget.py       <- Admin password management dialog
MessageWidget.py        <- Status message dialog
TextInputWidget.py      <- Text input dialog (password entry, alias)
WaitBobWriteWidget.py   <- Wait dialog for BoB register writes
WaitPasswordSaveWidget.py <- Wait dialog for password save to flash
logo_rc.py              <- Embedded logo/icon resources
```

### Dependencies
- **PyQt5** - GUI framework (with qdarkstyle theme)
- **Scapy** - Raw Layer 2 Ethernet packet construction
- **PyCryptodome** - AES encryption, CMAC authentication
- **reedsolo** - Reed-Solomon error correction (for firmware integrity)
- **pyperclip** - Clipboard operations (copy DNA/serial)
- **binascii** - Hex encoding/decoding

### Main Application Classes
```
GponDebugToolWindow(QMainWindow)    <- Main window, manages all OLT cards
  |-- ReadAxRegThread(Thread)       <- AX register read thread
  |-- load_default_25l99_reg_thread <- GN25L99 BoB default config
  |-- load_default_6533_reg_thread  <- NRG6533 BoB default config
  |-- MaskWidget(QWidget)           <- Overlay mask widget

WidgetOltInfo(QWidget)              <- Per-OLT device card
  |-- oltBootCheckThread(Thread)    <- Boot check sequence
  |-- oltUpgradeThread(Thread)      <- Firmware upgrade sequence

OltListenThread(Thread)             <- Incoming packet listener
oltSocketThread                     <- Socket communication handler

WidgetOnuInfo(QWidget)              <- Per-ONU subscriber card
WidgetOmciMsg(QWidget)              <- OMCI message display
WidgetPassword(QDialog)             <- Password management
WidgetTextInput(QDialog)            <- Generic text input
WidgetWaitBobWrite(QDialog)         <- BoB write wait
WidgetWaitPasswordSave(QDialog)     <- Password save wait
WidgetMessage(QDialog)              <- Message display
```

---

## 3. COMMUNICATION PROTOCOL

### Transport Layer
- **Layer 2 Ethernet** (NOT TCP/IP) via Scapy
- **EtherType: 0x88B6** (IEEE 802.1 Local Experimental EtherType 2)
- Broadcast discovery to `ff:ff:ff:ff:ff:ff`
- Direct Ethernet communication (requires NIC in same broadcast domain)
- Confirmed via Wireshark capture of actual VOLT tool traffic

### Wire Format (confirmed from pcap analysis)
```
+-----------------------------------------------------------+
| Ethernet Header                                           |
|   DST: ff:ff:ff:ff:ff:ff (broadcast)                      |
|   SRC: <host MAC>                                         |
|   EtherType: 0x88B6                                       |
+-----------------------------------------------------------+
| VOLT Payload (50 bytes)                                   |
|   [0:4]   Magic:     0xB958D63A (uint32 BE)               |
|   [4:6]   Sequence:  uint16 BE (auto-increment)           |
|   [6:8]   Length:    uint16 BE (payload data length)       |
|   [8:10]  Message ID: uint16 BE (EnumMessageId)           |
|   [10]    Sub-field: byte (msg type/flags)                |
|   [11]    Target:    byte (0xFF=all, 0x00-0x1F=ONU idx)   |
|   [12:50] Data:      payload (zeros for Get requests)     |
+-----------------------------------------------------------+
```

### Security
- **AES encryption** with module-level `AesKey` defined in `com_def`
- **CMAC authentication** (Crypto.Hash.CMAC) for message integrity
- **Reed-Solomon** error correction for firmware data integrity
- **Password authentication** - 8-character alphanumeric, default `12345678`
  - 5 failed attempts = 10-minute lockout
  - Reset via dealer CODE + OLT power cycle within 30 seconds

### Message Format (com_def)

#### Message Types (EnumMessageType)
| Value | Type | Description |
|-------|------|-------------|
| 0 | Init | Initialization/discovery |
| 1 | Get | Query parameter |
| 2 | GetResponse | Response to query |
| 3 | Set | Set parameter |
| 4 | SetResponse | Response to set |
| 5 | Notify | Asynchronous notification |
| 6 | OMCI | OMCI message passthrough |

#### Message IDs (EnumMessageId) - Complete List
| # | ID | Category |
|---|---|----------|
| | **System Info** | |
| 1 | Init | System initialization |
| 2 | OltVersion | Firmware version query |
| 3 | OltUpTime | System uptime |
| 4 | LedSwitch | LED control |
| 5 | OltTemperature | Current temperature |
| 6 | OltTemperatureMax | Maximum recorded temperature |
| 7 | OltIntVoltage | Internal voltage |
| 8 | OltAuxVoltage | Auxiliary voltage |
| 9 | ResetLedEnable | Reset LED enable state |
| 10 | FpgaDna | FPGA DNA (unique ID) |
| | **Flash Operations** | |
| 11 | FlashRead | Read flash memory |
| 12 | FlashErase | Erase flash sector |
| 13 | FlashWrite | Write flash memory |
| 14 | FlashErase64K | Erase 64KB flash block |
| 15 | FlashProtect | Flash write protection |
| 16 | FlashWrite1K | Write 1KB to flash |
| | **Network/Speed** | |
| 17 | UplinkSpeed | Uplink port speed |
| | **Authentication** | |
| 18 | LoginStat | Login status check |
| 19 | OltPassword | Password verification |
| 20 | OltLogout | Logout |
| 21 | ChangePassword | Change password request |
| 22 | ChangePasswordRet | Change password result |
| 23 | ResetPassword | Reset password to default |
| 24 | Reboot | System reboot |
| | **Optical Module** | |
| 25 | OpticalTxEnable | TX laser enable/disable |
| 26 | OpticalVoltage | SFP module voltage |
| 27 | OpticalCurrent | SFP bias current |
| 28 | OpticalPower | TX/RX optical power |
| 29 | OpticalTemperature | SFP temperature |
| | **Traffic Counters** | |
| 30 | BroadcastTxCnt | Broadcast TX counter |
| 31 | BroadcastRxCnt | Broadcast RX counter |
| 32 | MulticastTxCnt | Multicast TX counter |
| 33 | MulticastRxCnt | Multicast RX counter |
| 34 | UnicastTxCnt | Unicast TX counter |
| 35 | UnicastRxCnt | Unicast RX counter |
| | **Authentication Config** | |
| 36 | LoidAuthSwitch | LOID authentication on/off |
| 37 | AuthLoid | Authentication LOID value |
| 38 | AuthPassword | Authentication password |
| 39 | RogueOnuDetectSwitch | Rogue ONU detection |
| 40 | DiscoverOnuSwitch | ONU auto-discovery |
| | **Optical Diagnostics** | |
| 41 | OpticalModuleIIC | I2C read of optical module |
| 42 | PeriodicalOptical | Periodic optical monitoring |
| 43 | PeriodicalOpticalOnPeriod | On-period for periodic mode |
| 44 | PeriodicalOpticalOffPeriod | Off-period for periodic mode |
| 45 | Prbs23Enable | PRBS23 test pattern enable |
| 46 | Prbs23ErrorCnt | PRBS23 error counter |
| 47 | OpticalSD | Signal detect threshold |
| 48 | Ux3326Reset | UX3326 chip reset |
| | **ONU Management** | |
| 49 | MaxONU | Maximum ONU count |
| 50 | OmciMode | OMCI operation mode |
| 51 | P2PSwitch | Point-to-point mode |
| 52 | EthAnSwitch | Ethernet auto-negotiation |
| 53 | CoverOffline | Cover offline mode |
| 54 | OnuOnlineNumber | Currently online ONU count |
| 55 | OnuStatus | ONU status query |
| 56 | OnuUpTime | ONU online duration |
| 57 | OnuDownTime | ONU offline timestamp |
| 58 | OnuTxPower | ONU TX power (upstream) |
| 59 | OnuRxPower | ONU RX power (downstream) |
| 60 | OnuGponSn | ONU GPON serial number |
| 61 | OnuRangeTime | ONU ranging time/distance |
| 62 | OnuLoid | ONU LOID identifier |
| 63 | DeactivateOnu | Deactivate specific ONU |
| 64 | OnuPassword | ONU password |
| 65 | DisableSN | Disable ONU by serial number |
| 66 | OnuResponseTime | ONU response time |
| 67 | OnuLastOfflineReason | Last offline reason |
| 68 | ActivateOnu | Activate specific ONU |
| 69 | OnuOpticalInfo | ONU optical diagnostics |
| 70 | OnuOpticalInfoUpdate | Update ONU optical info |
| | **Events** | |
| 71 | OnuStatusChange | ONU status change notification |
| 72 | RogueOnuDetected | Rogue ONU detected event |
| 73 | DiscoverOnuGponSn | Discovered ONU serial number |
| | **Debug/Advanced** | |
| 74 | OmciErrorCnt | OMCI error counter |
| 75 | ResetPonRx | Reset PON receiver |
| 76 | BWmapLoopMax | Bandwidth map loop maximum |
| 77 | BurstResetOffset | Burst mode reset offset |
| 78 | FanPwnWidth | Fan PWM width control |
| 79 | BipErrorCnt | BIP error counter |
| 80 | MacTabelAddCnt | MAC table add counter |
| 81 | MacTabelAgingCnt | MAC table aging counter |
| 82 | PwmCnt | PWM counter |
| 83 | PwmWidth | PWM width value |
| 84 | OmciPackage | OMCI package data |

---

## 4. OMCI PROTOCOL SUPPORT

### OMCI Message Types (ITU-T G.988 compliant)
| Value | Type |
|-------|------|
| 4 | Create |
| 6 | Delete |
| 8 | Set |
| 9 | Get |
| 11 | GetAllAlarms |
| 12 | GetAllAlarmsNext |
| 13 | MibUpload |
| 14 | MibUploadNext |
| 15 | MibReset |
| 16 | Alarm |
| 17 | AttributeValueChange |
| 18 | Test |
| 19 | StartSoftwareDownload |
| 20 | DownloadSection |
| 21 | EndSoftwareDownload |
| 22 | ActivateSoftware |
| 23 | CommitSoftware |
| 24 | SynchronizeTime |
| 25 | Reboot |
| 26 | GetNext |
| 27 | TestResult |
| 28 | GetCurrentData |
| 29 | SetTable |

### OMCI Device Identifiers
| Value | Type |
|-------|------|
| 0x0A | Baseline message set |
| 0x0B | Extended message set |

### Supported Managed Entities (G.988 Class IDs)
| Class ID | Entity |
|----------|--------|
| 2 | ONU Data |
| 5 | Cardholder |
| 6 | Circuit Pack |
| 7 | Software Image |
| 45 | MAC Bridge Service Profile |
| 46 | MAC Bridge Configuration Data |
| 47 | MAC Bridge Port Configuration Data |
| 256 | ONU-G |
| 263 | ANI-G |

---

## 5. FIRMWARE UPGRADE PROCESS

### Upgrade Sequence (14 steps)
```
1. CheckVersion      <- Compare firmware versions
2. CheckMaxOnu       <- Verify max ONU count compatibility
3. UpdateDNA         <- Update FPGA DNA (unique ID)
4. CheckRunning      <- Check which image is active
5. Unlock            <- Unlock flash for writing
6. Erase             <- Erase target flash region
7. Program           <- Write new firmware data
8. CheckResult       <- Verify write integrity
9. TryRecover        <- Recovery attempt if verification fails
10. RecheckResult    <- Re-verify after recovery
11. Fallback         <- Fallback to other image if needed
12. Switch           <- Switch active image
13. CheckStartup     <- Verify boot parameters
14. Finish           <- Finalize upgrade
```

### Upgrade Result Codes
| Code | Meaning |
|------|---------|
| Success | Upgrade completed successfully |
| Fail | Upgrade failed |
| Latest | Already on latest version |
| Mismatch | Hardware/firmware mismatch |
| Timeout | Communication timeout |
| RebootCheckOK | Post-reboot check passed |
| MismatchOnuNum | ONU count mismatch |

### Flash Memory Layout
- **Dual-image support** (active/backup)
  - `FirstAddr` - Primary image location
  - `SecondAddr_4M` - Secondary image at 4MB offset
  - `SecondAddr_8M` - Secondary image at 8MB offset
- **Flash operations:** Read, Erase (sector), Erase64K (block), Write, Write1K
- **Protection:** Flash write protection toggle

### Firmware Binary Format
- **Type:** Xilinx-compatible FPGA bitstream
- **Sync word:** `0xAA995566` at offset `0x30`
- **IDCODE:** `0x0222C143` (custom ASIC, manufacturer ID `0x0A1`)
- **Build date:** Embedded in last 8 bytes of file (e.g., "20251107")
- **Size:** ~1.5 MB per variant

---

## 6. HARDWARE DETAILS

### FPGA/ASIC
- Xilinx 7-series compatible configuration interface
- Custom ASIC with IDCODE `0x0222C143` (not standard Xilinx part)
- JTAG manufacturer ID `0x0A1` (non-standard, likely Chinese PON ASIC)
- FPGA DNA used for device identification and license binding

### Optical Transceiver (BoB)
- **GN25L99** - BoB component with dedicated register configuration
- **NRG6533** - BoB component with dedicated register configuration
- **UX3326** - Transceiver chip with reset capability
- I2C bus interface for optical module diagnostics (DDM/DOM)
- Supports: TX enable, voltage, current, power, temperature monitoring

### ONU States
| State | Description |
|-------|-------------|
| Unused | Not configured |
| Online | Active and communicating |
| DyingGasp | Power failure reported |
| Offline | Not responding |
| Ranging | Performing ranging process |
| RangeFail | Ranging failed |
| Disabled | Administratively disabled |
| Omci | In OMCI configuration state |
| OmciFail | OMCI configuration failed |

### ONU Offline Reasons
| Reason | Description |
|--------|-------------|
| NA | Unknown/not applicable |
| DyingGasp | Power failure at ONU |
| Los | Loss of Signal |
| Lof | Loss of Frame |
| Loam | Loss of PLOAM |
| AuthFail | Authentication failure |
| Deactivate | Admin deactivation |
| OmciFail | OMCI setup failure |
| RangeFail | Ranging failure |

---

## 7. HELPER FUNCTIONS

### olt_socket module
- `twos_comp()` - Two's complement conversion for signed integer handling
- `round_to_nearest_integer()` - Rounding utility for optical power calculations

### com_def module
- `char2hex()` - Character to hex string conversion
- `hex2char()` - Hex string to character conversion

### Data Structures
- `StPackageInfo` - Firmware package metadata
- `StIicInfo` - I2C/optical module diagnostic data
- `StOltConfig` - OLT configuration parameters
- `StPasswordInfo` - Authentication state and credentials

---

## 8. UI TABS AND FEATURES

| Tab | Function |
|-----|----------|
| OnuInfo | View connected ONUs, activate/deactivate, view optical stats |
| Notify | Event notifications (status changes, rogue ONU, discovery) |
| BoB | Optical transceiver configuration (GN25L99, NRG6533 registers) |
| OMCI | OMCI message viewer and sender |
| Upgrade | Firmware upgrade with progress bar |
| Config | OLT configuration (LOID auth, P2P, auto-negotiation, etc.) |
| Debug | Debug tools (PRBS, counters, bandwidth map, error counters) |
| About | Version information |

---

## 9. EXTRACTION METHODOLOGY

1. Identified executables as PyInstaller bundles (Python 3.9)
2. Extracted with `pyinstxtractor.py` (618 PYZ modules)
3. Discovered PyArmor v52.6.11 protection on all app modules
4. Downloaded Python 3.9 64-bit embeddable to match `pytransform.pyd` ABI
5. Used `sys.settrace()` to capture decrypted code objects at runtime
6. Captured 161 code objects with full symbol tables, constants, and bytecode
7. Cross-referenced with firmware binary analysis and documentation

---

*Generated: 2026-03-17*
*Tool: Claude Code reverse engineering pipeline*
