import { NextRequest, NextResponse } from "next/server";
import { oltManager } from "@/lib/olt-manager";
import { updateOlt } from "@/lib/olt-store";

// GET /api/olts/[ip]/device — live OLT device info + optics (via SSE refresh).
// Updates the store's cached status/optics so the dashboard renders fast.
export async function GET(
  _req: NextRequest,
  { params }: { params: Promise<{ ip: string }> }
) {
  const { ip } = await params;
  try {
    const dev = await oltManager.withClient(ip, (c) => c.getDevice());
    if (!dev) {
      updateOlt(ip, { status: "offline" });
      return NextResponse.json({ ip, status: "offline", error: "no device_info" }, { status: 504 });
    }
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
    return NextResponse.json({ ip, status: "online", ...dev });
  } catch (err) {
    updateOlt(ip, { status: "offline" });
    return NextResponse.json(
      { ip, status: "offline", error: err instanceof Error ? err.message : "unreachable" },
      { status: 504 }
    );
  }
}
