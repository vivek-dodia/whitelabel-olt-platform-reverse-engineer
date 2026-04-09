import sys, struct
sys.path.insert(0, "/home/vivek/.local/lib/python3.10/site-packages")
from scapy.all import *

IFACE = "eth0.4091"
BCAST = "ff:ff:ff:ff:ff:ff"
MY_MAC = get_if_hwaddr("eth0")
MAGIC = 0xB958D63A

print(f"Interface: {IFACE}")
print(f"Magic: 0x{MAGIC:08X}, My MAC: {MY_MAC}")

results = []

# LLC + magic payload (various DSAP/SSAP)
print("\n[1] LLC probes...")
for dsap, ssap in [(0xAA, 0xAA), (0x00, 0x00), (0x04, 0x04), (0xFE, 0xFE)]:
    for endian in [">", "<"]:
        payload = struct.pack(endian + "I", MAGIC) + struct.pack(endian + "hh", -1, -1) + b"\x00" * 40
        for dst in [BCAST]:
            pkt = Ether(dst=dst, src=MY_MAC) / LLC(dsap=dsap, ssap=ssap, ctrl=0x03) / Raw(payload)
            ans = srp(pkt, iface=IFACE, timeout=1, verbose=0)
            if ans[0]:
                for s, r in ans[0]:
                    raw = bytes(r)
                    results.append(f"LLC dsap=0x{dsap:02x} endian={endian}: {raw[:100].hex()}")

# Raw EtherTypes
print("[2] EtherType probes...")
for etype in [0xA8C8, 0x8809, 0x88B5, 0x88B6, 0x789A, 0xB958, 0xD63A, 0x7788, 0x9A78, 0xAAAA, 0x0801, 0x8808]:
    payload = struct.pack(">I", MAGIC) + struct.pack(">hh", -1, -1) + b"\x00" * 40
    pkt = Ether(dst=BCAST, src=MY_MAC, type=etype) / Raw(payload)
    ans = srp(pkt, iface=IFACE, timeout=0.5, verbose=0)
    if ans[0]:
        for s, r in ans[0]:
            raw = bytes(r)
            results.append(f"EtherType 0x{etype:04x}: {raw[:100].hex()}")

# OAM multicast destinations
print("[3] OAM multicast probes...")
for dst in ['01:80:c2:00:00:02', '01:80:c2:00:00:0e', BCAST]:
    for etype in [0xA8C8, 0x8809]:
        pkt = Ether(dst=dst, src=MY_MAC, type=etype) / Raw(b"\x00" * 48)
        ans = srp(pkt, iface=IFACE, timeout=0.5, verbose=0)
        if ans[0]:
            for s, r in ans[0]:
                raw = bytes(r)
                results.append(f"OAM dst={dst} etype=0x{etype:04x}: {raw[:100].hex()}")

if results:
    print("\n=== RESPONSES ===")
    for r in results:
        print(r)
else:
    print("\nNo responses to targeted probes")

# Broad promiscuous capture
print("\n[4] Promiscuous capture on eth0.4091 for 10s...")
# Send a burst of probes then listen
for etype in range(0x8800, 0x8A00):
    sendp(Ether(dst=BCAST, src=MY_MAC, type=etype) / Raw(struct.pack(">I", MAGIC) + b"\x00" * 44), iface=IFACE, verbose=0)

pkts = sniff(iface=IFACE, timeout=10)
print(f"Captured {len(pkts)} total packets on VLAN 4091")
for p in pkts[:20]:
    raw = bytes(p)
    etype = struct.unpack(">H", raw[12:14])[0]
    print(f"  src={p.src} dst={p.dst} type=0x{etype:04X} len={len(raw)}")
    print(f"    {raw[:60].hex()}")
