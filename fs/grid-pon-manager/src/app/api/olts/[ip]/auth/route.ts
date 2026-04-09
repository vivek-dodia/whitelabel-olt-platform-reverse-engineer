import { NextRequest, NextResponse } from "next/server";
import { getOltClient } from "@/lib/olt-client";

// POST /api/olts/[ip]/auth — enable read/write access
export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ ip: string }> }
) {
  const { ip } = await params;
  const body = await req.json();
  const { mode } = body; // "read" | "write" | "both"

  const client = getOltClient();
  const results: Record<string, boolean> = {};

  if (mode === "write" || mode === "both") {
    results.write = await client.enableWrite(ip);
  }
  if (mode === "read" || mode === "both") {
    results.read = await client.enableRead(ip);
  }

  return NextResponse.json({ ip, auth: results });
}
