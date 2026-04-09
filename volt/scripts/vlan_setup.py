import socket, struct, time, subprocess, os, sys

class ROSAPI:
    def __init__(self, host):
        self.host = host
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.sock.settimeout(10)
        self.sock.connect((host, 8728))
    def _enc(self, l):
        if l < 0x80: return bytes([l])
        elif l < 0x4000: return struct.pack('>H', l | 0x8000)
        else: return struct.pack('>I', l | 0xE0000000)
    def _dec(self):
        b = self.sock.recv(1)
        if not b: return None
        b = b[0]
        if b < 0x80: return b
        elif b < 0xC0: return ((b & 0x3F) << 8) + self.sock.recv(1)[0]
        elif b < 0xE0:
            r = self.sock.recv(2); return ((b & 0x1F) << 16) + (r[0] << 8) + r[1]
        elif b < 0xF0:
            r = self.sock.recv(3); return ((b & 0x0F) << 24) + (r[0] << 16) + (r[1] << 8) + r[2]
        else: return struct.unpack('>I', self.sock.recv(4))[0]
    def _sw(self, w):
        e = w.encode() if isinstance(w, str) else w
        self.sock.send(self._enc(len(e)) + e)
    def _ss(self, words):
        for w in words: self._sw(w)
        self._sw('')
    def _rs(self):
        words = []
        while True:
            l = self._dec()
            if l is None or l == 0: break
            d = b''
            while len(d) < l: d += self.sock.recv(l - len(d))
            words.append(d.decode('utf-8', errors='replace'))
        return words
    def login(self, u, p):
        self._ss(['/login', '=name=' + u, '=password=' + p])
        r = self._rs()
        return r and r[0] == '!done'
    def cmd(self, c, a=None):
        self._ss([c] + (a or []))
        res = []
        while True:
            r = self._rs()
            if not r or r[0] == '!done': break
            if r[0] == '!trap':
                print(f'  ERR [{self.host}]: {r}')
                break
            res.append(r)
        return res

def ping_check(host):
    r = subprocess.run(['ping', '-c', '2', '-W', '2', host], capture_output=True, text=True)
    return r.returncode == 0

# ========================================
# STEP 1: Configure R01
# ========================================
print('=' * 50)
print('STEP 1: R01 (192.168.10.1)')
print('=' * 50)

r01 = ROSAPI('192.168.10.1')
r01.login('admin', '2mint503!')

# Get bridge ID
bridge_id = None
for r in r01.cmd('/interface/bridge/print', ['?name=main-office']):
    for x in r:
        if x.startswith('=.id='): bridge_id = x.split('=.id=')[1]
        if 'vlan-filtering' in x: print('  Current: ' + x)

# Get all bridge port names for VLAN 1 safety
port_names = []
for r in r01.cmd('/interface/bridge/port/print', ['?bridge=main-office']):
    for x in r:
        if x.startswith('=interface='): port_names.append(x.split('=interface=')[1])
print(f'  Bridge ports: {port_names}')

# Add VLAN 4091 (tagged on sfp-sfpplus1 + ether10)
print('\n  Adding VLAN 4091 (tagged: sfp-sfpplus1, ether10)...')
r01.cmd('/interface/bridge/vlan/add', [
    '=bridge=main-office',
    '=vlan-ids=4091',
    '=tagged=sfp-sfpplus1,ether10'
])

# Add VLAN 1 for all ports + bridge interface (CRITICAL for existing traffic)
print('  Adding VLAN 1 (untagged: all ports + bridge)...')
r01.cmd('/interface/bridge/vlan/add', [
    '=bridge=main-office',
    '=vlan-ids=1',
    '=untagged=' + ','.join(port_names) + ',main-office'
])

# Set frame-types=admit-all on trunk ports
for pname in ['sfp-sfpplus1', 'ether10']:
    for r in r01.cmd('/interface/bridge/port/print', ['?interface=' + pname]):
        pid = None
        for x in r:
            if x.startswith('=.id='): pid = x.split('=.id=')[1]
        if pid:
            r01.cmd('/interface/bridge/port/set', ['=.id=' + pid, '=frame-types=admit-all'])
            print(f'  {pname}: frame-types=admit-all')

# Enable VLAN filtering
print('\n  Enabling VLAN filtering on main-office...')
r01.cmd('/interface/bridge/set', ['=.id=' + bridge_id, '=vlan-filtering=true'])

time.sleep(2)
if ping_check('192.168.10.1'):
    print('  PING R01: OK!')
else:
    print('  PING R01: FAIL! Waiting for potential auto-revert...')
    time.sleep(30)
    sys.exit(1)

# Show VLANs
print('\n  R01 VLANs:')
for r in r01.cmd('/interface/bridge/vlan/print'):
    vids = tagged = untagged = ''
    for x in r:
        if '=vlan-ids=' in x: vids = x
        if '=tagged=' in x: tagged = x
        if '=untagged=' in x: untagged = x
    if vids: print(f'    {vids} {tagged} {untagged}')

# ========================================
# STEP 2: Configure SW02
# ========================================
print('\n' + '=' * 50)
print('STEP 2: SW02 (192.168.10.97)')
print('=' * 50)

sw02 = ROSAPI('192.168.10.97')
sw02.login('admin', '2mint503!')

bridge_id2 = None
for r in sw02.cmd('/interface/bridge/print', ['?name=bridge']):
    for x in r:
        if x.startswith('=.id='): bridge_id2 = x.split('=.id=')[1]
        if 'vlan-filtering' in x: print('  Current: ' + x)

port_names2 = []
for r in sw02.cmd('/interface/bridge/port/print', ['?bridge=bridge']):
    for x in r:
        if x.startswith('=interface='): port_names2.append(x.split('=interface=')[1])
print(f'  Bridge ports: {len(port_names2)} ports')

# Add VLAN 4091 (tagged on ether1, untagged on sfp-sfpplus1)
print('\n  Adding VLAN 4091 (tagged: ether1, untagged: sfp-sfpplus1)...')
sw02.cmd('/interface/bridge/vlan/add', [
    '=bridge=bridge',
    '=vlan-ids=4091',
    '=tagged=ether1',
    '=untagged=sfp-sfpplus1'
])

# Add VLAN 1 for all ports + bridge
print('  Adding VLAN 1 (untagged: all ports + bridge)...')
sw02.cmd('/interface/bridge/vlan/add', [
    '=bridge=bridge',
    '=vlan-ids=1',
    '=untagged=' + ','.join(port_names2) + ',bridge'
])

# Set frame-types=admit-all on trunk ports
for pname in ['ether1', 'sfp-sfpplus1']:
    for r in sw02.cmd('/interface/bridge/port/print', ['?interface=' + pname]):
        pid = None
        for x in r:
            if x.startswith('=.id='): pid = x.split('=.id=')[1]
        if pid:
            sw02.cmd('/interface/bridge/port/set', ['=.id=' + pid, '=frame-types=admit-all'])
            print(f'  {pname}: frame-types=admit-all')

# Enable VLAN filtering
print('\n  Enabling VLAN filtering on bridge...')
sw02.cmd('/interface/bridge/set', ['=.id=' + bridge_id2, '=vlan-filtering=true'])

time.sleep(2)
if ping_check('192.168.10.97'):
    print('  PING SW02: OK!')
else:
    print('  PING SW02: FAIL!')
    sys.exit(1)

print('\n  SW02 VLANs:')
for r in sw02.cmd('/interface/bridge/vlan/print'):
    vids = tagged = untagged = ''
    for x in r:
        if '=vlan-ids=' in x: vids = x
        if '=tagged=' in x: tagged = x
        if '=untagged=' in x: untagged = x
    if vids: print(f'    {vids} {tagged} {untagged}')

# ========================================
# STEP 3: Linux VLAN interface
# ========================================
print('\n' + '=' * 50)
print('STEP 3: Linux eth0.4091')
print('=' * 50)
os.system('sudo ip link del eth0.4091 2>/dev/null')
os.system('sudo ip link add link eth0 name eth0.4091 type vlan id 4091')
os.system('sudo ip link set eth0.4091 up')
os.system('ip link show eth0.4091')

# Final checks
print('\n' + '=' * 50)
print('FINAL CHECKS')
print('=' * 50)
print(f'Ping R01:  {"OK" if ping_check("192.168.10.1") else "FAIL"}')
print(f'Ping SW02: {"OK" if ping_check("192.168.10.97") else "FAIL"}')
print(f'Ping .19:  {"OK" if ping_check("192.168.10.19") else "FAIL"}')
print('\nVLAN 4091 path ready! Run L2 probes on eth0.4091')
