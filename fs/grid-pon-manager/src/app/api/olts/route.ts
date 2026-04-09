import { NextRequest, NextResponse } from "next/server";
import { loadOlts, addOlt, removeOlt, updateOlt } from "@/lib/olt-store";
import { getOltClient, getCachedStatus, startPoller } from "@/lib/olt-client";

// Start background poller on first request
let pollerInitialized = false;
function ensurePoller() {
  if (pollerInitialized) return;
  pollerInitialized = true;
  startPoller(() => loadOlts().map((o) => o.ip));
}

// GET /api/olts — list all managed OLTs with cached live status
export async function GET() {
  ensurePoller();

  const olts = loadOlts();
  const results = olts.map((olt) => {
    const cached = getCachedStatus(olt.ip);
    if (cached) {
      return {
        ...olt,
        status: cached.status,
        serial: cached.serial || olt.serial,
        mac: cached.mac || olt.mac,
        lastSeen: cached.lastSeen || olt.lastSeen,
      };
    }
    return olt;
  });

  return NextResponse.json(results);
}

// POST /api/olts — add a new OLT by IP
export async function POST(req: NextRequest) {
  ensurePoller();

  const body = await req.json();
  const { ip, name, siteLabel } = body;

  if (!ip || !name) {
    return NextResponse.json(
      { error: "ip and name are required" },
      { status: 400 }
    );
  }

  const client = getOltClient();
  const info = await client.shakeHand(ip);

  const olt = addOlt(ip, name, siteLabel);
  if (info) {
    updateOlt(ip, {
      serial: info.serial,
      mac: info.mac,
      status: "online",
      lastSeen: Date.now(),
    });
  }

  return NextResponse.json({ ...olt, ...info }, { status: 201 });
}

// DELETE /api/olts — remove an OLT
export async function DELETE(req: NextRequest) {
  const { ip } = await req.json();
  if (!ip) {
    return NextResponse.json({ error: "ip is required" }, { status: 400 });
  }
  const removed = removeOlt(ip);
  return NextResponse.json({ removed });
}
