import { NextRequest, NextResponse } from "next/server";
import { oltManager } from "@/lib/olt-manager";

// GET /api/olts/[ip]/config — device/network settings.
export async function GET(
  _req: NextRequest,
  { params }: { params: Promise<{ ip: string }> }
) {
  const { ip } = await params;
  try {
    const config = await oltManager.withClient(ip, (c) => c.getConfig());
    return NextResponse.json({ ip, config });
  } catch (err) {
    return NextResponse.json(
      { error: err instanceof Error ? err.message : "OLT not responding" },
      { status: 504 }
    );
  }
}

// POST /api/olts/[ip]/config — WRITE. The firmware only accepts { copyright }.
export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ ip: string }> }
) {
  const { ip } = await params;
  const body = await req.json();
  const copyright = typeof body?.copyright === "string" ? body.copyright : undefined;
  if (copyright === undefined) {
    return NextResponse.json({ error: "copyright is required" }, { status: 400 });
  }
  try {
    const result = await oltManager.withClient(ip, (c) => c.setConfig(copyright));
    return NextResponse.json({ ip, ok: true, result });
  } catch (err) {
    return NextResponse.json(
      { error: err instanceof Error ? err.message : "config write failed" },
      { status: 502 }
    );
  }
}
