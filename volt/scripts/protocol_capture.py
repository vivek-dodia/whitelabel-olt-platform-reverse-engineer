"""
VOLT OLT Protocol Capture Script
Hooks into scapy and socket to capture protocol traffic.
Run the VOLT tool through this wrapper.
"""
import sys
import os
import struct
import time
import json
from datetime import datetime

# Set up logging
LOG_FILE = os.path.join(os.path.dirname(__file__), 'protocol_log.jsonl')

def log_event(event_type, data):
    entry = {
        'timestamp': datetime.now().isoformat(),
        'type': event_type,
        'data': data
    }
    with open(LOG_FILE, 'a') as f:
        f.write(json.dumps(entry, default=str) + '\n')
    print(f'[HOOK] {event_type}: {json.dumps(data, default=str)[:200]}')

# Hook 1: Patch scapy send/receive functions
original_sendp = None
original_srp = None
original_sniff = None

def hook_scapy():
    global original_sendp, original_srp, original_sniff
    try:
        import scapy.sendrecv as sr
        import scapy.all as scapy_all
        
        original_sendp = sr.sendp
        original_srp = sr.srp
        
        def hooked_sendp(x, *args, **kwargs):
            try:
                log_event('SENDP', {
                    'packet_hex': bytes(x).hex(),
                    'packet_summary': x.summary() if hasattr(x, 'summary') else str(x),
                    'packet_show': x.show(dump=True) if hasattr(x, 'show') else '',
                    'kwargs': str(kwargs)
                })
            except Exception as e:
                log_event('SENDP_LOG_ERROR', {'error': str(e)})
            return original_sendp(x, *args, **kwargs)
        
        def hooked_srp(x, *args, **kwargs):
            try:
                log_event('SRP_SEND', {
                    'packet_hex': bytes(x).hex(),
                    'packet_summary': x.summary() if hasattr(x, 'summary') else str(x),
                    'kwargs': str(kwargs)
                })
            except Exception as e:
                log_event('SRP_LOG_ERROR', {'error': str(e)})
            result = original_srp(x, *args, **kwargs)
            try:
                if result and result[0]:
                    for sent, recv in result[0]:
                        log_event('SRP_RECV', {
                            'sent_hex': bytes(sent).hex(),
                            'recv_hex': bytes(recv).hex(),
                            'recv_summary': recv.summary() if hasattr(recv, 'summary') else str(recv),
                        })
            except Exception as e:
                log_event('SRP_RECV_LOG_ERROR', {'error': str(e)})
            return result
        
        sr.sendp = hooked_sendp
        sr.srp = hooked_srp
        scapy_all.sendp = hooked_sendp
        scapy_all.srp = hooked_srp
        
        log_event('HOOK_INSTALLED', {'target': 'scapy.sendrecv'})
    except Exception as e:
        log_event('HOOK_FAILED', {'target': 'scapy', 'error': str(e)})

# Hook 2: Patch Crypto operations
def hook_crypto():
    try:
        from Crypto.Cipher import AES
        original_new = AES.new
        
        def hooked_aes_new(key, mode=None, **kwargs):
            log_event('AES_NEW', {
                'key_hex': key.hex() if isinstance(key, bytes) else str(key),
                'key_len': len(key),
                'mode': mode,
                'kwargs': str(kwargs)
            })
            return original_new(key, mode, **kwargs)
        
        AES.new = hooked_aes_new
        log_event('HOOK_INSTALLED', {'target': 'Crypto.Cipher.AES'})
    except Exception as e:
        log_event('HOOK_FAILED', {'target': 'Crypto', 'error': str(e)})

# Hook 3: Patch raw socket operations
def hook_socket():
    try:
        import socket
        original_socket = socket.socket
        
        class HookedSocket(original_socket):
            def sendto(self, data, *args):
                log_event('SOCKET_SENDTO', {
                    'data_hex': data.hex() if isinstance(data, bytes) else str(data),
                    'args': str(args)
                })
                return super().sendto(data, *args)
            
            def send(self, data, *args):
                log_event('SOCKET_SEND', {
                    'data_hex': data.hex() if isinstance(data, bytes) else str(data),
                })
                return super().send(data, *args)
            
            def recv(self, *args):
                data = super().recv(*args)
                log_event('SOCKET_RECV', {
                    'data_hex': data.hex() if isinstance(data, bytes) else str(data),
                })
                return data
        
        socket.socket = HookedSocket
        log_event('HOOK_INSTALLED', {'target': 'socket'})
    except Exception as e:
        log_event('HOOK_FAILED', {'target': 'socket', 'error': str(e)})

print("[*] VOLT Protocol Capture - Installing hooks...")
hook_socket()
hook_scapy()
hook_crypto()
print(f"[*] Logging to: {LOG_FILE}")
print("[*] Hooks installed. Protocol data will be captured.")
