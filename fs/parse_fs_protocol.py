"""
FS PON Manager Protocol Decoder
Parses UDP 64219/64218 traffic and ARP OLT discovery
"""
import struct, sys, io
from collections import Counter, defaultdict

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

CMD_CODES = {
    0: 'None', 1: 'shake_hand', 2: 'ip_configuration', 3: 'cpe_white_list_send',
    4: 'cpe_white_list_read_qty', 5: 'ONU_WLIST_RPT', 6: 'cpe_illegal_CPE_report',
    7: 'cpe_alarm_report', 8: 'olt_alarm_report', 9: 'cpe_service_type_send',
    10: 'cpe_white_list_del', 11: 'cpe_opt_para_report', 12: 'cpe_sn_status',
    13: 'SERVICE_CONFIG_RPT', 66: 'Password_cmd', 67: 'Password_check_cmd',
    68: 'OLT_Update_BIN_cmd', 69: 'OLT_Reset_Master_cmd', 70: 'OLT_Reset_Slave_cmd',
    71: 'OLT_Softreset_cmd',
}

def mac_str(b): return ':'.join(f'{x:02x}' for x in b)
def ip_str(b): return '.'.join(str(x) for x in b)

def read_pcapng(filepath):
    packets = []
    with open(filepath, 'rb') as f:
        data = f.read()
    pos = 0
    while pos < len(data) - 8:
        block_type = struct.unpack_from('<I', data, pos)[0]
        block_len = struct.unpack_from('<I', data, pos + 4)[0]
        if block_len < 12 or pos + block_len > len(data): break
        if block_type == 6:
            cap_len = struct.unpack_from('<I', data, pos + 20)[0]
            packets.append(data[pos + 28: pos + 28 + cap_len])
        block_len = (block_len + 3) & ~3
        pos += block_len
    return packets

for pcap in ['fs-olt1.pcapng', 'fs-olt2.pcapng']:
    filepath = f'C:\\Users\\Vivek\\Downloads\\volt\\fs\\{pcap}'
    try:
        pkts = read_pcapng(filepath)
    except FileNotFoundError:
        continue

    print(f'\n{"="*70}')
    print(f'=== {pcap} ({len(pkts)} packets) ===')
    print(f'{"="*70}')

    etypes = Counter()
    olt_discovery = []
    udp_commands = []

    for p in pkts:
        if len(p) < 14: continue
        etype = struct.unpack_from('>H', p, 12)[0]
        etypes[etype] += 1

        # ARP packets
        if etype == 0x0806 and len(p) >= 42:
            opcode = struct.unpack_from('>H', p, 20)[0]
            sender_mac = mac_str(p[22:28])
            sender_ip = ip_str(p[28:32])
            target_ip = ip_str(p[38:42])
            extra = p[42:] if len(p) > 42 else b''
            try:
                code = extra.decode('ascii', 'ignore').strip('\x00')
            except:
                code = ''
            if 'OLT' in code:
                olt_discovery.append({
                    'opcode': opcode, 'sender_mac': sender_mac,
                    'sender_ip': sender_ip, 'target_ip': target_ip, 'code': code
                })

        # IP/UDP packets
        if etype == 0x0800 and len(p) > 42:
            proto = p[23]
            if proto == 17:  # UDP
                src_ip = ip_str(p[26:30])
                dst_ip = ip_str(p[30:34])
                sport = struct.unpack_from('>H', p, 34)[0]
                dport = struct.unpack_from('>H', p, 36)[0]
                udp_len = struct.unpack_from('>H', p, 38)[0]
                udp_payload = p[42:]

                if sport in [64218, 64219] or dport in [64218, 64219]:
                    cmd = udp_payload[0] if len(udp_payload) > 0 else -1
                    seq = struct.unpack_from('>H', udp_payload, 1)[0] if len(udp_payload) > 2 else 0
                    direction = 'REQ' if dport == 64219 else 'RSP'
                    udp_commands.append({
                        'direction': direction, 'cmd': cmd, 'seq': seq,
                        'src_ip': src_ip, 'dst_ip': dst_ip,
                        'sport': sport, 'dport': dport,
                        'payload': udp_payload,
                        'src_mac': mac_str(p[6:12]),
                        'dst_mac': mac_str(p[0:6]),
                    })

    print(f'\nEtherType distribution:')
    for et, count in etypes.most_common(10):
        print(f'  0x{et:04X}: {count}')

    # OLT Discovery
    print(f'\n--- OLT Discovery (ARP with "OLT" marker): {len(olt_discovery)} ---')
    seen_olts = {}
    for d in olt_discovery:
        key = d['sender_ip']
        if key not in seen_olts:
            seen_olts[key] = d
            print(f'  OLT: IP={d["sender_ip"]} MAC={d["sender_mac"]} op={d["opcode"]}')

    # UDP Commands
    print(f'\n--- UDP Commands: {len(udp_commands)} ---')
    cmd_counter = Counter()
    direction_counter = Counter()
    olt_ips = set()
    pc_ips = set()
    for c in udp_commands:
        cmd_counter[c['cmd']] += 1
        direction_counter[c['direction']] += 1
        if c['direction'] == 'RSP':
            olt_ips.add(c['src_ip'])
            pc_ips.add(c['dst_ip'])
        else:
            pc_ips.add(c['src_ip'])
            olt_ips.add(c['dst_ip'])

    print(f'  Requests: {direction_counter["REQ"]}, Responses: {direction_counter["RSP"]}')
    print(f'  PC IPs: {pc_ips}')
    print(f'  OLT IPs: {olt_ips}')
    print(f'\n  Command distribution:')
    for cmd, count in cmd_counter.most_common():
        name = CMD_CODES.get(cmd, f'unknown_{cmd}')
        print(f'    cmd={cmd:3d} ({name:30s}): {count}')

    # Show first 30 command exchanges
    print(f'\n--- First 40 command packets ---')
    for i, c in enumerate(udp_commands[:40]):
        name = CMD_CODES.get(c['cmd'], f'cmd_{c["cmd"]}')
        arrow = '>>>' if c['direction'] == 'REQ' else '<<<'
        payload_hex = c['payload'][:30].hex()
        extra = ''
        if c['cmd'] == 1 and c['direction'] == 'RSP' and len(c['payload']) > 20:
            sn = c['payload'][20:].decode('ascii', 'ignore').strip('\x00')
            extra = f' SN={sn}'
        elif c['cmd'] == 12 and len(c['payload']) > 3:
            extra = f' data={c["payload"][3:20].hex()}'
        elif c['cmd'] == 66 and len(c['payload']) > 3:
            extra = f' auth_data={c["payload"][3:20].hex()}'
        print(f'  [{i:3d}] {arrow} {c["src_ip"]:>15s}:{c["sport"]} -> {c["dst_ip"]:>15s}:{c["dport"]} {name}{extra}')
        if i < 10:
            print(f'        payload: {payload_hex}')

    # Analyze shake_hand responses to get OLT details
    print(f'\n--- OLT Details (from shake_hand responses) ---')
    for c in udp_commands:
        if c['cmd'] == 1 and c['direction'] == 'RSP' and len(c['payload']) > 20:
            mac_bytes = c['payload'][4:10]
            olt_mac = ':'.join(f'{b:02x}' for b in mac_bytes)
            ip_bytes = c['payload'][10:14]
            olt_ip = ip_str(ip_bytes)
            sn = c['payload'][20:].decode('ascii', 'ignore').strip('\x00')
            print(f'  OLT: MAC={olt_mac} IP={olt_ip} SN={sn}')
            break
