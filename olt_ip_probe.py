import sys, struct, socket, subprocess, os, threading, time
sys.path.insert(0, "/home/vivek/.local/lib/python3.10/site-packages")
from scapy.all import *

IFACE = "eth0.4091"
IFACE_RAW = "eth0"
MY_MAC = get_if_hwaddr("eth0")
BCAST = "ff:ff:ff:ff:ff:ff"

captured = []

def sniffer_vlan():
    """Sniff VLAN 4091 for anything"""
    pkts = sniff(iface=IFACE, timeout=30)
    for p in pkts:
        captured.append(("vlan4091", p))

def sniffer_raw():
    """Sniff eth0 for non-IP from unknown sources"""
    known_macs = {MY_MAC.lower(), "48:a9:8a:ae:44:52", "80:cc:9c:32:86:a4",
                  "78:9a:18:16:fc:09", "78:9a:18:e1:80:a0", "78:9a:18:59:9c:a3"}
    pkts = sniff(iface=IFACE_RAW, timeout=30,
        lfilter=lambda p: hasattr(p, "src") and p.src.lower() not in known_macs
                          and not p.haslayer(IP) and not p.haslayer(ARP) and not p.haslayer(IPv6))
    for p in pkts:
        captured.append(("eth0_unknown", p))

# Start sniffers
print("Starting sniffers on eth0 and eth0.4091 (30s)...")
t1 = threading.Thread(target=sniffer_vlan)
t2 = threading.Thread(target=sniffer_raw)
t1.start()
t2.start()
time.sleep(2)

# Assign IPs on eth0.4091 and try common OLT default subnets
default_ips = [
    # (our_ip, target_ip, subnet)
    ("192.168.1.2", "192.168.1.1", "24"),
    ("192.168.1.100", "192.168.1.1", "24"),
    ("192.168.0.2", "192.168.0.1", "24"),
    ("192.168.0.100", "192.168.0.100", "24"),
    ("192.168.100.2", "192.168.100.1", "24"),
    ("10.0.0.2", "10.0.0.1", "24"),
    ("10.10.10.2", "10.10.10.1", "24"),
    ("172.16.0.2", "172.16.0.1", "24"),
    ("192.168.2.2", "192.168.2.1", "24"),
    ("192.168.10.2", "192.168.10.1", "24"),
    ("10.0.1.2", "10.0.1.1", "24"),
    ("10.1.1.2", "10.1.1.1", "24"),
    ("192.168.11.2", "192.168.11.1", "24"),
    ("100.64.0.2", "100.64.0.1", "24"),
]

for our_ip, target_ip, mask in default_ips:
    # Set IP on VLAN interface
    os.system(f"sudo ip addr flush dev {IFACE} 2>/dev/null")
    os.system(f"sudo ip addr add {our_ip}/{mask} dev {IFACE}")
    time.sleep(0.3)

    # ARP probe
    ans = srp(Ether(dst=BCAST)/ARP(pdst=target_ip, psrc=our_ip), iface=IFACE, timeout=0.5, verbose=0)
    if ans[0]:
        for s, r in ans[0]:
            print(f"*** ARP REPLY from {target_ip}! MAC={r[ARP].hwsrc} ***")

    # Also try pinging
    result = subprocess.run(["ping", "-c", "1", "-W", "1", "-I", IFACE, target_ip],
                          capture_output=True, text=True)
    if result.returncode == 0:
        print(f"*** PING {target_ip} RESPONDED! ***")

    # TCP connect to common ports
    for port in [80, 443, 22, 23, 8080]:
        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        s.settimeout(0.3)
        try:
            s.bind((our_ip, 0))
            s.connect((target_ip, port))
            print(f"*** TCP {target_ip}:{port} OPEN! ***")
        except:
            pass
        finally:
            s.close()

print("\nTrying DHCP discover on VLAN 4091...")
os.system(f"sudo ip addr flush dev {IFACE} 2>/dev/null")
# Send DHCP discover
dhcp_discover = (
    Ether(dst=BCAST, src=MY_MAC) /
    IP(src="0.0.0.0", dst="255.255.255.255") /
    UDP(sport=68, dport=67) /
    BOOTP(chaddr=bytes.fromhex(MY_MAC.replace(":", "")), xid=0x12345678) /
    DHCP(options=[("message-type", "discover"), "end"])
)
ans = srp(dhcp_discover, iface=IFACE, timeout=5, verbose=0)
if ans[0]:
    for s, r in ans[0]:
        print(f"*** DHCP OFFER received! ***")
        print(f"    {r.summary()}")

# Also send on eth0 directly (no VLAN)
print("\nAlso trying DHCP on eth0 directly...")
ans2 = srp(dhcp_discover, iface=IFACE_RAW, timeout=3, verbose=0)
if ans2[0]:
    for s, r in ans2[0]:
        if r[ARP] if r.haslayer(ARP) else True:
            print(f"*** DHCP OFFER on eth0! {r.summary()} ***")

# Wait for sniffers
print("\nWaiting for sniffers to finish...")
t1.join()
t2.join()

print(f"\nTotal captured: {len(captured)} packets")
for iface_name, p in captured[:30]:
    raw = bytes(p)
    print(f"  [{iface_name}] src={p.src} dst={p.dst} len={len(raw)}")
    print(f"    {raw[:60].hex()}")
