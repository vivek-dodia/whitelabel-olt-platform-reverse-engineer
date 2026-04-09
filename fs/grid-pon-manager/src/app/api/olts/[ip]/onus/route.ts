import { NextRequest, NextResponse } from "next/server";
import { getOltClient } from "@/lib/olt-client";

// GET /api/olts/[ip]/onus — get ONU list and status
export async function GET(
  _req: NextRequest,
  { params }: { params: Promise<{ ip: string }> }
) {
  const { ip } = await params;
  const client = getOltClient();

  const resp = await client.getOnuStatus(ip);
  if (!resp) {
    return NextResponse.json(
      { error: "OLT not responding" },
      { status: 504 }
    );
  }

  return NextResponse.json({
    ip,
    raw: resp.toString("hex"),
    length: resp.length,
  });
}
