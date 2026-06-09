import { NextRequest, NextResponse } from "next/server";
import { oltManager } from "@/lib/olt-manager";

// POST /api/olts/[ip]/cmd — run a firmware terminal command (function-call
// syntax, e.g. get_olt_pn(), show_onu(), get_onu_optics("SN")). Output is
// collected from the SSE cmd_response stream and returned as text.
// WRITE/DESTRUCTIVE commands (reset_system, reboot_onu, set_*) are not filtered.
export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ ip: string }> }
) {
  const { ip } = await params;
  const body = await req.json();
  const cmd = typeof body?.cmd === "string" ? body.cmd.trim() : "";
  if (!cmd) {
    return NextResponse.json({ error: "cmd (command string) is required" }, { status: 400 });
  }
  try {
    const output = await oltManager.withClient(ip, (c) => c.runCommand(cmd));
    return NextResponse.json({ ip, cmd, output });
  } catch (err) {
    return NextResponse.json(
      { error: err instanceof Error ? err.message : "command failed" },
      { status: 504 }
    );
  }
}
