"""
VOLT OLT Protocol Decoder — maps captured Wireshark frames to reverse-engineered enums
"""
import struct, sys, io
from collections import Counter, defaultdict

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

# From reverse engineering (com_def runtime extraction)
MSG_TYPES = {-1: 'Init', 0: 'Get', 1: 'GetResponse', 2: 'Set', 3: 'SetResponse', 4: 'Notify', 5: 'OMCI'}
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

def decode_volt_payload(payload):
    """Decode VOLT protocol payload and map to known enums"""
    if len(payload) < 8:
        return None

    magic = struct.unpack_from('>I', payload, 0)[0]
    if magic != 0xB958D63A:
        return None

    # Bytes 4-5: sequence number (increments per packet)
    seq = struct.unpack_from('>H', payload, 4)[0]

    # Byte 6: appears to be payload length or flags
    byte6 = payload[6]
    byte7 = payload[7]

    # Bytes 8-9: could be message type (high byte) + message ID category
    # Bytes 10-11: could be message ID or ONU index
    if len(payload) >= 12:
        word8 = struct.unpack_from('>H', payload, 8)[0]
        word10 = struct.unpack_from('>H', payload, 10)[0]

        # Try different field interpretations
        # Interpretation 1: byte8 = msg_type, bytes 9-10 = msg_id (big endian)
        msg_type_1 = payload[8]
        msg_id_1 = struct.unpack_from('>H', payload, 9)[0]

        # Interpretation 2: word8 = msg_id, byte10 = onu_index
        msg_id_2 = word8
        onu_idx = payload[10]

        # Interpretation 3: byte8-9 = msg_type+flags, byte10-11 = msg_id
        msg_id_3 = word10

        return {
            'seq': seq,
            'byte6': byte6,
            'byte7': byte7,
            'word8': word8,
            'word10': word10,
            'byte8': payload[8],
            'byte9': payload[9],
            'byte10': payload[10],
            'byte11': payload[11] if len(payload) > 11 else 0,
            'msg_type_name': MSG_TYPES.get(payload[8], MSG_TYPES.get(payload[8] - 256, f'0x{payload[8]:02X}')),
            'msg_id_2_name': MSG_IDS.get(word8, f'0x{word8:04X}'),
            'msg_id_3_name': MSG_IDS.get(word10, f'0x{word10:04X}'),
            'data': payload[12:] if len(payload) > 12 else b'',
        }

# Analyze both captures
for pcap_file in ['volt.pcapng', 'volt2.pcapng']:
    filepath = f'C:\\Users\\Vivek\\Downloads\\volt\\pcap\\{pcap_file}'
    print(f'\n{"="*70}')
    print(f'=== {pcap_file} ===')
    print(f'{"="*70}')

    pkts = read_pcapng(filepath)
    volt_pkts = [p for p in pkts if len(p) >= 14 and struct.unpack_from('>H', p, 12)[0] == 0x88B6]
    print(f'VOLT packets: {len(volt_pkts)}')

    # Decode and analyze
    word8_counter = Counter()
    word10_counter = Counter()
    byte8_counter = Counter()
    seq_values = []

    print(f'\n--- First 20 decoded packets ---')
    for i, p in enumerate(volt_pkts[:20]):
        payload = p[14:]
        d = decode_volt_payload(payload)
        if not d:
            continue

        seq_values.append(d['seq'])
        word8_counter[d['word8']] += 1
        word10_counter[d['word10']] += 1
        byte8_counter[d['byte8']] += 1

        print(f'\n  Pkt {i}: seq=0x{d["seq"]:04X} len_field=0x{d["byte6"]:02X}{d["byte7"]:02X}')
        print(f'    word8=0x{d["word8"]:04X} ({d["msg_id_2_name"]})')
        print(f'    word10=0x{d["word10"]:04X} byte10=0x{d["byte10"]:02X} byte11=0x{d["byte11"]:02X}')
        print(f'    raw: {payload.hex()}')

    # Full statistics
    for p in volt_pkts[20:]:
        payload = p[14:]
        d = decode_volt_payload(payload)
        if d:
            seq_values.append(d['seq'])
            word8_counter[d['word8']] += 1
            word10_counter[d['word10']] += 1
            byte8_counter[d['byte8']] += 1

    print(f'\n--- Field Statistics ---')
    print(f'\nword8 (potential msg_id) distribution:')
    for val, count in word8_counter.most_common(30):
        name = MSG_IDS.get(val, '???')
        print(f'  0x{val:04X} ({val:5d}) = {name:30s} count={count}')

    print(f'\nbyte8 distribution:')
    for val, count in byte8_counter.most_common(10):
        name = MSG_TYPES.get(val, MSG_TYPES.get(val - 256, f'???'))
        print(f'  0x{val:02X} ({val:3d}) = {name:15s} count={count}')

    print(f'\nword10 distribution (top 20):')
    for val, count in word10_counter.most_common(20):
        name = MSG_IDS.get(val, '???')
        hi = (val >> 8) & 0xFF
        lo = val & 0xFF
        print(f'  0x{val:04X} (hi=0x{hi:02X} lo=0x{lo:02X}) = {name:30s} count={count}')

    # Sequence analysis
    if seq_values:
        print(f'\nSequence range: 0x{min(seq_values):04X} to 0x{max(seq_values):04X}')
        print(f'Unique sequences: {len(set(seq_values))}')
        # Check if sequential
        diffs = [seq_values[i+1] - seq_values[i] for i in range(min(100, len(seq_values)-1))]
        print(f'First 20 seq diffs: {diffs[:20]}')

    # Pattern: look at the repeating cycle
    print(f'\n--- Discovery Cycle Pattern ---')
    # Group by word8 to see what queries repeat
    cycle_pattern = []
    seen = set()
    for p in volt_pkts[:200]:
        payload = p[14:]
        d = decode_volt_payload(payload)
        if d:
            key = (d['word8'], d['byte10'], d['byte11'])
            if key in seen:
                break
            seen.add(key)
            name = MSG_IDS.get(d['word8'], f'0x{d["word8"]:04X}')
            cycle_pattern.append(f'word8=0x{d["word8"]:04X}({name}) onu={d["byte10"]}')

    print(f'Cycle length: {len(cycle_pattern)} unique queries')
    for j, pat in enumerate(cycle_pattern[:50]):
        print(f'  [{j:3d}] {pat}')
    if len(cycle_pattern) > 50:
        print(f'  ... and {len(cycle_pattern) - 50} more')
