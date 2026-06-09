import { NextRequest, NextResponse } from "next/server";
import { oltManager } from "@/lib/olt-manager";

// GET /api/olts/[ip]/network — IP mode (static/DHCP) + IP/mask/gateway/MAC,
// parsed from the firmware terminal commands.
export async function GET(
  _req: NextRequest,
  { params }: { params: Promise<{ ip: string }> }
) {
  const { ip } = await params;
  try {
    const network = await oltManager.withClient(ip, (c) => c.getNetworkInfo());
    return NextResponse.json({ ip, network });
  } catch (err) {
    return NextResponse.json(
      { error: err instanceof Error ? err.message : "OLT not responding" },
      { status: 504 }
    );
  }
}
