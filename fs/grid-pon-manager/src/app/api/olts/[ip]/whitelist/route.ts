import { NextRequest, NextResponse } from "next/server";
import { oltManager } from "@/lib/olt-manager";
import type { WhitelistEntry } from "@/lib/olt-web-client";

// GET /api/olts/[ip]/whitelist — download the ONU whitelist (parsed records).
export async function GET(
  _req: NextRequest,
  { params }: { params: Promise<{ ip: string }> }
) {
  const { ip } = await params;
  try {
    const entries = await oltManager.withClient(ip, (c) => c.downloadWhitelist());
    return NextResponse.json({ ip, count: entries.length, entries });
  } catch (err) {
    return NextResponse.json(
      { error: err instanceof Error ? err.message : "OLT not responding" },
      { status: 504 }
    );
  }
}

// POST /api/olts/[ip]/whitelist — WRITE (persists to flash). Body: { entries:
// [{ sn, type, active }] }. Replaces the whitelist with the supplied entries.
export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ ip: string }> }
) {
  const { ip } = await params;
  const body = await req.json();
  const entries = body?.entries as WhitelistEntry[] | undefined;
  if (!Array.isArray(entries)) {
    return NextResponse.json({ error: "entries array is required" }, { status: 400 });
  }
  try {
    await oltManager.withClient(ip, (c) => c.uploadWhitelist(entries));
    return NextResponse.json({ ip, ok: true, count: entries.length });
  } catch (err) {
    return NextResponse.json(
      { error: err instanceof Error ? err.message : "whitelist upload failed" },
      { status: 502 }
    );
  }
}
