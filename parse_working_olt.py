"""
Parse the working OLT capture — both requests AND responses!
This is the first capture with a functioning OLT SFP stick.
"""
import struct, sys, io
from collections import Counter, defaultdict

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

MSG_IDS = {
    -1: 'Init', 0: 'OltVersion', 1: 'OltUpTime', 2: 'LedSwitch', 3: 'OltTemperature',
    4: 'OltTemperatureMax', 5: 'OltIntVoltage', 6: 'OltAuxVoltage', 7: 'ResetLedEnable',
    8: 'FpgaDna', 9: 'FlashRead', 10: 'FlashErase', 11: 'FlashWrite', 12: 'FlashErase64K',
    13: 'FlashProtect', 14: 'FlashWrite1K', 15: 'UplinkSpeed', 16: 'LoginStat',
    17: 'OltPassword', 18: 'OltLogout', 19: 'ChangePassword', 20: 'ChangePasswordRet',
    21: 'ResetPassword', 61731: 'Reboot',
    256: 'OpticalTxEnable', 257: 'OpticalVoltage', 258: 'OpticalCurrent',
    259: 'OpticalPower', 260: 'OpticalTemperature',
    261: 'BroadcastTxCnt', 262: 'BroadcastRxCnt', 263: 'MulticastTxCnt',
    264: 'MulticastRxCnt', 265: 'UnicastTxCnt', 266: 'UnicastRxCnt',
    267: 'LoidAuthSwitch', 268: 'AuthLoid', 269: 'AuthPassword',
    270: 'RogueOnuDetectSwitch', 271: 'DiscoverOnuSwitch', 272: 'OpticalModuleIIC',
    273: 'PeriodicalOptical', 274: 'PeriodicalOpticalOnPeriod',
    275: 'PeriodicalOpticalOffPeriod', 276: 'Prbs23Enable', 277: 'Prbs23ErrorCnt',
    278: 'OpticalSD', 279: 'Ux3326Reset', 280: 'MaxONU', 281: 'OmciMode',
    282: 'P2PSwitch', 283: 'EthAnSwitch', 284: 'CoverOffline',
    512: 'OnuOnlineNumber', 513: 'OnuStatus', 514: 'OnuUpTime', 515: 'OnuDownTime',
    516: 'OnuTxPower', 517: 'OnuRxPower', 518: 'OnuGponSn', 519: 'OnuRangeTime',
    520: 'OnuLoid', 521: 'DeactivateOnu', 522: 'OnuPassword', 523: 'DisableSN',
    524: 'OnuResponseTime', 525: 'OnuLastOfflineReason', 526: 'ActivateOnu',
    527: 'OnuOpticalInfo', 528: 'OnuOpticalInfoUpdate',
    768: 'OnuStatusChange', 769: 'RogueOnuDetected', 770: 'DiscoverOnuGponSn',
    2304: 'OmciErrorCnt', 2305: 'ResetPonRx', 2306: 'BWmapLoopMax',
    2307: 'BurstResetOffset', 2309: 'FanPwnWidth', 2310: 'BipErrorCnt',
    2311: 'MacTabelAddCnt', 2312: 'MacTabelAgingCnt', 2313: 'PwmCnt',
    2314: 'PwmWidth', 4096: 'OmciPackage',
}

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
        if block_type == 6:
            cap_len = struct.unpack_from('<I', data, pos + 20)[0]
            pkt_data = data[pos + 28: pos + 28 + cap_len]
            packets.append(pkt_data)
        block_len = (block_len + 3) & ~3
        pos += block_len
    return packets

filepath = r'C:\Users\Vivek\Downloads\volt\pcap\working-olt-pcap.pcapng'
pkts = read_pcapng(filepath)
print(f'Total packets: {len(pkts)}')

# Separate by EtherType
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

print(f'\nVOLT packets (0x88B6): {len(volt_pkts)}')

# Identify unique MACs
macs = set()
for p in volt_pkts:
    macs.add(mac_str(p[0:6]))
    macs.add(mac_str(p[6:12]))
print(f'MACs involved: {macs}')

# Separate requests (from PC) and responses (from OLT)
# OLT MAC from the screenshot: 48:E6:63:10:D4:38
olt_mac_candidates = set()
pc_mac_candidates = set()
for p in volt_pkts:
    src = mac_str(p[6:12])
    dst = mac_str(p[0:6])
    if dst == 'ff:ff:ff:ff:ff:ff':
        pc_mac_candidates.add(src)
    else:
        # Unicast response — src is OLT
        olt_mac_candidates.add(src)

print(f'PC MACs (broadcast senders): {pc_mac_candidates}')
print(f'OLT MACs (unicast responders): {olt_mac_candidates}')

# Determine which is which
pc_mac = list(pc_mac_candidates)[0] if pc_mac_candidates else None
olt_macs = olt_mac_candidates - pc_mac_candidates

requests = [p for p in volt_pkts if mac_str(p[6:12]) == pc_mac]
responses = [p for p in volt_pkts if mac_str(p[6:12]) != pc_mac]

print(f'\nRequests (from PC {pc_mac}): {len(requests)}')
print(f'Responses (from OLT): {len(responses)}')

# Show first 20 request/response pairs
print(f'\n{"="*80}')
print(f'=== FIRST 30 PACKETS (request + response flow) ===')
print(f'{"="*80}')
for i, p in enumerate(volt_pkts[:30]):
    src = mac_str(p[6:12])
    dst = mac_str(p[0:6])
    payload = p[14:]
    direction = 'REQ >>>' if src == pc_mac else '<<< RSP'

    magic = struct.unpack_from('>I', payload, 0)[0] if len(payload) >= 4 else 0
    seq = struct.unpack_from('>H', payload, 4)[0] if len(payload) >= 6 else 0
    length = struct.unpack_from('>H', payload, 6)[0] if len(payload) >= 8 else 0
    msg_id = struct.unpack_from('>H', payload, 8)[0] if len(payload) >= 10 else 0
    sub = payload[10] if len(payload) > 10 else 0
    target = payload[11] if len(payload) > 11 else 0
    data = payload[12:] if len(payload) > 12 else b''

    msg_name = MSG_IDS.get(msg_id, f'0x{msg_id:04X}')

    print(f'\n  [{i:3d}] {direction} seq=0x{seq:04X} msg_id=0x{msg_id:04X}({msg_name}) sub=0x{sub:02X} target=0x{target:02X}')
    print(f'        src={src} dst={dst} len_field={length}')
    if data and any(b != 0 for b in data):
        print(f'        data: {data.hex()}')
    else:
        print(f'        data: (all zeros, {len(data)} bytes)')

# Analyze response payloads — what data does the OLT send back?
print(f'\n{"="*80}')
print(f'=== RESPONSE ANALYSIS ===')
print(f'{"="*80}')

resp_by_msgid = defaultdict(list)
for p in responses:
    payload = p[14:]
    if len(payload) >= 12:
        msg_id = struct.unpack_from('>H', payload, 8)[0]
        sub = payload[10]
        target = payload[11]
        data = payload[12:]
        resp_by_msgid[msg_id].append({
            'sub': sub, 'target': target, 'data': data,
            'seq': struct.unpack_from('>H', payload, 4)[0]
        })

for msg_id in sorted(resp_by_msgid.keys()):
    entries = resp_by_msgid[msg_id]
    name = MSG_IDS.get(msg_id, f'0x{msg_id:04X}')
    print(f'\n  {name} (0x{msg_id:04X}): {len(entries)} responses')
    for e in entries[:5]:
        data_hex = e['data'].hex() if any(b != 0 for b in e['data']) else '(zeros)'
        print(f'    sub=0x{e["sub"]:02X} target=0x{e["target"]:02X} data={data_hex}')

# Summary of response data patterns
print(f'\n{"="*80}')
print(f'=== PROTOCOL SUMMARY ===')
print(f'{"="*80}')
print(f'PC MAC:  {pc_mac}')
print(f'OLT MAC: {olt_macs}')
print(f'Requests:  {len(requests)}')
print(f'Responses: {len(responses)}')
print(f'Response message IDs: {sorted(resp_by_msgid.keys())}')
print(f'Response msg names: {[MSG_IDS.get(k, f"0x{k:04X}") for k in sorted(resp_by_msgid.keys())]}')
