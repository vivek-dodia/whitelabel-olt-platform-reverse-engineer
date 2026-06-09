import { NextRequest, NextResponse } from "next/server";
import { loadOlts, addOlt, removeOlt, updateOlt } from "@/lib/olt-store";
import { setOltSecret, deleteOltSecret, hasOltSecret } from "@/lib/olt-creds";
import { oltManager } from "@/lib/olt-manager";

// GET /api/olts — list managed OLTs from the config store (instant; the
// dashboard fetches live device/ONU data per-OLT). `hasPassword` reflects a
// per-OLT credential override; passwords themselves are never returned.
export async function GET() {
  const olts = loadOlts().map((o) => ({ ...o, hasPassword: hasOltSecret(o.ip) }));
  return NextResponse.json(olts);
}

// POST /api/olts — add an OLT by IP. Optional per-OLT user/password override.
// Probes the OLT once to learn its serial/PN and mark it online.
export async function POST(req: NextRequest) {
  const body = await req.json();
  const { ip, name, siteLabel, user, password } = body as {
    ip?: string;
    name?: string;
    siteLabel?: string;
    user?: string;
    password?: string;
  };

  if (!ip || !name) {
    return NextResponse.json({ error: "ip and name are required" }, { status: 400 });
  }

  if (password) setOltSecret(ip, password, user);
  const olt = addOlt(ip, name, { siteLabel, user });

  // Best-effort probe so the card shows real data immediately.
  try {
    const dev = await oltManager.withClient(ip, (c) => c.getDevice());
    if (dev) {
      updateOlt(ip, {
        status: "online",
        serial: dev.olt_sn,
        pn: dev.pn,
        lastSeen: Date.now(),
        optics: {
          tx_pwr_mw: dev.optics.tx_pwr_mw,
          voltage: dev.optics.voltage,
          temperature: dev.optics.temperature,
          bias_ma: dev.optics.bias_ma,
          alarm: dev.optics.alarm,
        },
      });
    }
  } catch {
    updateOlt(ip, { status: "offline" });
  }

  const fresh = loadOlts().find((o) => o.ip === ip) ?? olt;
  return NextResponse.json({ ...fresh, hasPassword: hasOltSecret(ip) }, { status: 201 });
}

// DELETE /api/olts — remove an OLT and forget its session + stored secret.
export async function DELETE(req: NextRequest) {
  const { ip } = (await req.json()) as { ip?: string };
  if (!ip) return NextResponse.json({ error: "ip is required" }, { status: 400 });
  oltManager.invalidate(ip);
  deleteOltSecret(ip);
  const removed = removeOlt(ip);
  return NextResponse.json({ removed });
}
