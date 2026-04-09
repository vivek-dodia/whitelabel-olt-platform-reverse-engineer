import { NextRequest, NextResponse } from "next/server";
import { getOltClient } from "@/lib/olt-client";
import { updateOlt } from "@/lib/olt-store";

// GET /api/olts/[ip]/status — get OLT status via shake_hand
export async function GET(
  _req: NextRequest,
  { params }: { params: Promise<{ ip: string }> }
) {
  const { ip } = await params;
  const client = getOltClient();
  const info = await client.shakeHand(ip);

  if (info) {
    updateOlt(ip, {
      status: "online",
      serial: info.serial,
      mac: info.mac,
      lastSeen: Date.now(),
    });
    return NextResponse.json(info);
  }

  updateOlt(ip, { status: "offline" });
  return NextResponse.json(
    { ip, status: "offline", error: "OLT not responding" },
    { status: 504 }
  );
}
