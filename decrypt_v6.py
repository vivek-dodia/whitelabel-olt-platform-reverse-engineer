
import sys, os, types, dis, io, marshal, builtins

EXTRACT_DIR = r"C:\Users\Vivek\Downloads\volt\VOLT_Tool_V6.6.exe_extracted"
PYZ_DIR = os.path.join(EXTRACT_DIR, "PYZ-00.pyz_extracted")
DUMP_DIR = r"C:\Users\Vivek\Downloads\volt\decrypted_source"
os.makedirs(DUMP_DIR, exist_ok=True)

sys.path.insert(0, EXTRACT_DIR)
sys.path.insert(0, PYZ_DIR)
os.add_dll_directory(EXTRACT_DIR)
sys._MEIPASS = EXTRACT_DIR

crypto_base = os.path.join(EXTRACT_DIR, "Crypto")
for sub in ["Cipher","Hash","Math","Util","Protocol","PublicKey"]:
    p = os.path.join(crypto_base, sub)
    if os.path.exists(p): sys.path.insert(0, p)

qt_bin = os.path.join(EXTRACT_DIR, "PyQt5", "Qt5", "bin")
if os.path.exists(qt_bin):
    os.add_dll_directory(qt_bin)
    os.environ["PATH"] = qt_bin + ";" + os.environ.get("PATH", "")

all_code = {}

def safe_name(s):
    for c in "<>:\/":
        s = s.replace(c, "_")
    return s

def dump_code_obj(co, filepath):
    with open(filepath, "w", encoding="utf-8") as f:
        def _dump(c, indent=0):
            pfx = "  " * indent
            f.write(f"{pfx}" + "="*60 + "\n")
            f.write(f"{pfx}CODE: {c.co_name}\n")
            f.write(f"{pfx}  file: {c.co_filename}\n")
            f.write(f"{pfx}  argcount: {c.co_argcount}\n")
            f.write(f"{pfx}  varnames: {c.co_varnames}\n")
            f.write(f"{pfx}  names: {c.co_names}\n")
            f.write(f"{pfx}  freevars: {c.co_freevars}\n")
            f.write(f"{pfx}  cellvars: {c.co_cellvars}\n")
            nc = []
            for x in c.co_consts:
                if isinstance(x, types.CodeType): nc.append(f"<code:{x.co_name}>")
                else:
                    r = repr(x)
                    nc.append(r[:300] if len(r) > 300 else r)
            f.write(f"{pfx}  consts: {nc}\n\n")
            sio = io.StringIO()
            try: dis.dis(c, file=sio)
            except: sio.write("(failed)\n")
            for line in sio.getvalue().split("\n"): f.write(f"{pfx}  {line}\n")
            f.write("\n")
            for x in c.co_consts:
                if isinstance(x, types.CodeType): _dump(x, indent+1)
        _dump(co)

def trace_func(frame, event, arg):
    co = frame.f_code
    fn = co.co_filename or ""
    if "frozen" in fn or any(x in fn for x in ["gdt","olt_socket","com_def","Widget",
            "Password","Message","TextInput","WaitBob","WaitPassword",
            "OltInfo","OnuInfo","OmciMsg"]):
        key = f"{fn}::{co.co_name}"
        if key not in all_code:
            all_code[key] = co
            try:
                sn = safe_name(fn)
                cn = safe_name(co.co_name)
                dump_code_obj(co, os.path.join(DUMP_DIR, f"{sn}__{cn}.txt"))
            except: pass
            try:
                pyc_path = os.path.join(DUMP_DIR, f"{sn}__{cn}.pyc")
                with open(pyc_path, "wb") as pf:
                    pf.write(b"\x61\x0d\x0d\x0a" + b"\x00"*12)
                    marshal.dump(co, pf)
            except: pass
    return trace_func

sys.settrace(trace_func)
_real_exec = builtins.exec

targets = ["com_def","olt_socket","gdt_form","OltInfoWidget","OltInfoWidget_form",
           "OnuInfoWidget","OnuInfoWidget_form","OmciMsgWidget","OmciMsgWidget_form",
           "PasswordWidget","PasswordWidget_form","MessageWidget","MessageWidget_form",
           "TextInputWidget","TextInputWidget_form","WaitBobWriteWidget",
           "WaitBobWriteWidget_form","WaitPasswordSaveWidget"]

loaded = {}
for modname in targets:
    pyc_path = os.path.join(PYZ_DIR, f"{modname}.pyc")
    if not os.path.exists(pyc_path): continue
    try:
        with open(pyc_path, "rb") as f:
            f.read(16)
            co = marshal.load(f)
        mod_dict = {"__name__":modname, "__file__":f"{modname}.py", "__builtins__":builtins}
        _real_exec(co, mod_dict)
        real = {k:v for k,v in mod_dict.items() if not k.startswith("__")}
        loaded[modname] = real
        for k,v in real.items():
            if isinstance(v, type):
                for a in dir(v):
                    attr = getattr(v, a, None)
                    if callable(attr) and hasattr(attr, "__code__"):
                        c = attr.__code__
                        key2 = f"{c.co_filename}::{c.co_name}"
                        if key2 not in all_code:
                            all_code[key2] = c
                            try:
                                sn2 = safe_name(c.co_filename)
                                cn2 = safe_name(c.co_name)
                                dump_code_obj(c, os.path.join(DUMP_DIR, f"{sn2}__{cn2}.txt"))
                            except: pass
    except Exception as e:
        print(f"[-] {modname}: {str(e)[:150]}")

sys.settrace(None)
print(f"\nCaptured {len(all_code)} code objects:")
for key in sorted(all_code.keys()):
    co = all_code[key]
    print(f"  {key}: names={co.co_names[:10]}")
print(f"\nLoaded {len(loaded)} modules:")
for name, items in loaded.items():
    print(f"  {name}: {{{', '.join(k+':'+type(v).__name__ for k,v in items.items())}}}")
