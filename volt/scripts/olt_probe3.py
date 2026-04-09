import sys, struct, socket, time
sys.path.insert(0, "/home/vivek/.local/lib/python3.10/site-packages")
from scapy.all import *

OLT_IP = "192.168.10.19"
OLT_MAC = "78:9a:18:59:9c:a3"
BCAST = "ff:ff:ff:ff:ff:ff"
MY_MAC = get_if_hwaddr("eth0")
MAGIC = 0xB958D63A

print(f"=== UDP SCAN FROM LOCAL SUBNET ===")
# Full UDP scan from same subnet
open_udp = []
for port in list(range(1, 200)) + list(range(4000, 4100)) + list(range(5000, 5100)) + \
            list(range(8000, 8100)) + list(range(10000, 10020)) + [161, 162, 514, 1234, 9999, 20000]:
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    s.settimeout(0.2)
    try:
        # Send magic code as UDP payload
        s.sendto(struct.pack(">I", MAGIC) + b"\xff\xff\xff\xff" + b"\x00" * 32, (OLT_IP, port))
        data, addr = s.recvfrom(4096)
        open_udp.append((port, data))
        print(f"  UDP {port}: {data[:50].hex()}")
    except:
        pass
    finally:
        s.close()

if not open_udp:
    print("  No UDP responses")

print(f"\n=== RAW SOCKET APPROACH ===")
# Try using raw AF_PACKET socket directly (bypasses any scapy issues)
import fcntl
ETH_P_ALL = 0x0003

# Create raw socket
rawsock = socket.socket(socket.AF_PACKET, socket.SOCK_RAW, socket.htons(ETH_P_ALL))
rawsock.bind(("eth0", 0))
rawsock.settimeout(3)

# Construct frames manually with different EtherTypes and structures
dst_bytes = bytes.fromhex(OLT_MAC.replace(":", ""))
src_bytes = bytes.fromhex(MY_MAC.replace(":", ""))
bcast_bytes = b"\xff\xff\xff\xff\xff\xff"
magic_bytes = struct.pack(">I", MAGIC)

# Init message: magic + type(-1) + id(-1)
init_payload = magic_bytes + struct.pack(">hh", -1, -1) + b"\x00" * 40

# Try many EtherType values systematically
print("Trying all EtherTypes 0x8800-0x89FF from raw socket...")
responses = []
for etype in range(0x8800, 0x8A00):
    frame = dst_bytes + src_bytes + struct.pack(">H", etype) + init_payload
    rawsock.send(frame)

# Also try broadcast
for etype in range(0x8800, 0x8A00):
    frame = bcast_bytes + src_bytes + struct.pack(">H", etype) + init_payload
    rawsock.send(frame)

# Also try 802.3 frames (length < 0x0600)
for length_val in [48, 52, 56, 64]:
    # 802.3: dst + src + length + LLC(DSAP/SSAP/ctrl) + payload
    for dsap in [0x00, 0x42, 0xAA, 0xFE, 0xFF]:
        llc = bytes([dsap, dsap, 0x03])
        frame = dst_bytes + src_bytes + struct.pack(">H", length_val) + llc + init_payload
        rawsock.send(frame)
        frame = bcast_bytes + src_bytes + struct.pack(">H", length_val) + llc + init_payload
        rawsock.send(frame)

# Now listen for any response from OLT
print("Listening for 5 seconds...")
end_time = time.time() + 5
while time.time() < end_time:
    try:
        data = rawsock.recv(65535)
        src_mac = data[6:12].hex()
        if src_mac == OLT_MAC.replace(":", "").lower():
            etype = struct.unpack(">H", data[12:14])[0]
            print(f"\n*** OLT RESPONSE! ***")
            print(f"  EtherType: 0x{etype:04X}")
            print(f"  Length: {len(data)}")
            print(f"  Full hex: {data.hex()}")
            responses.append(data)
    except socket.timeout:
        break
    except BlockingIOError:
        time.sleep(0.01)

rawsock.close()

if not responses:
    print("No responses from OLT to any probes")
else:
    print(f"\n{len(responses)} responses captured!")
