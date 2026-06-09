import { NextRequest, NextResponse } from "next/server";
import { oltManager } from "@/lib/olt-manager";

export const maxDuration = 300; // firmware flashing can take minutes

// POST /api/olts/[ip]/firmware — DESTRUCTIVE. Streams a raw firmware image
// (request body = octet-stream) to the OLT via header -> status-poll -> blocks.
// Returns once flashing completes. Can take minutes; can brick a stick.
export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ ip: string }> }
) {
  const { ip } = await params;
  const buf = new Uint8Array(await req.arrayBuffer());
  if (buf.length < 12) {
    return NextResponse.json(
      { error: "firmware image too small (need at least a 12-byte header)" },
      { status: 400 }
    );
  }
  try {
    await oltManager.withClient(ip, (c) => c.upgradeFirmware(buf));
    return NextResponse.json({ ip, ok: true, bytes: buf.length });
  } catch (err) {
    return NextResponse.json(
      { error: err instanceof Error ? err.message : "firmware upgrade failed" },
      { status: 502 }
    );
  }
}
