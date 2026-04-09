import sys, os, types, dis, io, marshal, builtins

EXTRACT_DIR = r'C:\Users\Vivek\Downloads\volt\VOLT_Tool_V6.6.exe_extracted'
PYZ_DIR = os.path.join(EXTRACT_DIR, 'PYZ-00.pyz_extracted')
DUMP_DIR = r'C:\Users\Vivek\Downloads\volt\decrypted_source'
os.makedirs(DUMP_DIR, exist_ok=True)

sys.path.insert(0, EXTRACT_DIR)
sys.path.insert(0, PYZ_DIR)
os.add_dll_directory(EXTRACT_DIR)
sys._MEIPASS = EXTRACT_DIR

# Add PyQt5 DLLs path
qt_bin = os.path.join(EXTRACT_DIR, 'PyQt5', 'Qt5', 'bin')
if os.path.exists(qt_bin):
    os.add_dll_directory(qt_bin)
    os.environ['PATH'] = qt_bin + ';' + os.environ.get('PATH', '')

# Also add the PyQt5 directory to sys.path
pyqt5_dir = os.path.join(EXTRACT_DIR, 'PyQt5')
if os.path.exists(pyqt5_dir):
    sys.path.insert(0, os.path.join(EXTRACT_DIR))

def dump_module_contents(modname, mod_dict, outpath):
    """Dump all functions, classes, and attributes from a decrypted module dict"""
    with open(outpath, 'w', encoding='utf-8') as f:
        f.write(f"# Decrypted module: {modname}\n")
        f.write(f"# Keys: {sorted(mod_dict.keys())}\n\n")
        
        for key in sorted(mod_dict.keys()):
            if key.startswith('__') and key.endswith('__'):
                continue
            val = mod_dict[key]
            
            if isinstance(val, type):
                f.write(f"\n{'='*60}\n")
                f.write(f"CLASS: {key}\n")
                f.write(f"  Bases: {val.__bases__}\n")
                f.write(f"  MRO: {val.__mro__}\n")
                for attr_name in sorted(dir(val)):
                    if attr_name.startswith('_'):
                        continue
                    attr = getattr(val, attr_name, None)
                    if callable(attr) and hasattr(attr, '__code__'):
                        co = attr.__code__
                        f.write(f"\n  METHOD: {attr_name}\n")
                        f.write(f"    args: {co.co_varnames[:co.co_argcount]}\n")
                        f.write(f"    all_vars: {co.co_varnames}\n")
                        f.write(f"    names: {co.co_names}\n")
                        nc = [repr(c)[:150] for c in co.co_consts if not isinstance(c, types.CodeType)]
                        f.write(f"    consts: {nc}\n")
                        sio = io.StringIO()
                        try: dis.dis(co, file=sio)
                        except: sio.write("(failed)\n")
                        f.write(f"    --- disasm ---\n")
                        for line in sio.getvalue().split('\n'):
                            f.write(f"    {line}\n")
                    elif not callable(attr):
                        f.write(f"  ATTR: {attr_name} = {repr(attr)[:200]}\n")
            
            elif callable(val) and hasattr(val, '__code__'):
                co = val.__code__
                f.write(f"\n{'='*60}\n")
                f.write(f"FUNCTION: {key}\n")
                f.write(f"  args: {co.co_varnames[:co.co_argcount]}\n")
                f.write(f"  all_vars: {co.co_varnames}\n")
                f.write(f"  names: {co.co_names}\n")
                nc = [repr(c)[:150] for c in co.co_consts if not isinstance(c, types.CodeType)]
                f.write(f"  consts: {nc}\n")
                sio = io.StringIO()
                try: dis.dis(co, file=sio)
                except: sio.write("(failed)\n")
                f.write(f"  --- disasm ---\n")
                for line in sio.getvalue().split('\n'):
                    f.write(f"  {line}\n")
            
            else:
                r = repr(val)
                if len(r) > 500: r = r[:500] + '...'
                f.write(f"\nVAR: {key} = {r}\n")
        
        f.write(f"\n\n# End of {modname}\n")

# Load modules
targets = ['com_def', 'olt_socket', 'gdt_form',
           'OltInfoWidget', 'OltInfoWidget_form',
           'OnuInfoWidget', 'OnuInfoWidget_form',
           'OmciMsgWidget', 'OmciMsgWidget_form',
           'PasswordWidget', 'PasswordWidget_form',
           'MessageWidget', 'MessageWidget_form',
           'TextInputWidget', 'TextInputWidget_form',
           'WaitBobWriteWidget', 'WaitBobWriteWidget_form',
           'WaitPasswordSaveWidget']

results = {}
_real_exec = builtins.exec

for modname in targets:
    pyc_path = os.path.join(PYZ_DIR, f'{modname}.pyc')
    if not os.path.exists(pyc_path):
        continue
    
    print(f"[*] Loading {modname}...")
    try:
        with open(pyc_path, 'rb') as f:
            f.read(16)
            co = marshal.load(f)
        
        mod_dict = {
            '__name__': modname,
            '__file__': f'{modname}.py',
            '__builtins__': builtins,
            '__loader__': None,
            '__spec__': None,
        }
        
        _real_exec(co, mod_dict)
        
        # Check what got defined
        new_keys = [k for k in mod_dict.keys() if not k.startswith('__')]
        print(f"  [+] Loaded! New keys: {new_keys[:20]}")
        
        outpath = os.path.join(DUMP_DIR, f'{modname}.txt')
        dump_module_contents(modname, mod_dict, outpath)
        results[modname] = new_keys
        
    except Exception as e:
        err = str(e)
        if len(err) > 150: err = err[:150]
        print(f"  [-] {modname}: {err}")

print(f"\n{'='*60}")
print(f"Successfully loaded: {len(results)} modules")
for name, keys in results.items():
    print(f"  {name}: {keys}")
