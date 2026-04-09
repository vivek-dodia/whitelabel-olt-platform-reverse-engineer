import sys, os, types, dis, io, marshal, builtins

EXTRACT_DIR = r'C:\Users\Vivek\Downloads\volt\VOLT_Tool_V6.6.exe_extracted'
PYZ_DIR = os.path.join(EXTRACT_DIR, 'PYZ-00.pyz_extracted')
DUMP_DIR = r'C:\Users\Vivek\Downloads\volt\decrypted_source'
os.makedirs(DUMP_DIR, exist_ok=True)

sys.path.insert(0, EXTRACT_DIR)
sys.path.insert(0, PYZ_DIR)
os.add_dll_directory(EXTRACT_DIR)
sys._MEIPASS = EXTRACT_DIR

# Hook exec BEFORE anything loads
dumped = {}
_real_exec = builtins.exec

def hooked_exec(code, globs=None, locs=None):
    if isinstance(code, types.CodeType):
        fn = code.co_filename or ''
        cname = code.co_name or ''
        
        # Check if this is decrypted app code (will have real function names, not just <module>)
        is_app = any(x in fn for x in ['gdt', 'olt_socket', 'com_def', 'Widget', 'Password', 
                                         'Message', 'TextInput', 'WaitBob', 'WaitPassword',
                                         'OltInfo', 'OnuInfo', 'OmciMsg'])
        
        if is_app:
            mod_name = os.path.basename(fn).replace('.py', '')
            
            # Check if this code object has REAL content (not just the pyarmor wrapper)
            has_real_code = len(code.co_names) > 4 or any(
                isinstance(c, types.CodeType) and c.co_name not in ('<module>',)
                for c in code.co_consts
            )
            
            if has_real_code and mod_name not in dumped:
                print(f"  [DECRYPTED!] {mod_name}: {len(code.co_names)} names, co_name={cname}")
                print(f"    Names: {code.co_names[:30]}")
                dumped[mod_name] = code
                
                # Dump disassembly
                outpath = os.path.join(DUMP_DIR, f'{mod_name}.txt')
                with open(outpath, 'w', encoding='utf-8') as f:
                    def dump_code(co, indent=0):
                        pfx = "  " * indent
                        f.write(f"\n{pfx}{'='*60}\n")
                        f.write(f"{pfx}CODE: {co.co_name} (file: {co.co_filename})\n")
                        f.write(f"{pfx}  argcount={co.co_argcount}\n")
                        f.write(f"{pfx}  varnames: {co.co_varnames}\n")
                        f.write(f"{pfx}  names: {co.co_names}\n")
                        f.write(f"{pfx}  freevars: {co.co_freevars}, cellvars: {co.co_cellvars}\n")
                        nc = []
                        for c in co.co_consts:
                            if isinstance(c, types.CodeType): continue
                            r = repr(c)
                            nc.append(r[:200] if len(r) > 200 else r)
                        f.write(f"{pfx}  consts: {nc}\n")
                        sio = io.StringIO()
                        try: dis.dis(co, file=sio)
                        except: sio.write("(failed)\n")
                        for line in sio.getvalue().split('\n'):
                            f.write(f"{pfx}  {line}\n")
                        for c in co.co_consts:
                            if isinstance(c, types.CodeType):
                                dump_code(c, indent + 1)
                    dump_code(code)
                
                # Save .pyc
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

builtins.exec = hooked_exec

# Now run the actual entry points in order
# First load gdt.pyc (the main entry) - it will call pytransform.pyarmor() 
# which will decrypt and exec the real code, which our hook will catch

targets = ['com_def', 'olt_socket', 'OltInfoWidget', 'OltInfoWidget_form',
           'OnuInfoWidget', 'OnuInfoWidget_form', 'OmciMsgWidget', 'OmciMsgWidget_form',
           'PasswordWidget', 'PasswordWidget_form', 'MessageWidget', 'MessageWidget_form',
           'TextInputWidget', 'TextInputWidget_form', 'WaitBobWriteWidget', 
           'WaitBobWriteWidget_form', 'WaitPasswordSaveWidget', 'gdt_form']

for modname in targets:
    pyc_path = os.path.join(PYZ_DIR, f'{modname}.pyc')
    if not os.path.exists(pyc_path):
        continue
    
    print(f"\n[*] Loading {modname}...")
    try:
        with open(pyc_path, 'rb') as f:
            f.read(16)  # skip header
            co = marshal.load(f)
        
        # Execute the pyarmor wrapper code which will decrypt and exec real code
        mod_dict = {
            '__name__': modname,
            '__file__': os.path.join(PYZ_DIR, f'{modname}.pyc'),
            '__builtins__': builtins,
            '__loader__': None,
            '__spec__': None,
        }
        _real_exec(co, mod_dict)
        print(f"  [OK] {modname} loaded")
    except SystemExit:
        print(f"  [!] {modname} tried to exit")
    except Exception as e:
        err = str(e)
        if len(err) > 200: err = err[:200]
        print(f"  [-] {modname}: {err}")

print(f"\n{'='*60}")
print(f"Total decrypted modules: {len(dumped)}")
for name in sorted(dumped.keys()):
    co = dumped[name]
    nested = len([c for c in co.co_consts if isinstance(c, types.CodeType)])
    print(f"  {name}: {len(co.co_names)} names, {nested} functions/classes")
