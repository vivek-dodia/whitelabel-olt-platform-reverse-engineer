import { NextRequest, NextResponse } from "next/server";
import { getOltClient, CommandCode } from "@/lib/olt-client";
import { Buffer } from "buffer";

// POST /api/olts/[ip]/command — send a raw command to the OLT
export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ ip: string }> }
) {
  const { ip } = await params;
  const body = await req.json();
  const { cmd, data } = body; // cmd = CommandCode number, data = hex string

  if (cmd === undefined) {
    return NextResponse.json(
      { error: "cmd is required (CommandCode number)" },
      { status: 400 }
    );
  }

  const client = getOltClient();
  const dataBuffer = data ? Buffer.from(data, "hex") : undefined;
  const resp = await client.sendFCmd(ip, cmd as CommandCode, dataBuffer);

  if (!resp) {
    return NextResponse.json(
      { error: "OLT not responding" },
      { status: 504 }
    );
  }

  return NextResponse.json({
    ip,
    cmd,
    response: resp.toString("hex"),
    length: resp.length,
  });
}
