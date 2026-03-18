import struct, sys, io
from collections import Counter

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

def mac_str(b):
    return ':'.join(f'{x:02x}' for x in b)

def read_pcapng(filepath):
    packets = []
    with open(filepath, 'rb') as f:
        data = f.read()
    pos = 0
    while pos < len(data) - 8:
        block_type = struct.unpack_from('<I', data, pos)[0]
        block_len = struct.unpack_from('<I', data, pos + 4)[0]
        if block_len < 12 or pos + block_len > len(data):
            break
        if block_type == 6:  # Enhanced Packet Block
            cap_len = struct.unpack_from('<I', data, pos + 20)[0]
            pkt_data = data[pos + 28: pos + 28 + cap_len]
            packets.append(pkt_data)
        block_len = (block_len + 3) & ~3
        pos += block_len
    return packets

for pcap_file in ['volt.pcapng', 'volt2.pcapng']:
    filepath = f'C:\\Users\\Vivek\\Downloads\\volt\\pcap\\{pcap_file}'
    print(f'\n{"="*70}')
    print(f'=== {pcap_file} ===')
    print(f'{"="*70}')

    pkts = read_pcapng(filepath)
    print(f'Total packets: {len(pkts)}')

    etypes = Counter()
    volt_pkts = []
    for p in pkts:
        if len(p) >= 14:
            etype = struct.unpack_from('>H', p, 12)[0]
            etypes[etype] += 1
            if etype == 0x88B6:
                volt_pkts.append(p)

    print(f'\nEtherType distribution:')
    for et, count in etypes.most_common(20):
        print(f'  0x{et:04X}: {count} packets')

    print(f'\n--- VOLT Protocol (0x88B6): {len(volt_pkts)} packets ---')

    # Detailed analysis of first 10 packets
    for i, p in enumerate(volt_pkts[:10]):
        dst = mac_str(p[0:6])
        src = mac_str(p[6:12])
        payload = p[14:]

        print(f'\n  Packet {i}:')
        print(f'    DST: {dst}')
        print(f'    SRC: {src}')
        print(f'    Payload ({len(payload)} bytes): {payload.hex()}')

        # Field-by-field decode
        if len(payload) >= 4:
            val = struct.unpack_from('>I', payload, 0)[0]
            print(f'    [0:4]  0x{val:08X}  {"<-- MAGIC" if val == 0xB958D63A else ""}')
        for off in range(4, min(len(payload), 32), 2):
            if off + 2 <= len(payload):
                val16 = struct.unpack_from('>H', payload, off)[0]
                val16s = struct.unpack_from('>h', payload, off)[0]
                extra = ""
                if val16s == -1:
                    extra = " <-- Init (-1)"
                print(f'    [{off}:{off+2}]  0x{val16:04X} (signed: {val16s}){extra}')

    # Variation analysis
    if volt_pkts:
        print(f'\n--- Variation Analysis ---')
        unique_dsts = set()
        unique_srcs = set()
        unique_payloads = {}
        for p in volt_pkts:
            unique_dsts.add(mac_str(p[0:6]))
            unique_srcs.add(mac_str(p[6:12]))
            ph = p[14:].hex()
            if ph not in unique_payloads:
                unique_payloads[ph] = 0
            unique_payloads[ph] += 1

        print(f'  Unique DST MACs: {len(unique_dsts)}')
        for d in sorted(unique_dsts):
            print(f'    {d}')
        print(f'  Unique SRC MACs: {len(unique_srcs)}')
        for s in sorted(unique_srcs):
            print(f'    {s}')
        print(f'  Unique payload patterns: {len(unique_payloads)}')
        for j, (ph, count) in enumerate(sorted(unique_payloads.items(), key=lambda x: -x[1])):
            print(f'    [{j}] count={count}: {ph[:120]}{"..." if len(ph) > 120 else ""}')
            # Decode structure
            pb = bytes.fromhex(ph)
            if len(pb) >= 8:
                magic = struct.unpack_from('>I', pb, 0)[0]
                b4 = pb[4]
                b5 = pb[5]
                w6 = struct.unpack_from('>H', pb, 6)[0] if len(pb) >= 8 else 0
                print(f'         magic=0x{magic:08X} byte4=0x{b4:02X} byte5=0x{b5:02X} word6=0x{w6:04X}')
