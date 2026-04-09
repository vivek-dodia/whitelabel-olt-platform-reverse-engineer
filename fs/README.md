# FS.com OLT SFP Stick — Reverse Engineering

Reverse engineering of the FS.com GPON OLT SFP stick (GPON-SFP-OLT-MAC-I) and FS PON Manager V1.0.0.

**Vendor:** FS.com
**Model:** GPON-SFP-OLT-MAC-I (128 ONU, Class C+, 20km)
**Management:** Windows app "FS PON Manager V1.0.0", IP-based (ARP discovery with `code=OLT`)

## Key Differences from VOLT

| Feature | VOLT (Ainopol) | FS.com |
|---------|---------------|--------|
| Transport | Raw L2 (EtherType 0x88B6) | IP-based (ARP + TCP/UDP) |
| Discovery | L2 broadcast | ARP with `code=OLT` |
| OLT addressing | MAC only | IP address (192.168.15.x) |
| Max ONUs | 32 | 128 |
| Class | B+ | C+ |
| Uplink | SGMII 1G | SGMII/HSGMII 2.5G |
| Switch compatibility | Media converter only | Should work through switches |

## Status

Reverse engineering in progress.
