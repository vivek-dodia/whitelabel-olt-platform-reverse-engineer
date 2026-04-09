import sys, os, types, dis, io, marshal, builtins

EXTRACT_DIR = r'C:\Users\Vivek\Downloads\volt\VOLT_Tool_V6.6.exe_extracted'
PYZ_DIR = os.path.join(EXTRACT_DIR, 'PYZ-00.pyz_extracted')
DUMP_DIR = r'C:\Users\Vivek\Downloads\volt\decrypted_source'

sys.path.insert(0, EXTRACT_DIR)
sys.path.insert(0, PYZ_DIR)
os.add_dll_directory(EXTRACT_DIR)
sys._MEIPASS = EXTRACT_DIR

all_code = {}

def safe_name(s):
    for c in '<>:\/':
        s = s.replace(c, '_')
    return s

def trace_func(frame, event, arg):
    co = frame.f_code
    fn = co.co_filename or ''
    if 'frozen' in fn or 'olt_socket' in fn or 'com_def' in fn:
        key = f"{fn}::{co.co_name}"
        if key not in all_code:
            all_code[key] = co
            sn = safe_name(fn)
            cn = safe_name(co.co_name)
            outpath = os.path.join(DUMP_DIR, f'{sn}__{cn}.txt')
            try:
                with open(outpath, 'w', encoding='utf-8') as f:
                    def _dump(c, indent=0):
                        pfx = "  " * indent
                        f.write(f"{pfx}{'='*60}\n")
                        f.write(f"{pfx}CODE: {c.co_name}\n")
                        f.write(f"{pfx}  argcount: {c.co_argcount}\n")
                        f.write(f"{pfx}  varnames: {c.co_varnames}\n")
                        f.write(f"{pfx}  names: {c.co_names}\n")
                        f.write(f"{pfx}  freevars: {c.co_freevars}, cellvars: {c.co_cellvars}\n")
                        nc = []
                        for x in c.co_consts:
                            if isinstance(x, types.CodeType): nc.append(f"<code:{x.co_name}>")
                            else:
                                r = repr(x)
                                nc.append(r[:500] if len(r) > 500 else r)
                        f.write(f"{pfx}  consts: {nc}\n\n")
                        sio = io.StringIO()
                        try: dis.dis(c, file=sio)
                        except: sio.write("(failed)\n")
                        for line in sio.getvalue().split('\n'): f.write(f"{pfx}  {line}\n")
                        f.write('\n')
                        for x in c.co_consts:
                            if isinstance(x, types.CodeType): _dump(x, indent+1)
                    _dump(co)
            except: pass
    return trace_func

sys.settrace(trace_func)
_real_exec = builtins.exec

# Load com_def first (no GUI deps)
for modname in ['com_def', 'olt_socket']:
    pyc_path = os.path.join(PYZ_DIR, f'{modname}.pyc')
    with open(pyc_path, 'rb') as f:
        f.read(16)
        co = marshal.load(f)
    mod_dict = {'__name__': modname, '__file__': f'{modname}.py', '__builtins__': builtins}
    try:
        _real_exec(co, mod_dict)
        
        # Inspect what was defined
        for k, v in mod_dict.items():
            if k.startswith('__'): continue
            if isinstance(v, type):
                print(f"[{modname}] class {k}: {[a for a in dir(v) if not a.startswith('_')]}")
                for attr_name in dir(v):
                    attr = getattr(v, attr_name, None)
                    if callable(attr) and hasattr(attr, '__code__'):
                        co2 = attr.__code__
                        key2 = f"{co2.co_filename}::{co2.co_name}"
                        if key2 not in all_code:
                            all_code[key2] = co2
                            sn = safe_name(co2.co_filename)
                            cn = safe_name(co2.co_name)
                            try:
                                with open(os.path.join(DUMP_DIR, f'{sn}__{cn}.txt'), 'w', encoding='utf-8') as f:
                                    def _dump2(c, indent=0):
                                        pfx = "  " * indent
                                        f.write(f"{pfx}CODE: {c.co_name}\n")
                                        f.write(f"{pfx}  varnames: {c.co_varnames}\n")
                                        f.write(f"{pfx}  names: {c.co_names}\n")
                                        nc = [repr(x)[:500] if not isinstance(x, types.CodeType) else f"<code:{x.co_name}>" for x in c.co_consts]
                                        f.write(f"{pfx}  consts: {nc}\n\n")
                                        sio = io.StringIO()
                                        try: dis.dis(c, file=sio)
                                        except: pass
                                        for line in sio.getvalue().split('\n'): f.write(f"{pfx}  {line}\n")
                                        for x in c.co_consts:
                                            if isinstance(x, types.CodeType): _dump2(x, indent+1)
                                    _dump2(co2)
                            except: pass
            elif callable(v) and hasattr(v, '__code__'):
                print(f"[{modname}] func {k}: args={v.__code__.co_varnames[:v.__code__.co_argcount]}")
            elif not callable(v):
                print(f"[{modname}] var {k} = {repr(v)[:200]}")
    except Exception as e:
        print(f"[-] {modname}: {e}")

sys.settrace(None)
print(f"\nTotal captured: {len(all_code)} code objects")
