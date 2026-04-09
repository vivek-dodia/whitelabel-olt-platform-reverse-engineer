import sys, os, types, io

LOG = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'trace_log.txt')
logf = open(LOG, 'w', encoding='utf-8')

def trace_calls(frame, event, arg):
    fn = frame.f_code.co_filename
    # Only trace app code
    if any(x in fn for x in ['gdt', 'olt', 'com_def', 'Widget', 'obf', 'volt']):
        if event == 'call':
            name = frame.f_code.co_name
            args = {}
            for k in list(frame.f_locals.keys())[:10]:
                v = frame.f_locals[k]
                try:
                    args[k] = repr(v)[:100]
                except:
                    args[k] = str(type(v))
            logf.write(f"CALL {fn}::{name} args={args}\n")
            logf.flush()
        elif event == 'return':
            name = frame.f_code.co_name
            try:
                ret = repr(arg)[:200]
            except:
                ret = str(type(arg))
            logf.write(f"RET  {fn}::{name} -> {ret}\n")
            logf.flush()
    return trace_calls

sys.settrace(trace_calls)
print(f"[*] Trace hook installed, logging to {LOG}")
