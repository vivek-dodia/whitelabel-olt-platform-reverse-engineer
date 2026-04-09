import sys, struct
sys.path.insert(0, "/home/vivek/.local/lib/python3.10/site-packages")
from scapy.all import *

OLT_MAC = "78:9a:18:59:9c:a3"
BCAST = "ff:ff:ff:ff:ff:ff"
MY_MAC = get_if_hwaddr("eth0")
MAGIC = 0xB958D63A

print(f"Magic: 0x{MAGIC:08X}, My MAC: {MY_MAC}")

# The key insight: magic 0xB958 could be the EtherType
# Try 0xB958 as EtherType with D63A + init payload
candidates = []

# EtherType = 0xB958, payload starts with remaining magic bytes
for etype in [0xB958, 0xD63A, 0x58B9, 0x3AD6]:
    for payload_prefix in [
        struct.pack(">H", MAGIC & 0xFFFF),   # D63A
        struct.pack(">H", MAGIC >> 16),       # B958
        struct.pack(">I", MAGIC),             # full magic
        b"",                                   # no prefix
    ]:
        init_data = payload_prefix + struct.pack(">hh", -1, -1) + b"\x00" * 40
        for dst in [OLT_MAC, BCAST]:
            pkt = Ether(dst=dst, src=MY_MAC, type=etype) / Raw(init_data)
            candidates.append((f"etype=0x{etype:04X} prefix={payload_prefix.hex()} dst={dst}", pkt))

# Also try with LLC where the LENGTH field encodes packet size (802.3 style)
# and magic is first 4 bytes of payload after LLC header
for dsap in [0x00, 0xAA, 0xFF]:
    for ctrl in [0x03, 0x00, 0xFF]:
        init_data = struct.pack(">I", MAGIC) + struct.pack(">hh", -1, -1) + b"\x00" * 40
        for dst in [OLT_MAC, BCAST]:
            pkt = Ether(dst=dst, src=MY_MAC) / LLC(dsap=dsap, ssap=dsap, ctrl=ctrl) / Raw(init_data)
            candidates.append((f"LLC dsap=0x{dsap:02x} ctrl=0x{ctrl:02x} dst={dst}", pkt))

# Try with magic as BOTH EtherType AND in payload
init_data = struct.pack(">I", MAGIC) + struct.pack(">bb", -1, -1) + struct.pack(">h", -1) + b"\x00" * 40
pkt = Ether(dst=OLT_MAC, src=MY_MAC, type=0xB958) / Raw(init_data)
candidates.append(("etype=0xB958 + full_magic + byte_init", pkt))

# Try with no LLC, just raw Ether with length < 1500 (802.3 frame)
init_data = struct.pack(">I", MAGIC) + struct.pack(">hh", -1, -1) + b"\x00" * 32
pkt = Ether(dst=OLT_MAC, src=MY_MAC) / Raw(init_data)
candidates.append(("raw 802.3 + magic", pkt))
pkt2 = Ether(dst=BCAST, src=MY_MAC) / Raw(init_data)
candidates.append(("raw 802.3 bcast + magic", pkt2))

print(f"Testing {len(candidates)} packet variants...")
for desc, pkt in candidates:
    ans = srp(pkt, iface="eth0", timeout=0.3, verbose=0)
    if ans[0]:
        for s, r in ans[0]:
            raw = bytes(r)
            print(f"RESPONSE to {desc}")
            print(f"  {raw[:100].hex()}")
            print(f"  {r.summary()}")

print("\nDone probing. Now doing full promiscuous capture for 10s...")
# Send ALL variants rapidly then listen
for desc, pkt in candidates:
    sendp(pkt, iface="eth0", verbose=0)

pkts = sniff(iface="eth0", timeout=10,
    lfilter=lambda p: hasattr(p, "src") and (p.src == OLT_MAC) and not p.haslayer(ARP))
print(f"Captured {len(pkts)} packets from OLT")
for p in pkts:
    raw = bytes(p)
    etype = struct.unpack(">H", raw[12:14])[0]
    print(f"  type=0x{etype:04X} len={len(raw)} : {raw[:80].hex()}")
