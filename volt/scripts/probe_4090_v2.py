import sys, struct, time, threading
sys.path.insert(0, "/home/vivek/.local/lib/python3.10/site-packages")
from scapy.all import *

IFACE = "eth0.4090"
MY_MAC = get_if_hwaddr("eth0")
BCAST = "ff:ff:ff:ff:ff:ff"
MAGIC = 0xB958D63A

print(f"Interface: {IFACE}, MAC: {MY_MAC}")

captured = []

def sniffer():
    pkts = sniff(iface=IFACE, timeout=20)
    captured.extend(pkts)

t = threading.Thread(target=sniffer)
t.start()
time.sleep(1)

# IEEE 1904.2 (EtherType 0xA8C8)
print("[1] IEEE 1904.2 (0xA8C8)...")
for dst in [BCAST, "01:80:c2:00:00:02", "01:80:c2:00:00:0e"]:
    sendp(Ether(dst=dst, src=MY_MAC, type=0xA8C8)/Raw(b"\x00" * 64), iface=IFACE, verbose=0)
    sendp(Ether(dst=dst, src=MY_MAC, type=0xA8C8)/Raw(struct.pack(">I", MAGIC) + b"\x00" * 60), iface=IFACE, verbose=0)

# OAM (0x8809)
print("[2] OAM (0x8809)...")
for dst in [BCAST, "01:80:c2:00:00:02"]:
    sendp(Ether(dst=dst, src=MY_MAC, type=0x8809)/Raw(b"\x03\x00" + b"\x00" * 62), iface=IFACE, verbose=0)

# LLC + magic
print("[3] LLC + magic...")
for dsap in [0x00, 0xAA]:
    payload = struct.pack(">I", MAGIC) + struct.pack(">hh", -1, -1) + b"\x00" * 40
    sendp(Ether(dst=BCAST, src=MY_MAC)/LLC(dsap=dsap, ssap=dsap, ctrl=3)/Raw(payload), iface=IFACE, verbose=0)

# ARP scan
print("[4] ARP scan...")
for subnet in ["192.168.0", "192.168.1", "10.0.0", "172.16.0", "192.168.100", "10.10.10"]:
    ans = srp(Ether(dst=BCAST)/ARP(pdst=subnet + ".0/24"), iface=IFACE, timeout=2, verbose=0)
    for s, r in ans[0]:
        print(f"  *** ARP: {r[ARP].psrc} = {r[ARP].hwsrc} ***")

# DHCP
print("[5] DHCP discover...")
dhcp_pkt = (Ether(dst=BCAST, src=MY_MAC) /
    IP(src="0.0.0.0", dst="255.255.255.255") /
    UDP(sport=68, dport=67) /
    BOOTP(chaddr=bytes.fromhex(MY_MAC.replace(":", "")), xid=0xDEADBEEF) /
    DHCP(options=[("message-type", "discover"), "end"]))
ans = srp(dhcp_pkt, iface=IFACE, timeout=5, verbose=0)
for s, r in ans[0]:
    print(f"  *** DHCP: {r.summary()} ***")

print("\nWaiting for sniffer...")
t.join()

print(f"\nCaptured {len(captured)} packets on VLAN 4090:")
for p in captured[:30]:
    raw = bytes(p)
    etype = struct.unpack(">H", raw[12:14])[0] if len(raw) > 14 else 0
    src = p.src if hasattr(p, "src") else "?"
    if src != MY_MAC:
        print(f"  src={src} dst={p.dst} type=0x{etype:04X} len={len(raw)}")
        print(f"    {raw[:80].hex()}")

own_count = sum(1 for p in captured if hasattr(p, "src") and p.src == MY_MAC)
other_count = len(captured) - own_count
print(f"\nOwn packets: {own_count}, Other: {other_count}")
