"""
Bytecode dumper - patches Python's code execution to dump decrypted code objects.
This intercepts exec/eval to capture code after PyArmor decrypts it.
"""
import sys, os, types, dis, io, marshal

DUMP_DIR = os.path.join(os.path.dirname(__file__), 'dumped_code')
os.makedirs(DUMP_DIR, exist_ok=True)

# Track what we've already dumped
dumped = set()

def dump_code_object(co, prefix=''):
    """Recursively dump a code object and its nested code objects"""
    key = (co.co_filename, co.co_name, co.co_firstlineno if hasattr(co, 'co_firstlineno') else 0)
    if key in dumped:
        return
    dumped.add(key)
    
    safe_filename = co.co_filename.replace('\', '_').replace('/', '_').replace(':', '_')
    safe_name = co.co_name.replace('<', '').replace('>', '')
    outname = f"{safe_filename}__{safe_name}.txt"
    outpath = os.path.join(DUMP_DIR, outname)
    
    with open(outpath, 'w', encoding='utf-8') as f:
        f.write(f"# Filename: {co.co_filename}\n")
        f.write(f"# Name: {co.co_name}\n")
        f.write(f"# Argcount: {co.co_argcount}\n")
        f.write(f"# Varnames: {co.co_varnames}\n")
        f.write(f"# Names: {co.co_names}\n")
        f.write(f"# Constants (non-code): {[c for c in co.co_consts if not isinstance(c, types.CodeType)]}\n")
        f.write(f"# Freevars: {co.co_freevars}\n")
        f.write(f"# Cellvars: {co.co_cellvars}\n\n")
        
        # Disassemble
        sio = io.StringIO()
        try:
            dis.dis(co, file=sio)
        except:
            sio.write("(disassembly failed)")
        f.write(sio.getvalue())
    
    # Also save the raw .pyc
    pyc_path = os.path.join(DUMP_DIR, outname.replace('.txt', '.pyc'))
    with open(pyc_path, 'wb') as f:
        f.write(b'\x61\x0d\x0d\x0a')  # Python 3.9 magic
        f.write(b'\x00' * 12)
        marshal.dump(co, f)
    
    print(f"[DUMP] {co.co_filename}::{co.co_name}")
    
    # Recurse into nested code objects
    for const in co.co_consts:
        if isinstance(const, types.CodeType):
            dump_code_object(const, prefix + '  ')

# Patch exec to intercept decrypted code
original_exec = exec
def hooked_exec(code, *args, **kwargs):
    if isinstance(code, types.CodeType):
        # Check if it's an app module (not stdlib)
        if code.co_filename and ('gdt' in code.co_filename or 'olt' in code.co_filename or 
            'com_def' in code.co_filename or 'Widget' in code.co_filename or 
            'obf' in code.co_filename or 'volt' in code.co_filename):
            dump_code_object(code)
    return original_exec(code, *args, **kwargs)

import builtins
builtins.exec = hooked_exec
print("[*] Bytecode dumper installed - intercepting exec()")
