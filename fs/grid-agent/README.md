# GRID Agent

Lightweight site agent for managing FS GPON OLT SFP sticks. Runs on any Linux machine on the same subnet as the OLTs, exposes a REST API for the GRID PON Manager controller.

## Quick Start

```bash
# Build
go build -o grid-agent ./cmd/grid-agent

# Run
./grid-agent -site "my-site" -token "my-secret"

# Or with Docker
docker build -t grid-agent .
docker run -d --net=host -e AUTH_TOKEN=my-secret grid-agent
```

## API

All endpoints require `Authorization: Bearer <token>` header.

### Agent Info
```
GET /
```

### OLT Management
```
GET    /api/olts              — List all tracked OLTs
POST   /api/olts/discover     — Add OLT by IP {"ip": "100.64.2.200"}
POST   /api/olts/scan         — Scan subnet {"subnet": "100.64.2"}
DELETE /api/olts/{ip}         — Remove OLT from tracking
GET    /api/olts/{ip}         — Get OLT status (live shake_hand)
POST   /api/olts/{ip}/auth    — Enable read/write {"mode": "both"}
GET    /api/olts/{ip}/onus    — Get connected ONUs
GET    /api/olts/{ip}/alarms  — Get OLT alarms
POST   /api/olts/{ip}/command — Send raw command {"cmd": 12, "data": ""}
```

## Config

`config.json`:
```json
{
  "listen_addr": ":8420",
  "auth_token": "grid-agent-secret",
  "agent_name": "grid-agent",
  "site_label": "office-lab",
  "poll_interval_seconds": 5
}
```

Or use CLI flags: `-listen :8420 -token secret -site my-site`

## Architecture

```
GRID Controller (Next.js)
  |
  |-- REST API over ZeroTier/WAN
  |
  ├── Site A: grid-agent :8420 ──UDP 64219/64218──> OLTs
  ├── Site B: grid-agent :8420 ──UDP 64219/64218──> OLTs
  └── Site C: grid-agent :8420 ──UDP 64219/64218──> OLTs
```

No NAT rules needed. The agent handles L2 ARP discovery and asymmetric UDP ports locally.
