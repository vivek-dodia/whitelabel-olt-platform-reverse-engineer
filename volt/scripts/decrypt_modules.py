import sys, os, types, dis, io, marshal, importlib

EXTRACT_DIR = r'C:\Users\Vivek\Downloads\volt\VOLT_Tool_V6.6.exe_extracted'
DUMP_DIR = r'C:\Users\Vivek\Downloads\volt\decrypted_source'
os.makedirs(DUMP_DIR, exist_ok=True)

# Add extracted dir to path so pytransform.pyd can be found
sys.path.insert(0, EXTRACT_DIR)
os.add_dll_directory(EXTRACT_DIR)

# Set _MEIPASS for PyInstaller compatibility
sys._MEIPASS = EXTRACT_DIR

# Hook exec to capture decrypted code
dumped_modules = {}
_real_exec = exec

def hooked_exec(code, globs=None, locs=None):
    if isinstance(code, types.CodeType):
        fn = code.co_filename
        name = code.co_name
        
        # Capture ALL code objects from app modules
        if any(x in fn for x in ['gdt', 'olt_socket', 'com_def', 'Widget', 'obf', 'Password', 'Message', 'TextInput', 'WaitBob', 'WaitPassword', 'Olt', 'Onu', 'Omci']):
            mod_name = os.path.basename(fn).replace('.py', '')
            print(f"[CAPTURED] {fn} :: {name}")
            
            # Save full disassembly
            outpath = os.path.join(DUMP_DIR, f'{mod_name}.txt')
            with open(outpath, 'w', encoding='utf-8') as f:
                f.write(f"# Decrypted from: {fn}\n")
                f.write(f"# Code name: {name}\n")
                f.write(f"# co_names: {code.co_names}\n")
                f.write(f"# co_varnames: {code.co_varnames}\n\n")
                
                def dump_code(co, indent=0):
                    pfx = "  " * indent
                    f.write(f"\n{pfx}{'='*60}\n")
                    f.write(f"{pfx}CODE: {co.co_name}\n")
                    f.write(f"{pfx}  File: {co.co_filename}\n")
                    f.write(f"{pfx}  Args: {co.co_argcount} | KWOnly: {co.co_kwonlyargcount}\n")
                    f.write(f"{pfx}  Varnames: {co.co_varnames}\n")
                    f.write(f"{pfx}  Names: {co.co_names}\n")
                    f.write(f"{pfx}  FreeVars: {co.co_freevars}\n")
                    f.write(f"{pfx}  CellVars: {co.co_cellvars}\n")
                    non_code = [c for c in co.co_consts if not isinstance(c, types.CodeType)]
                    # Truncate large byte constants
                    printable = []
                    for c in non_code:
                        if isinstance(c, bytes) and len(c) > 200:
                            printable.append(f"<bytes {len(c)}B>")
                        else:
                            printable.append(repr(c))
                    f.write(f"{pfx}  Consts: {printable}\n")
                    f.write(f"{pfx}  --- Disassembly ---\n")
                    sio = io.StringIO()
                    try:
                        dis.dis(co, file=sio)
                    except:
                        sio.write("(disassembly failed)\n")
                    for line in sio.getvalue().split('\n'):
                        f.write(f"{pfx}  {line}\n")
                    
                    for c in co.co_consts:
                        if isinstance(c, types.CodeType):
                            dump_code(c, indent + 1)
                
                dump_code(code)
            
            # Save raw .pyc
            pyc_path = os.path.join(DUMP_DIR, f'{mod_name}.pyc')
            with open(pyc_path, 'wb') as f:
                f.write(b'\x61\x0d\x0d\x0a')
                f.write(b'\x00' * 12)
                marshal.dump(code, f)
            
            dumped_modules[mod_name] = True
    
    # Call the real exec
    if globs is None:
        return _real_exec(code)
    elif locs is None:
        return _real_exec(code, globs)
    else:
        return _real_exec(code, globs, locs)

import builtins
builtins.exec = hooked_exec

# Now try to import pytransform and load each module
print("[*] Loading pytransform...")
try:
    import pytransform
    print(f"[+] pytransform loaded: {dir(pytransform)}")
    
    # Now load each encrypted module
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
        print(f"\n[*] Attempting to decrypt: {modname}")
        pyc_path = os.path.join(PYZ_DIR, f'{modname}.pyc')
        if os.path.exists(pyc_path):
            try:
                spec = importlib.util.spec_from_file_location(modname, pyc_path)
                mod = importlib.util.module_from_spec(spec)
                mod.__file__ = f'{modname}.py'
                sys.modules[modname] = mod
                spec.loader.exec_module(mod)
                print(f"[+] Decrypted {modname}")
            except Exception as e:
                print(f"[-] Failed {modname}: {e}")
        else:
            print(f"[-] Not found: {pyc_path}")
    
    print(f"\n[*] Done! Dumped {len(dumped_modules)} modules: {list(dumped_modules.keys())}")

except Exception as e:
    import traceback
    print(f"[-] Error: {e}")
    traceback.print_exc()
