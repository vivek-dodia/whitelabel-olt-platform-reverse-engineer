# Tibit MCMS / Juniper Unified PON — Key Findings

## The VOLT OLT SFP is likely a Tibit MicroPlug OLT (or clone)

The Juniper "MicroClimate Management System" (MCMS) documentation reveals the exact
management architecture used by these OLT SFP sticks:

### Critical Discovery: Management VLAN 4090

From `PonCntlInit.json`:
```json
{
    "interface": "eno1.4090"
}
```

The PON Controller communicates with OLT devices on **VLAN 4090** using a Linux VLAN
subinterface (e.g., `enp0s8.4090`). This is the management plane VLAN.

### Protocol: IEEE 1904.2 (L2)

> "The MCMS PON Controller uses IEEE 1904.2 packets (L2) to communicate with the
> OLT and ONU devices."

- **Protocol:** IEEE 1904.2 (Standard for Control and Management of EPON)
- **EtherType:** 0xA8C8 (IEEE 1904.2)
- **Transport:** Layer 2 Ethernet, NOT IP-based
- **VLAN:** 4090 (management VLAN)

### Architecture

```
MCMS PON Manager (Angular Web UI, Django REST API)
        |
        v (MongoDB)
MCMS PON Controller (L2 device driver, IEEE 1904.2)
        |
        v (Ethernet VLAN 4090, EtherType 0xA8C8)
MicroPlug OLT (SFP form factor)
        |
        v (GPON/XGS-PON fiber)
MicroPlug ONU / Third-party ONUs
```

### MCMS Stack Components
1. **PON Manager** — Angular web app + Django REST API (Apache2, port 80/443)
2. **PON Controller** — Stateless L2 device driver (IEEE 1904.2 packets)
3. **MongoDB** — Central datastore for config, state, stats, alarms
4. **Netconf Server** — YANG models (BBF TR-383, TR-385) via Netopeer2/Sysrepo

### Tibit-specific Details
- Database: `tibit_pon_controller`
- Users DB: `tibit_users`
- Default users: `pdmPonController`, `pdmPonManager`, `pdmNetconf`, `pdmUserAdmin`
- Default password: `pdmPass`
- Install path: `/opt/tibit/poncntl/`, `/etc/tibit/poncntl/`
- Log path: `/var/log/tibit/`
- Service: `tibit-poncntl.service`
- Virtual interface pair: `tibitvirteap`, `tibitvirtumt` (for 802.1X auth)
- Supports DHCPv4 and DHCPv6 relay
- Supports 802.1X authentication engine

### Scaling
- 1 PON Controller instance = 3072 subscribers (48 OLTs x 64 ONUs)
- Requires <=5ms round-trip latency to OLTs
- 100K subscribers = 32 PON Controller instances

### Next Steps
1. Try VLAN **4090** instead of 4091 — this is the standard Tibit management VLAN
2. Send IEEE 1904.2 frames (EtherType 0xA8C8) on VLAN 4090
3. If VOLT OLT is truly a Tibit clone, it should respond on this VLAN
4. Consider deploying the actual Tibit MCMS PON Controller (it's a .deb package)
   to manage the OLT natively
