"""
FS GPON OLT Stick — V2 firmware web API client.

Reverse-engineered from the firmware's self-contained web UI (port 128).
The entire management UI is a single HTML page with one inline script; this
client reimplements that script's API calls in Python.

Key facts about this API:
  * Auth is challenge-response: GET /api/challenge -> md5(password + nonce) -> POST /api/login.
  * The returned token is passed as a `?token=` query param on every other call.
  * Device info, ONU status, and terminal command output do NOT come back in the
    HTTP response. They are pushed asynchronously over a Server-Sent Events stream
    (GET /api/sse). The POST /api/{device,onu}/refresh and /api/cmd calls only
    *trigger* work; results arrive as SSE events. This client runs the SSE stream
    in a background thread and correlates events for you.

Only depends on `requests`.

Example:
    client = OltWebClient("100.64.2.147", "admin", "2mint503!")
    client.login()
    print(client.get_device())          # dict with olt_sn, pn, optics, ...
    for onu in client.get_onus():        # list of ONUs with parsed optics
        print(onu["sn"], onu["state"], onu["rx_pwr_dbm"])
    print(client.run_command("get_olt_pn()"))
    client.close()
"""

from __future__ import annotations

import hashlib
import json
import re
import threading
import time
import uuid
from dataclasses import dataclass, field
from queue import Empty, Queue
from typing import Any, Callable, Dict, List, Optional

import requests


def _compact(obj: Any) -> str:
    """Serialize JSON the way the browser's JSON.stringify does — no whitespace.

    The OLT's embedded HTTP server parses request bodies naively and rejects
    JSON with spaces after ':' / ',' (returns 400 'invalid request'). Always
    send compact JSON."""
    return json.dumps(obj, separators=(",", ":"))


# ── Parsing helpers for the OLT's text data formats ──

_ONU_PERF_RE = re.compile(
    r"([\d.]+)\s*V;\s*([\d.]+)\s*c;\s*([\d.]+)\s*mA;\s*tp=([-\d.]+)\s*dBm;\s*rp=([-\d.]+)\s*dBm"
)
_DEV_EXTRA_RE = re.compile(
    r"txp=([-\d.]+)\(mW\);bias=([-\d.]+)\(mA\);vol=([-\d.]+)\(v\);temp=([-\d.]+)\(degC\);alarm=(\d+)"
)


def parse_onu_perf(perf: str) -> Dict[str, Optional[float]]:
    """Parse 'perf' string e.g. '3.20 V;71.0 c;18.3 mA;tp=1.5 dBm;rp=-6.2 dBm'.

    A reading of -99 dBm is the firmware's "no DDM" sentinel (e.g. Ubiquiti ONUs)."""
    m = _ONU_PERF_RE.search(perf or "")
    if not m:
        return {"voltage": None, "temperature": None, "bias": None,
                "tx_pwr_dbm": None, "rx_pwr_dbm": None}
    v, t, b, tp, rp = (float(x) for x in m.groups())
    return {"voltage": v, "temperature": t, "bias": b, "tx_pwr_dbm": tp, "rx_pwr_dbm": rp}


def parse_device_extra(extra: str) -> Dict[str, Optional[float]]:
    """Parse device 'extra' e.g. 'txp=3.24(mW);bias=30.35(mA);vol=3.24(v);temp=41.2(degC);alarm=0'."""
    m = _DEV_EXTRA_RE.search(extra or "")
    if not m:
        return {"tx_pwr_mw": None, "bias_ma": None, "voltage": None,
                "temperature": None, "alarm": None}
    txp, bias, vol, temp, alarm = m.groups()
    return {"tx_pwr_mw": float(txp), "bias_ma": float(bias), "voltage": float(vol),
            "temperature": float(temp), "alarm": int(alarm)}


def encode_onu_sn(sn: str) -> bytes:
    """Encode a 12-char ONU SN to the 8-byte wire form: 4 ASCII chars + 4 hex bytes.

    e.g. 'TPLG31A11C1A' -> b'TPLG' + bytes.fromhex('31A11C1A')."""
    if len(sn) != 12:
        raise ValueError(f"ONU SN must be 12 chars, got {len(sn)!r}")
    return sn[:4].encode("ascii") + bytes.fromhex(sn[4:])


def decode_onu_sn(raw: bytes) -> str:
    """Decode the 8-byte wire form back to the 12-char SN."""
    if len(raw) != 8:
        raise ValueError("raw SN must be 8 bytes")
    return raw[:4].decode("ascii") + raw[4:].hex().upper()


# ── Client ──

@dataclass
class _SseState:
    thread: Optional[threading.Thread] = None
    resp: Optional[requests.Response] = None
    stop: threading.Event = field(default_factory=threading.Event)
    connected: threading.Event = field(default_factory=threading.Event)
    listeners: List[Callable[[dict], None]] = field(default_factory=list)


class OltWebClient:
    """Client for the FS GPON OLT Stick v2 firmware web API (port 128)."""

    def __init__(
        self,
        host: str,
        username: str,
        password: str,
        port: int = 128,
        timeout: float = 10.0,
        scheme: str = "http",
    ):
        self.host = host
        self.username = username
        self.password = password
        self.base_url = f"{scheme}://{host}:{port}"
        self.timeout = timeout
        self.token: Optional[str] = None
        self.session = requests.Session()
        # The long-lived SSE GET monopolizes its TCP connection; the embedded
        # server won't service a trigger POST on the same pooled connection
        # concurrently (the event never fires). Triggers go on a separate session.
        self.trigger_session = requests.Session()
        self._sse = _SseState()

    # ── auth ──

    def login(self) -> str:
        """Perform challenge-response login. Returns the session token.

        Flow: GET /api/challenge -> {nonce}; hash = md5(password + nonce);
        POST /api/login {user, hash, nonce} -> {token}."""
        ch = self.session.get(f"{self.base_url}/api/challenge", timeout=self.timeout)
        ch.raise_for_status()
        nonce = ch.json()["nonce"]
        digest = hashlib.md5((self.password + nonce).encode()).hexdigest()
        r = self.session.post(
            f"{self.base_url}/api/login",
            data=_compact({"user": self.username, "hash": digest, "nonce": nonce}),
            timeout=self.timeout,
        )
        r.raise_for_status()
        j = r.json()
        if not j.get("token"):
            raise RuntimeError(f"login failed: {j.get('error', j)}")
        self.token = j["token"]
        self._sync_time()
        return self.token

    def _q(self, path: str) -> str:
        return f"{self.base_url}{path}?token={self.token}"

    def _require_token(self):
        if not self.token:
            raise RuntimeError("not logged in; call login() first")

    def _sync_time(self):
        """POST /api/time/sync — best effort, matches what the web UI does on login."""
        local_ts = int(time.time())
        try:
            self.session.post(
                self._q("/api/time/sync"),
                data=_compact({"timestamp": local_ts}),
                headers={"Content-Type": "application/json"},
                timeout=self.timeout,
            )
        except requests.RequestException:
            pass

    # ── SSE stream (background thread) ──

    def start_sse(self, wait: float = 5.0):
        """Open the SSE stream in a background thread and block until it is actually
        connected (up to `wait` seconds). Required before get_device/get_onus/
        run_command, whose results are delivered as SSE events — triggering a refresh
        before the stream is connected would miss the one-shot response."""
        self._require_token()
        if self._sse.thread and self._sse.thread.is_alive():
            self._sse.connected.wait(timeout=wait)
            return
        self._sse.stop.clear()
        self._sse.connected.clear()
        self._sse.thread = threading.Thread(target=self._sse_loop, daemon=True)
        self._sse.thread.start()
        self._sse.connected.wait(timeout=wait)
        # The server needs a moment after the stream connects before it will route
        # events to it; without this settle the FIRST refresh/cmd trigger is lost.
        time.sleep(1.5)

    def _sse_loop(self):
        while not self._sse.stop.is_set():
            try:
                resp = self.session.get(self._q("/api/sse"), stream=True, timeout=(self.timeout, None))
                self._sse.resp = resp
                self._sse.connected.set()
                for line in resp.iter_lines(decode_unicode=True):
                    if self._sse.stop.is_set():
                        break
                    if not line or not line.startswith("data:"):
                        continue
                    payload = line[5:].strip()
                    try:
                        evt = json.loads(payload)
                    except json.JSONDecodeError:
                        continue
                    for cb in list(self._sse.listeners):
                        try:
                            cb(evt)
                        except Exception:
                            pass
            except requests.RequestException:
                pass
            if not self._sse.stop.is_set():
                time.sleep(2)  # reconnect, mirroring the UI's 5s backoff

    def _collect(self, want_types, trigger: Optional[Callable] = None,
                 stop_type: Optional[str] = None, window: float = 8.0,
                 retry_every: float = 2.5) -> List[dict]:
        """Subscribe to SSE, fire `trigger`, and collect events of the requested
        types until `stop_type` is seen or `window` elapses.

        The OLT silently drops the first one or two refresh/cmd triggers issued on a
        freshly-connected SSE stream (it only routes push events once the stream is
        'warm'). So we re-fire `trigger` every `retry_every` seconds until the first
        matching event arrives, then stop re-firing and just collect."""
        self.start_sse()
        q: "Queue[dict]" = Queue()
        want = set(want_types) | ({stop_type} if stop_type else set())

        def listener(evt):
            if evt.get("type") in want:
                q.put(evt)

        self._sse.listeners.append(listener)
        try:
            out: List[dict] = []
            got_any = False
            deadline = time.time() + window
            next_trigger = 0.0
            while time.time() < deadline:
                # (re)fire the trigger until we've seen at least one matching event
                if trigger and not got_any and time.time() >= next_trigger:
                    try:
                        trigger()
                    except requests.RequestException:
                        pass
                    next_trigger = time.time() + retry_every
                try:
                    evt = q.get(timeout=min(retry_every, max(0.05, deadline - time.time())))
                except Empty:
                    continue
                got_any = True
                if stop_type and evt.get("type") == stop_type:
                    break
                out.append(evt)
            return out
        finally:
            self._sse.listeners.remove(listener)

    # ── reads ──

    def get_user(self) -> dict:
        """GET /api/user -> {username, role, last_modified}."""
        self._require_token()
        r = self.session.get(self._q("/api/user"), timeout=self.timeout)
        r.raise_for_status()
        return r.json()

    def get_config(self) -> dict:
        """GET /api/config -> {device_name, location, vlan_id, max_onu, debug_level,
        config_flags, copyright, port_state}."""
        self._require_token()
        r = self.session.get(self._q("/api/config"), timeout=self.timeout)
        r.raise_for_status()
        return r.json()

    def get_device(self, window: float = 12.0) -> Optional[dict]:
        """Trigger a device refresh and return the device_info SSE event, with optics
        parsed. Fields: olt_sn, name, pn, status, uptime, extra(+parsed optics)."""
        events = self._collect(
            ["device_info"],
            trigger=lambda: self.trigger_session.post(self._q("/api/device/refresh"), timeout=self.timeout),
            window=window,
        )
        if not events:
            return None
        dev = events[-1]
        dev["optics"] = parse_device_extra(dev.get("extra", ""))
        return dev

    def get_onus(self, window: float = 12.0) -> List[dict]:
        """Trigger an ONU refresh and collect onu_status events until onu_list_end.
        Each ONU gets parsed optics under 'optics' plus flattened *_dbm fields."""
        events = self._collect(
            ["onu_status"],
            trigger=lambda: self.trigger_session.post(self._q("/api/onu/refresh"), timeout=self.timeout),
            stop_type="onu_list_end",
            window=window,
        )
        onus = []
        for o in events:
            optics = parse_onu_perf(o.get("perf", ""))
            o["optics"] = optics
            o.update(optics)  # convenience: tx_pwr_dbm / rx_pwr_dbm at top level
            onus.append(o)
        onus.sort(key=lambda x: x.get("id", 0))
        return onus

    # ── terminal ──

    def run_command(self, cmd: str, window: float = 12.0, retry_every: float = 2.5) -> str:
        """POST /api/cmd {cmd}; output is streamed back over SSE as one or more
        cmd_response events (chunked via the 'more' flag). Returns assembled output.

        Like the refresh calls, the cmd trigger is re-fired until output starts
        arriving (the OLT drops the first triggers on a cold SSE stream).

        Pass any terminal command from the user guide, e.g. 'get_olt_pn()',
        'show_onu()', 'get_onu_optics("TPLG31A11C1A")'. WRITE/DESTRUCTIVE commands
        (reset_system, reboot_onu, set_*) are NOT filtered here — caller beware."""
        box: Dict[str, Any] = {"buf": "", "started": False, "done": False}

        def listener(evt):
            if evt.get("type") != "cmd_response":
                return
            box["started"] = True
            box["buf"] += evt.get("output", "")
            if not evt.get("more"):
                box["done"] = True

        self.start_sse()
        self._sse.listeners.append(listener)
        try:
            deadline = time.time() + window
            next_trigger = 0.0
            while time.time() < deadline and not box["done"]:
                if not box["started"] and time.time() >= next_trigger:
                    r = self.trigger_session.post(
                        self._q("/api/cmd"),
                        data=_compact({"cmd": cmd}),
                        headers={"Content-Type": "application/json"},
                        timeout=self.timeout,
                    )
                    if not r.ok:
                        raise RuntimeError(f"cmd error: {r.json().get('error', r.text)}")
                    next_trigger = time.time() + retry_every
                time.sleep(0.1)
            return box["buf"]
        finally:
            self._sse.listeners.remove(listener)

    # ── whitelist (binary) ──

    def download_whitelist(self) -> List[dict]:
        """GET /api/onu/whitelist -> binary blob: uint16 count, then 10-byte records
        (8-byte SN + type + active). Returns list of {sn, type, active}."""
        self._require_token()
        r = self.session.get(self._q("/api/onu/whitelist"), timeout=self.timeout)
        r.raise_for_status()
        buf = r.content
        if len(buf) < 2:
            return []
        count = int.from_bytes(buf[0:2], "little")
        out = []
        for i in range(count):
            base = 2 + i * 10
            if base + 10 > len(buf):
                break
            sn = decode_onu_sn(buf[base:base + 8])
            out.append({"sn": sn, "type": buf[base + 8], "active": bool(buf[base + 9])})
        return out

    def upload_whitelist(self, entries: List[dict], batch_size: int = 100):
        """POST /api/onu/whitelist — WRITE OPERATION (persists to flash).

        entries: list of {sn: '12charSN', type: 1-5, active: bool/int}.
        Mirrors the UI's batched binary upload. Use with care on live OLTs."""
        self._require_token()
        ip = [int(x) for x in self.host.split(".")]
        if len(ip) != 4:
            raise ValueError("host must be a dotted IPv4 to build the whitelist header")
        total = len(entries)
        if total > 256:
            raise ValueError(f"too many ONUs: {total} (max 256)")
        for off in range(0, total, batch_size):
            cnt = min(batch_size, total - off)
            buf = bytearray(10 + cnt * 10)
            buf[0:4] = bytes(ip)
            buf[4:6] = total.to_bytes(2, "little")
            buf[6:8] = off.to_bytes(2, "little")
            buf[8:10] = cnt.to_bytes(2, "little")
            for j in range(cnt):
                e = entries[off + j]
                base = 10 + j * 10
                buf[base:base + 8] = encode_onu_sn(e["sn"])
                tp = int(e["type"])
                if not 1 <= tp <= 5:
                    raise ValueError(f"service type must be 1-5, got {tp}")
                buf[base + 8] = tp
                buf[base + 9] = int(bool(e.get("active", 1)))
            r = self.session.post(
                self._q("/api/onu/whitelist"),
                data=bytes(buf),
                headers={"Content-Type": "application/octet-stream"},
                timeout=self.timeout,
            )
            if not r.ok:
                raise RuntimeError(f"whitelist upload failed: {r.json().get('error', r.text)}")

    # ── teardown ──

    def close(self):
        self._sse.stop.set()
        if self._sse.resp is not None:
            try:
                self._sse.resp.close()
            except Exception:
                pass
        if self._sse.thread:
            self._sse.thread.join(timeout=3)
        self.session.close()
        self.trigger_session.close()

    def __enter__(self):
        self.login()
        return self

    def __exit__(self, *exc):
        self.close()


if __name__ == "__main__":
    import os
    import sys

    host = sys.argv[1] if len(sys.argv) > 1 else "100.64.2.147"
    user = os.environ.get("OLT_USER", "admin")
    pw = os.environ.get("OLT_PW")
    if not pw:
        sys.exit("set OLT_PW env var (and optionally OLT_USER)")

    with OltWebClient(host, user, pw) as c:
        print("user:", c.get_user())
        print("config:", c.get_config())
        dev = c.get_device()
        print("device:", dev)
        print("ONUs:")
        for o in c.get_onus():
            print(f"  [{o['id']}] {o['sn']} {o['state']}  "
                  f"tx={o['tx_pwr_dbm']} rx={o['rx_pwr_dbm']} dBm")
        print("get_olt_pn():", c.run_command("get_olt_pn()"))
