import sys, struct
sys.path.insert(0, "/home/vivek/.local/lib/python3.10/site-packages")
from scapy.all import *

OLT_MAC = "78:9a:18:59:9c:a3"
BCAST = "ff:ff:ff:ff:ff:ff"
MY_MAC = get_if_hwaddr("eth0")
MAGIC = 3109606970  # 0xB95A1DCA

print(f"Magic: 0x{MAGIC:08X}, My MAC: {MY_MAC}")

results = []

# Try LLC + magic payload
for dsap, ssap in [(0xAA, 0xAA), (0x00, 0x00), (0x04, 0x04), (0xFE, 0xFE)]:
    for endian in [">", "<"]:
        payload = struct.pack(endian + "I", MAGIC) + struct.pack(endian + "hh", -1, -1) + b"\x00" * 40
        for dst in [OLT_MAC, BCAST]:
            pkt = Ether(dst=dst, src=MY_MAC) / LLC(dsap=dsap, ssap=ssap, ctrl=0x03) / Raw(payload)
            ans = srp(pkt, iface="eth0", timeout=0.5, verbose=0)
            if ans[0]:
                for s, r in ans[0]:
                    raw = bytes(r)
                    results.append(f"LLC dsap=0x{dsap:02x} endian={endian} dst={dst}: {raw[:100].hex()}")

# Try raw EtherTypes
for etype in [0x88B5, 0x789A, 0xB95A, 0x1DCA, 0x7788, 0x9A78, 0xAAAA, 0x0801]:
    payload = struct.pack(">I", MAGIC) + struct.pack(">hh", -1, -1) + b"\x00" * 40
    pkt = Ether(dst=OLT_MAC, src=MY_MAC, type=etype) / Raw(payload)
    ans = srp(pkt, iface="eth0", timeout=0.3, verbose=0)
    if ans[0]:
        for s, r in ans[0]:
            raw = bytes(r)
            results.append(f"EtherType 0x{etype:04x}: {raw[:100].hex()}")

# Try SNAP frames
for code in [0x1DCA, 0x789A, 0x0000, 0xB95A]:
    payload = struct.pack(">I", MAGIC) + struct.pack(">hh", -1, -1) + b"\x00" * 40
    pkt = Ether(dst=OLT_MAC, src=MY_MAC) / LLC(dsap=0xAA, ssap=0xAA, ctrl=3) / SNAP(OUI=0, code=code) / Raw(payload)
    ans = srp(pkt, iface="eth0", timeout=0.3, verbose=0)
    if ans[0]:
        for s, r in ans[0]:
            raw = bytes(r)
            results.append(f"SNAP code=0x{code:04x}: {raw[:100].hex()}")

if results:
    print("\n=== RESPONSES ===")
    for r in results:
        print(r)
else:
    print("\nNo responses to LLC/EtherType/SNAP probes")

# Sniff ALL non-IP traffic from OLT
print("\nSniffing non-IP from OLT for 3s...")
import os
os.system("ping -c 1 -W 1 192.168.10.19 > /dev/null 2>&1 &")
pkts = sniff(iface="eth0", timeout=3,
    lfilter=lambda p: hasattr(p, "src") and p.src == OLT_MAC and not p.haslayer(IP) and not p.haslayer(ARP))
print(f"Non-IP from OLT: {len(pkts)}")
for p in pkts:
    raw = bytes(p)
    print(f"  {raw[:80].hex()}")
