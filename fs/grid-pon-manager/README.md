# GRID PON Manager

Web UI for managing FS GPON OLT SFP sticks (**v2 firmware**) over the firmware's
built-in **port-128 HTTP API**. This replaces the original v1 build, which spoke
the raw UDP binary protocol — the v2 sticks reject those frames (they require the
new per-frame hash auth), so the UI now drives the HTTP/SSE web API instead.

See [`../FIRMWARE_V2_FINDINGS.md`](../FIRMWARE_V2_FINDINGS.md) and
[`../web-api/`](../web-api/) for the reverse-engineering notes the client is based on.

## Architecture

```
Browser ──HTTP──▶ Next.js route handlers ──▶ OltWebClient (per OLT) ──HTTP/SSE──▶ OLT :128
```

- **`src/lib/olt-web-client.ts`** — TypeScript port of `web-api/olt_web_client.py`.
  One instance per OLT. Challenge/response login (`md5(password + nonce)` → token),
  a persistent SSE stream for device/ONU/command results, and the cold-stream
  trigger-retry quirk handled. Covers all 16 firmware endpoints.
- **`src/lib/olt-manager.ts`** — singleton holding one logged-in client per OLT IP
  (the embedded server is fragile under concurrent connections, so we reuse a
  long-lived client and let the browser poll our routes). Re-logs-in on token expiry.
- **`src/lib/olt-creds.ts`** — credential resolution: per-OLT secret →
  `olts.json` user → `OLT_USER`/`OLT_PW` env.
- **`src/lib/olt-store.ts`** — config store of managed OLTs (`data/olts.json`).

### API routes

| Route | Method | Purpose |
|-------|--------|---------|
| `/api/olts` | GET/POST/DELETE | list / add / remove managed OLTs |
| `/api/olts/[ip]/device` | GET | device info + OLT optics (live) |
| `/api/olts/[ip]/onus` | GET | ONU list with parsed DDM optics |
| `/api/olts/[ip]/cmd` | POST | run a firmware terminal command |
| `/api/olts/[ip]/whitelist` | GET/POST | download / upload ONU whitelist |
| `/api/olts/[ip]/config` | GET/POST | read settings / write copyright |
| `/api/olts/[ip]/firmware` | POST | flash a firmware image (destructive) |

### UI

Dashboard fleet grid → per-OLT detail with tabs: **Overview** (device optics),
**ONUs** (DDM table), **Terminal**, **Whitelist**, **Settings** (network /
provisioning), **Firmware**.

## Setup

```bash
cp .env.example .env      # set OLT_USER / OLT_PW (fleet default credentials)
bun install
bun dev                   # http://localhost:3000
```

### Credentials

- The fleet-wide default user/password come from `.env` (`OLT_USER`, `OLT_PW`).
- A per-OLT override can be entered in the **Add OLT** dialog; it is stored in
  `data/olt-secrets.json` (gitignored — never committed).
- Factory defaults: `admin`/`abcd1@`, `operator`/`op1234`, `viewer`/`view123`.

### Managed OLTs

`data/olts.json` is the config-based list of OLTs to manage (add/remove via the UI).
Automating this from NetBox or similar is a future enhancement.

## Build

```bash
bun run build
bun run lint
```
