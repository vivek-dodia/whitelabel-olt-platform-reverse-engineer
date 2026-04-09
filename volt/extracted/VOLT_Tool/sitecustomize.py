import sys, os, types, dis, io, marshal, traceback as tb

DUMP_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'dumped_code')
os.makedirs(DUMP_DIR, exist_ok=True)
dumped = set()

def dump_code(co, depth=0):
    if id(co) in dumped:
        return
    dumped.add(id(co))
    
    fn = co.co_filename.replace('\', '_').replace('/', '_').replace(':', '_').replace('<','').replace('>','')
    nm = co.co_name.replace('<','').replace('>','')
    outpath = os.path.join(DUMP_DIR, f"{fn}__{nm}.txt")
    
    try:
        with open(outpath, 'w', encoding='utf-8') as f:
            f.write(f"# File: {co.co_filename}\n# Name: {co.co_name}\n")
            f.write(f"# Args: {co.co_argcount} | Varnames: {co.co_varnames}\n")
            f.write(f"# Names: {co.co_names}\n")
            f.write(f"# Consts: {[c for c in co.co_consts if not isinstance(c, types.CodeType)]}\n\n")
            sio = io.StringIO()
            dis.dis(co, file=sio)
            f.write(sio.getvalue())
        
        # Save raw pyc too
        pyc_path = outpath.replace('.txt', '.pyc')
        with open(pyc_path, 'wb') as f:
            f.write(b'\x61\x0d\x0d\x0a\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00')
            marshal.dump(co, f)
    except:
        pass
    
    for c in co.co_consts:
        if isinstance(c, types.CodeType):
            dump_code(c, depth+1)

_orig_exec = exec

def _hooked_exec(__code, __globals=None, __locals=None, **kw):
    if isinstance(__code, types.CodeType):
        fn = getattr(__code, 'co_filename', '')
        if any(x in fn for x in ['gdt','olt_socket','com_def','Widget','obf']):
            try:
                dump_code(__code)
                with open(os.path.join(DUMP_DIR, '_log.txt'), 'a', encoding='utf-8') as log:
                    log.write(f"Dumped: {fn}::{__code.co_name}\n")
            except:
                pass
    if __globals is None:
        return _orig_exec(__code)
    elif __locals is None:
        return _orig_exec(__code, __globals)
    else:
        return _orig_exec(__code, __globals, __locals)

import builtins
builtins.exec = _hooked_exec

with open(os.path.join(DUMP_DIR, '_log.txt'), 'a', encoding='utf-8') as log:
    log.write("=== sitecustomize hooks installed ===\n")
