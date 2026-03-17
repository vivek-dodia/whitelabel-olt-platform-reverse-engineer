import sys, os, types, dis, io, marshal, builtins, traceback

EXTRACT_DIR = r'C:\Users\Vivek\Downloads\volt\VOLT_Tool_V6.6.exe_extracted'
PYZ_DIR = os.path.join(EXTRACT_DIR, 'PYZ-00.pyz_extracted')
DUMP_DIR = r'C:\Users\Vivek\Downloads\volt\decrypted_source'
os.makedirs(DUMP_DIR, exist_ok=True)

sys.path.insert(0, EXTRACT_DIR)
sys.path.insert(0, PYZ_DIR)
os.add_dll_directory(EXTRACT_DIR)

# Add crypto DLLs
crypto_dirs = [
    os.path.join(EXTRACT_DIR, 'Crypto', 'Cipher'),
    os.path.join(EXTRACT_DIR, 'Crypto', 'Hash'),
    os.path.join(EXTRACT_DIR, 'Crypto', 'Math'),
    os.path.join(EXTRACT_DIR, 'Crypto', 'Util'),
    os.path.join(EXTRACT_DIR, 'Crypto', 'Protocol'),
    os.path.join(EXTRACT_DIR, 'Crypto', 'PublicKey'),
    os.path.join(EXTRACT_DIR, 'Crypto'),
]
for d in crypto_dirs:
    if os.path.exists(d):
        sys.path.insert(0, d)
        try: os.add_dll_directory(d)
        except: pass

qt_bin = os.path.join(EXTRACT_DIR, 'PyQt5', 'Qt5', 'bin')
if os.path.exists(qt_bin):
    os.add_dll_directory(qt_bin)
    os.environ['PATH'] = qt_bin + ';' + os.environ.get('PATH', '')

sys._MEIPASS = EXTRACT_DIR

# Use sys.settrace to capture ALL function calls from app code
captured_functions = {}
log_file = open(os.path.join(DUMP_DIR, '_trace_log.txt'), 'w', encoding='utf-8')

def trace_func(frame, event, arg):
    fn = frame.f_code.co_filename or ''
    
    # Only trace app code files
    if any(x in fn for x in ['gdt', 'olt_socket', 'com_def', 'Widget', 'Password',
                               'Message', 'TextInput', 'WaitBob', 'WaitPassword',
                               'OltInfo', 'OnuInfo', 'OmciMsg']):
        co = frame.f_code
        key = f"{fn}::{co.co_name}"
        
        if event == 'call' and key not in captured_functions:
            captured_functions[key] = co
            log_file.write(f"CALL {key}\n")
            log_file.write(f"  args: {co.co_varnames[:co.co_argcount]}\n")
            log_file.write(f"  names: {co.co_names}\n")
            log_file.write(f"  consts: {[repr(c)[:100] for c in co.co_consts if not isinstance(c, types.CodeType)]}\n")
            log_file.flush()
            
            # Dump the code object
            mod_name = os.path.basename(fn).replace('.py', '')
            func_name = co.co_name.replace('<', '').replace('>', '')
            outpath = os.path.join(DUMP_DIR, f'{mod_name}__{func_name}.txt')
            with open(outpath, 'w', encoding='utf-8') as f:
                f.write(f"# {key}\n")
                f.write(f"# argcount: {co.co_argcount}\n")
                f.write(f"# varnames: {co.co_varnames}\n")
                f.write(f"# names: {co.co_names}\n")
                f.write(f"# freevars: {co.co_freevars}\n")
                f.write(f"# cellvars: {co.co_cellvars}\n")
                nc = []
                for c in co.co_consts:
                    if isinstance(c, types.CodeType):
                        nc.append(f"<code:{c.co_name}>")
                    else:
                        r = repr(c)
                        nc.append(r[:200] if len(r) > 200 else r)
                f.write(f"# consts: {nc}\n\n")
                sio = io.StringIO()
                try: dis.dis(co, file=sio)
                except: sio.write("(failed)\n")
                f.write(sio.getvalue())
                
                # Also dump nested code objects
                for c in co.co_consts:
                    if isinstance(c, types.CodeType):
                        f.write(f"\n\n# --- Nested: {c.co_name} ---\n")
                        f.write(f"# varnames: {c.co_varnames}\n")
                        f.write(f"# names: {c.co_names}\n")
                        sio2 = io.StringIO()
                        try: dis.dis(c, file=sio2)
                        except: sio2.write("(failed)\n")
                        f.write(sio2.getvalue())
    
    return trace_func

sys.settrace(trace_func)
print("[+] sys.settrace installed")

# Now load modules
_real_exec = builtins.exec

targets = ['com_def', 'olt_socket', 'gdt_form',
           'OltInfoWidget', 'OnuInfoWidget', 'OmciMsgWidget',
           'PasswordWidget', 'MessageWidget', 'TextInputWidget',
           'WaitBobWriteWidget', 'WaitPasswordSaveWidget']

for modname in targets:
    pyc_path = os.path.join(PYZ_DIR, f'{modname}.pyc')
    if not os.path.exists(pyc_path):
        continue
    
    print(f"\n[*] Loading {modname}...")
    try:
        with open(pyc_path, 'rb') as f:
            f.read(16)
            co = marshal.load(f)
        
        mod_dict = {
            '__name__': modname,
            '__file__': f'{modname}.py',
            '__builtins__': builtins,
        }
        _real_exec(co, mod_dict)
        print(f"  [+] OK")
    except Exception as e:
        err = str(e)
        if len(err) > 150: err = err[:150]
        print(f"  [-] {err}")

sys.settrace(None)
log_file.close()

print(f"\n{'='*60}")
print(f"Captured {len(captured_functions)} unique functions:")
for key in sorted(captured_functions.keys()):
    co = captured_functions[key]
    print(f"  {key}: args={co.co_varnames[:co.co_argcount]}")
