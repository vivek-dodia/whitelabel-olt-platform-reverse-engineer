# FS GPON OLT Stick — V2 Web API (port 128)

Reverse-engineered HTTP API of the FS GPON OLT Stick v2 firmware, plus a working
Python client ([olt_web_client.py](olt_web_client.py)).

This is the **second of the two v2 management paths** — see
[../FIRMWARE_V2_FINDINGS.md](../FIRMWARE_V2_FINDINGS.md) for the binary protocol and
the overall v2 picture. The web API is served by the OLT firmware itself and is
fully independent of the Windows app.

## How it was captured

The entire web UI is a single self-contained HTML page (~32 KB) with one ~21 KB
inline `<script>` — no external JS. The capture (via the `reverse-api-engineer`
plugin's Playwright MCP + HAR) plus reading that inline script gave the complete
API. Raw artifacts in [captures/](captures/).

## Auth — challenge/response + token

```
GET  /api/challenge                      -> {"nonce": "<hex16>"}
hash = md5(password + nonce)             (lowercase hex)
POST /api/login   {"user","hash","nonce"} -> {"status":"ok","token":"<hex8>"}
```

The `token` is then passed as a **`?token=`** query parameter on every other request.
Roles: `admin`, `operator`, `viewer` (from `/api/user`).

### Two gotchas that will waste your afternoon

1. **Compact JSON only.** The embedded HTTP server parses request bodies naively and
   rejects JSON containing spaces after `:` / `,` with `400 {"error":"invalid request"}`.
   Serialize with no whitespace (`json.dumps(obj, separators=(",", ":"))`), exactly
   like the browser's `JSON.stringify`.

2. **Results arrive over SSE, not in the HTTP response.** Device info, ONU status and
   terminal-command output are pushed asynchronously on the `GET /api/sse` stream. The
   `device/refresh`, `onu/refresh`, and `cmd` POSTs only *trigger* work and return
   `200` with no useful body.

   Two further quirks of that SSE channel:
   - **Use a separate TCP connection for triggers.** The long-lived SSE GET monopolizes
     its connection; a trigger POST issued on the same connection pool won't be serviced
     and the event never fires. The client uses a dedicated `requests.Session` for SSE
     and another for everything else.
   - **The first one or two triggers on a freshly-connected SSE stream are dropped.**
     The server only routes push events once the stream is "warm" (a few seconds / a
     couple of triggers). The client re-fires each trigger every ~2.5 s until the first
     matching event arrives.

## Endpoints

| Method | Path | Body | Result | Notes |
|--------|------|------|--------|-------|
| GET  | `/api/challenge` | — | `{nonce}` | start of login |
| POST | `/api/login` | `{user,hash,nonce}` | `{status,token}` | `hash=md5(pw+nonce)` |
| GET  | `/api/user?token=` | — | `{username,role,last_modified}` | |
| GET  | `/api/config?token=` | — | `{device_name,location,vlan_id,max_onu,debug_level,config_flags,copyright,port_state[8]}` | read settings |
| POST | `/api/config?token=` | `{copyright}` | `{}` | write (only copyright observed) |
| POST | `/api/time/sync?token=` | `{timestamp}` | — | unix seconds, local-adjusted |
| GET  | `/api/sse?token=` | — | event stream | realtime push channel |
| POST | `/api/device/refresh?token=` | — | `200` | → `device_info` SSE event |
| POST | `/api/onu/refresh?token=` | — | `200` | → `onu_status` events + `onu_list_end` |
| POST | `/api/cmd?token=` | `{cmd}` | `200` | → `cmd_response` events (chunked) |
| GET  | `/api/onu/whitelist?token=` | — | binary | download whitelist (see format) |
| POST | `/api/onu/whitelist?token=` | binary | `{}` | **write** — upload whitelist (batched) |
| POST | `/api/user/changepwd?token=` | `{old_password,new_password}` | `{}` | **write** |
| POST | `/api/user/reset?token=` | `{username}` | `{message}` | **write**, admin only |
| POST | `/api/fw/header?token=` | 12 bytes (octet-stream) | `{}` | **firmware** — start upgrade |
| GET  | `/api/fw/status?token=` | — | `{state[,error]}` | poll: `receiving`/`error`/… |
| POST | `/api/fw/block?token=` | ≤1024 bytes (octet-stream) | `{status}` | **firmware** — data chunk |
| POST | `/api/fw/cancel?token=` | — | `{}` | cancel upgrade |

## SSE event types (`GET /api/sse`)

Each line is `data: {json}`. Observed `type` values:

| type | fields | meaning |
|------|--------|---------|
| `device_info` | `olt_sn, name, pn, status, uptime, extra` | OLT info + optics (`extra`) |
| `onu_status` | `id, sn, state, uptime, perf` | one ONU; `perf` = optics string |
| `onu_list_end` | — | end of an ONU refresh batch |
| `cmd_response` | `cmd_id, output, more` | terminal output; `more:true` = more chunks follow |
| `fw_progress` | `progress` | firmware upgrade % |
| `fw_error` / `fw_complete` | `error` / `message` | firmware result |
| `wl_result` | `ok, count, error, sn` | whitelist flash-write result |

### Data string formats

**Device `extra`:**
```
txp=3.22(mW);bias=30.14(mA);vol=3.25(v);temp=41.5(degC);alarm=0
```
**ONU `perf`:**
```
3.20 V;71.0 c;18.3 mA;tp=1.5 dBm;rp=-6.2 dBm
   V    temp   bias    tx_pwr    rx_pwr
```
A reading of **-99 dBm** is the "no DDM" sentinel — e.g. Ubiquiti ONUs report online but
expose no optics. (TP-Link ONUs report full optics.)

### ONU states

`REG_DONE, AUTH_DONE, OMCI_DONE, ETH_CFG, ETH_DONE, ON_LINE, LOSFI, OFF_LINE, LONGLU, ETH_CFGING, ETH_CFGING_DOWN, ONU_FRQ, DYING_GASP, POWER_OFF` (the web UI also shows `online`/`offline` for the device).

## Whitelist binary format

**Download** (`GET /api/onu/whitelist`): `uint16 count` (little-endian) followed by
`count` × 10-byte records:
```
[0:8]  ONU SN  — 4 ASCII chars + 4 raw hex bytes  (e.g. "TPLG" + 31 A1 1C 1A = TPLG31A11C1A)
[8]    service type (1–5)
[9]    active (0/1)
```

**Upload** (`POST /api/onu/whitelist`, batched ≤100 records): each batch is a 10-byte
header + records:
```
[0:4]  device IP octets
[4:6]  total ONU count   (uint16 LE)
[6:8]  offset            (uint16 LE)
[8:10] count in batch    (uint16 LE)
then count × 10-byte records (same layout as download)
```

## Terminal commands

`POST /api/cmd {cmd}` runs any of the firmware terminal commands (function-call syntax),
output streamed back via `cmd_response`. Full list in
[../FIRMWARE_V2_FINDINGS.md](../FIRMWARE_V2_FINDINGS.md). Read-only examples:
`get_olt_pn()`, `show_onu()`, `get_onu_optics("TPLG31A11C1A")`, `onu_offline_cause("SN")`,
`get_white_list_type()`. **Write/destructive** (`reset_system()`, `reboot_onu()`, `set_*`)
are not filtered by the client — caller beware.

## Python client usage

```python
from olt_web_client import OltWebClient

with OltWebClient("100.64.2.147", "admin", "<password>") as c:   # logs in on enter
    print(c.get_user())
    print(c.get_config())
    print(c.get_device())                 # dict incl. parsed 'optics'
    for onu in c.get_onus():
        print(onu["id"], onu["sn"], onu["state"], onu["tx_pwr_dbm"], onu["rx_pwr_dbm"])
    print(c.run_command("get_olt_pn()"))
    print(c.download_whitelist())
```

Or run it directly against an OLT:
```bash
OLT_PW='<password>' OLT_USER=admin python3 olt_web_client.py 100.64.2.147
```

Only dependency: `requests`.

> **Operational note:** the embedded server is fragile under concurrent connections and
> rapid reconnects. Keep one SSE stream open for the lifetime of the client and avoid
> hammering it — repeated connect/disconnect cycles can wedge SSE delivery until the
> server reaps stale sessions (~tens of seconds).

## Tested against

FS GPON OLT Stick v2 firmware, OLT SN `C2604649438` (`100.64.2.147:128`), `FS PON Manager`
firmware era, 2026-06-08. Login confirmed, device/ONU/optics/command paths all working live.
