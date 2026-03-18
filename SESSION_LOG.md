# Session Log: VOLT OLT Reverse Engineering & Live Testing

## What Was Accomplished

### Phase 1: Static Reverse Engineering (Complete)
1. Identified all three executables as PyInstaller bundles (Python 3.9)
2. Extracted with `pyinstxtractor.py` — 618 PYZ modules
3. Discovered PyArmor v52.6.11 protection on all 18 app modules
4. Downloaded Python 3.9 64-bit embeddable to match `pytransform.pyd` ABI
5. Used `sys.settrace()` to hook into PyArmor runtime decryption
6. Captured 161 decrypted code objects with full symbol tables
7. Extracted ALL runtime enum values by importing `com_def` through pytransform

### Key Protocol Values Extracted
- **AES Key:** `D53C068D406B3BD55C0CE444DEBF74C9`
- **Magic Code:** `0xB958D63A` (3109606970)
- **Message Types:** Init=-1, Get=0, GetResponse=1, Set=2, SetResponse=3, Notify=4, OMCI=5
- **84 Message IDs** with exact integer values (see REVERSE_ENGINEERING_REPORT.md)
- **Transport:** Raw Ethernet via Scapy (`Ether` + `LLC` from `scapy.layers.l2`)
- **Firmware version:** GDT v6.6, Firmware v9.1

### Phase 2: Live Testing (In Progress)

#### Network Topology Discovered
```
Linux Box (192.168.10.162, eth0)
  |
  v
CSS SW01 (SwOS, port2 + SFP1 uplink)
  |
  v
R01 (RouterOS, sfp-sfpplus1 from SW01, ether10 to SW02)
  |
  v
CRS354 SW02 (RouterOS, ether1 from R01, sfp-sfpplus1 = OLT SFP)
  |
  v (sfp-sfpplus1)
VOLT OLT SFP Stick (GPON, no IP address)
  |
  v (fiber)
ONU
  |
  v (ethernet)
MikroTik cAP (192.168.10.19) — this is NOT the OLT!
```

#### Key Discovery: 192.168.10.19 is NOT the OLT
- MAC `78:9a:18:59:9c:a3` is MikroTik OUI
- DHCP lease confirms `host-name=MikroTik`
- It's a cAP device behind the ONU, reachable through the GPON link
- The OLT SFP itself has NO IP address on the network

#### VLAN 4091 Setup (Configured)
Created VLAN 4091 path for L2 management traffic:
- **SW01 (SwOS):** VLAN 4091 added, port2 + SFP1 as members
- **R01:** VLAN filtering enabled on `main-office` bridge, VLAN 4091 tagged on `sfp-sfpplus1` + `ether10`, VLAN 1 untagged on all ports (preserves existing traffic)
- **SW02:** VLAN filtering enabled on `bridge`, VLAN 4091 tagged on `ether1`, untagged on `sfp-sfpplus1` (strips tag for OLT), VLAN 1 untagged on all ports, **HW offload disabled** on `sfp-sfpplus1` + `ether1`
- **Linux:** `eth0.4091` VLAN interface created

#### L2 Probing Results (All Failed)
Tried ~500+ packet combinations from the Linux box:
- All LLC DSAP/SSAP combinations (0x00, 0x04, 0xAA, 0xFE, 0xFF)
- All common EtherTypes (0x8800-0x89FF range, 0xA8C8, 0x88B5, etc.)
- IEEE 1904.2 (0xA8C8) and OAM (0x8809) protocols
- SNAP frames with various OUI/protocol IDs
- OAM multicast destinations (01:80:c2:00:00:02, 01:80:c2:00:00:0e)
- Broadcast and unicast to OLT MAC
- Both big-endian and little-endian magic code encoding
- UDP scan on all ports 1-1023 + common high ports
- TCP scan on 330 ports from same subnet
- ARP probes on 14 common default subnets (192.168.1.x, 192.168.0.x, 10.0.0.x, etc.)
- DHCP discover on VLAN 4091
- Promiscuous capture after port bounce

**Result:** OLT SFP responds to nothing. Zero packets captured from the OLT on any interface.

#### Hypothesis
The VOLT management protocol likely works at the **SFP electrical interface level**, not through the switch fabric. The management frames may need to be sent directly from a host NIC's SFP port to the OLT SFP, bypassing the switch entirely. The CRS354's switch ASIC (even with HW offload disabled and software bridging) may still not forward the specific frame type the OLT expects.

### Phase 2.5: VLAN 4090 Discovery (Tibit MCMS)

Juniper Unified PON / Tibit MCMS documentation revealed:
- Management VLAN is **4090** (not 4091)
- Protocol is **IEEE 1904.2** (EtherType `0xA8C8`)
- PON Controller interface config: `"interface": "eno1.4090"`
- The VOLT OLT SFP is likely a **Tibit MicroPlug OLT clone**
- See `TIBIT_MCMS_FINDINGS.md` for full details

#### VLAN 4090 Setup
Configured VLAN 4090 across all devices (same pattern as 4091).
Probed with IEEE 1904.2 frames — still no response through the multi-hop path.

#### RouterOS Upgrade Issue
- Upgraded SW02 from RouterOS 7.11 to latest
- **Broke sfp-sfpplus1 link** — auto-negotiation behavior changed
- **Fix:** `/interface/ethernet/set sfp-sfpplus1 auto-negotiation=no speed=1G-baseX`
- Link restored: `status: link-ok`, 1Gbps full-duplex, `sfp-rx-loss: no`
- Important: the OLT SFP requires **forced 1G-baseX, no auto-negotiation**

#### Results After Link Restore
- cAP at 192.168.10.19 not yet pingable (ONU re-registration pending)
- VLAN 4090 probes: 0 responses from OLT through multi-hop path
- 1 captured packet was a Docker container ARP, not from OLT

### Phase 2.6: Vendor & Video Research

#### Vendor Identified
- **Manufacturer:** AINOPOL (智慧光迅 / sczhgx.com), Sichuan, China
- **Model:** ZH-VOLT32 (also sold as HT-VOLT32 by other OEMs)
- **SFP Label:** `ZH-VOLT-16 (插拔型VOLT)` = "pluggable VOLT"
- **Ecosystem:** Core switch ZH-CS5228P-PWR, ONU ZH-F4P, Cloud AP ZH-AP30006S-M
- **Key spec:** SVLAN/CVLAN transparent transmission

#### Video Walkthrough Findings (Spanish tutorial, MikroTik Hex S + VOLT SFP)
A 33-minute video of someone configuring the exact same SFP OLT in a MikroTik confirmed:

1. **Discovery works through a MikroTik bridge** — SFP port + Ethernet ports bridged,
   Windows PC on one of the bridged ports, VOLT tool discovers OLT through the bridge.
2. **Interface selection by NIC MAC address** — Tool scans interfaces, user picks by MAC.
   Pure L2 discovery, no IP or VLAN needed.
3. **"OLT TX enable" is critical** — Must be toggled in the software on first use before
   the OLT communicates with ONUs. Maps to our RE: `OpticalTxEnable` (Message ID 256).
4. **Works through a bridge, not direct** — "The OLT doesn't necessarily have to show as
   connected to our computer" — the bridge forwards the management frames.
5. **Simple media converter works** — No special host device, just L2 connectivity.
6. **PPPoE setup on MikroTik** — Bridge created with SFP + Ethernet ports, PPPoE server
   on the bridge, NAT masquerade for internet, ONUs configured in PPPoE mode.
7. **ONU management via web UI** — ONUs accessed at 192.168.1.1, configured for PPPoE.
8. **Real-time monitoring** — Temperature, uptime, online/offline per ONU, remote
   activate/deactivate individual ONUs from the VOLT tool.

#### Key Insight: Why Our Probes Failed
The video shows the PC on the **same MikroTik** as the SFP — one bridge, one device.
Our setup has the Linux box going through SW01 → R01 → SW02 (three hops, two additional
switch fabrics). Tomorrow's direct patch into SW02 replicates the video's topology.

### Phase 3: Next Steps
1. **Patch Linux box directly into SW02** — same bridge as OLT SFP, no intermediate hops
2. Run tcpdump/Wireshark in promiscuous mode to capture all L2 traffic
3. Try our existing probe scripts (LLC, EtherType variants, VLAN 4090)
4. If probes work, build the Linux-native management tool
5. If probes still fail, run actual VOLT tool via Wine on MacBook (same SW02 bridge)
   while capturing on Linux to see exact frame format

## Files in This Repo

### Reverse Engineering Scripts
| File | Purpose |
|------|---------|
| `decrypt_modules.py` | First attempt at PyArmor decryption |
| `decrypt_v2.py` | Direct `pytransform.pyarmor()` call approach |
| `decrypt_v3.py` | Run actual PyArmor entry points |
| `decrypt_v4.py` | Module dict inspection after exec |
| `decrypt_v5.py` | `sys.settrace` approach (breakthrough) |
| `decrypt_v6.py` | Full trace with class method extraction |
| `decrypt_olt_socket.py` | Targeted olt_socket decryption with Crypto deps |
| `dump_bytecode.py` | Runtime bytecode dumper via exec hook |
| `protocol_capture.py` | Scapy hook for live packet capture |
| `trace_hook.py` | `sys.settrace` hook for function tracing |

### Network Probing Scripts
| File | Purpose |
|------|---------|
| `olt_probe.py` | Basic L2 probe (LLC + EtherType + SNAP) |
| `olt_probe2.py` | Extended probe with all EtherType combinations |
| `olt_probe3.py` | UDP scan + raw AF_PACKET socket probe |
| `olt_probe_vlan.py` | L2 probe via VLAN 4091 interface |
| `olt_ip_probe.py` | IP probe across 14 default subnets + DHCP |
| `vlan_setup.py` | VLAN 4091 configuration for R01 + SW02 + Linux |

## RouterOS Configuration Applied

### R01 (192.168.10.1)
- VLAN filtering: **enabled** on `main-office` bridge
- VLAN 4091: tagged on `sfp-sfpplus1`, `ether10`
- VLAN 1: untagged on all bridge ports + bridge interface
- `sfp-sfpplus1`, `ether10`: frame-types=admit-all

### SW02 (192.168.10.97)
- VLAN filtering: **enabled** on `bridge`
- VLAN 4091: tagged on `ether1`, untagged on `sfp-sfpplus1`
- VLAN 1: untagged on all 61 bridge ports + bridge interface
- `sfp-sfpplus1`, `ether1`: frame-types=admit-all, **hw-offload=false**

### SW01 (SwOS, 192.168.10.249)
- VLAN 4091 added in VLANs tab, port2 + SFP1 as members
- VLAN tab: unchanged (optional mode, any receive, PVID 1)

### To Revert
If needed, disable VLAN filtering on R01 and SW02:
```
# R01
/interface/bridge/set main-office vlan-filtering=no

# SW02
/interface/bridge/set bridge vlan-filtering=no
/interface/bridge/port/set [find interface=sfp-sfpplus1] hw=yes
/interface/bridge/port/set [find interface=ether1] hw=yes
```
