import sys, os, types, dis, io, marshal

EXTRACT_DIR = r'C:\Users\Vivek\Downloads\volt\VOLT_Tool_V6.6.exe_extracted'
DUMP_DIR = r'C:\Users\Vivek\Downloads\volt\decrypted_source'
os.makedirs(DUMP_DIR, exist_ok=True)

sys.path.insert(0, EXTRACT_DIR)
os.add_dll_directory(EXTRACT_DIR)
sys._MEIPASS = EXTRACT_DIR

# Hook exec
dumped = {}
_real_exec = exec

def hooked_exec(code, globs=None, locs=None):
    if isinstance(code, types.CodeType):
        fn = code.co_filename
        if any(x in fn for x in ['gdt', 'olt_socket', 'com_def', 'Widget', 'Password', 
                                   'Message', 'TextInput', 'WaitBob', 'WaitPassword',
                                   'OltInfo', 'OnuInfo', 'OmciMsg']):
            mod_name = os.path.basename(fn).replace('.py', '')
            if mod_name not in dumped:
                print(f"  [CAPTURED] {mod_name} ({len(code.co_consts)} consts, {len(code.co_names)} names)")
                dumped[mod_name] = code
                
                outpath = os.path.join(DUMP_DIR, f'{mod_name}.txt')
                with open(outpath, 'w', encoding='utf-8') as f:
                    def dump_code(co, indent=0):
                        pfx = "  " * indent
                        f.write(f"\n{pfx}{'='*60}\n")
                        f.write(f"{pfx}CODE: {co.co_name} (file: {co.co_filename})\n")
                        f.write(f"{pfx}  argcount={co.co_argcount}, kwonly={co.co_kwonlyargcount}\n")
                        f.write(f"{pfx}  varnames: {co.co_varnames}\n")
                        f.write(f"{pfx}  names: {co.co_names}\n")
                        f.write(f"{pfx}  freevars: {co.co_freevars}, cellvars: {co.co_cellvars}\n")
                        nc = []
                        for c in co.co_consts:
                            if isinstance(c, types.CodeType):
                                continue
                            elif isinstance(c, bytes) and len(c) > 200:
                                nc.append(f"<bytes:{len(c)}B>")
                            else:
                                nc.append(repr(c))
                        f.write(f"{pfx}  consts: {nc}\n")
                        sio = io.StringIO()
                        try:
                            dis.dis(co, file=sio)
                        except Exception as e:
                            sio.write(f"(dis failed: {e})\n")
                        for line in sio.getvalue().split('\n'):
                            f.write(f"{pfx}  {line}\n")
                        for c in co.co_consts:
                            if isinstance(c, types.CodeType):
                                dump_code(c, indent + 1)
                    dump_code(code)
                
                pyc_path = os.path.join(DUMP_DIR, f'{mod_name}.pyc')
                with open(pyc_path, 'wb') as f:
                    f.write(b'\x61\x0d\x0d\x0a\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00')
                    marshal.dump(code, f)
    
    if globs is None:
        return _real_exec(code)
    elif locs is None:
        return _real_exec(code, globs)
    else:
        return _real_exec(code, globs, locs)

import builtins
builtins.exec = hooked_exec

# Load pytransform
import pytransform
print(f"[+] pytransform loaded, version: {pytransform.version_info()}")

# Now manually load each module's .pyc, extract the pyarmor blob, and call pyarmor()
PYZ_DIR = os.path.join(EXTRACT_DIR, 'PYZ-00.pyz_extracted')

targets = ['com_def', 'olt_socket', 'gdt_form',
           'OltInfoWidget', 'OltInfoWidget_form',
           'OnuInfoWidget', 'OnuInfoWidget_form',
           'OmciMsgWidget', 'OmciMsgWidget_form',
           'PasswordWidget', 'PasswordWidget_form',
           'MessageWidget', 'MessageWidget_form',
           'TextInputWidget', 'TextInputWidget_form',
           'WaitBobWriteWidget', 'WaitBobWriteWidget_form',
           'WaitPasswordSaveWidget']

for modname in targets:
    pyc_path = os.path.join(PYZ_DIR, f'{modname}.pyc')
    if not os.path.exists(pyc_path):
        print(f"[-] {modname}: not found")
        continue
    
    try:
        # Load the .pyc code object
        with open(pyc_path, 'rb') as f:
            magic = f.read(4)
            flags = f.read(4)
            f.read(8)  # timestamp + size
            co = marshal.load(f)
        
        # The code object is: from pytransform import pyarmor; pyarmor(__name__, __file__, blob)
        # Extract the blob from co.co_consts
        blob = None
        for c in co.co_consts:
            if isinstance(c, bytes) and b'PYARMOR' in c:
                blob = c
                break
        
        if blob is None:
            print(f"[-] {modname}: no PYARMOR blob found")
            continue
        
        print(f"[*] Decrypting {modname} (blob: {len(blob)} bytes)...")
        
        # Create a fake module namespace
        mod_globals = {
            '__name__': modname,
            '__file__': f'{modname}.py',
            '__builtins__': builtins,
        }
        
        # Call pyarmor to decrypt and exec
        try:
            pytransform.pyarmor(modname, f'{modname}.py', blob, 2)
            print(f"[+] {modname}: decrypted!")
        except Exception as e:
            print(f"[-] {modname}: pyarmor failed: {e}")
    
    except Exception as e:
        print(f"[-] {modname}: error: {e}")

print(f"\n[*] Total captured: {len(dumped)} modules")
for name in sorted(dumped.keys()):
    co = dumped[name]
    print(f"  {name}: {len(co.co_names)} names, {len([c for c in co.co_consts if isinstance(c, types.CodeType)])} code objects")
