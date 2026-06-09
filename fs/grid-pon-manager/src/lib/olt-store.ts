/**
 * GRID PON Manager — OLT Store
 *
 * Config-based store for managed OLTs (data/olts.json). Tracks which OLTs to
 * manage, an optional per-OLT username, and the last known live status/optics
 * so the dashboard renders instantly before a fresh poll completes.
 *
 * Passwords are NOT stored here (this file is committed) — see olt-creds.ts.
 */

import { readFileSync, writeFileSync, existsSync, mkdirSync } from "fs";
import path from "path";

export interface ManagedOlt {
  ip: string;
  name: string;
  /** Optional per-OLT username override; password lives in olt-secrets.json. */
  user?: string;
  /** OLT serial number, learned from the device on first contact. */
  serial?: string;
  pn?: string;
  status: "online" | "offline" | "unknown";
  lastSeen?: number;
  addedAt: number;
  siteLabel?: string;
  /** Cached device optics summary from the last successful poll. */
  optics?: {
    tx_pwr_mw: number | null;
    voltage: number | null;
    temperature: number | null;
    bias_ma: number | null;
    alarm: number | null;
  };
}

const STORE_PATH = path.join(process.cwd(), "data", "olts.json");

function ensureDir() {
  const dir = path.dirname(STORE_PATH);
  if (!existsSync(dir)) mkdirSync(dir, { recursive: true });
}

export function loadOlts(): ManagedOlt[] {
  ensureDir();
  if (!existsSync(STORE_PATH)) return [];
  try {
    return JSON.parse(readFileSync(STORE_PATH, "utf-8"));
  } catch {
    return [];
  }
}

export function saveOlts(olts: ManagedOlt[]) {
  ensureDir();
  writeFileSync(STORE_PATH, JSON.stringify(olts, null, 2));
}

export function getOlt(ip: string): ManagedOlt | undefined {
  return loadOlts().find((o) => o.ip === ip);
}

export function addOlt(
  ip: string,
  name: string,
  opts: { siteLabel?: string; user?: string } = {}
): ManagedOlt {
  const olts = loadOlts();
  const existing = olts.find((o) => o.ip === ip);
  if (existing) {
    existing.name = name;
    if (opts.siteLabel) existing.siteLabel = opts.siteLabel;
    if (opts.user) existing.user = opts.user;
    saveOlts(olts);
    return existing;
  }
  const olt: ManagedOlt = {
    ip,
    name,
    status: "unknown",
    addedAt: Date.now(),
    ...(opts.siteLabel ? { siteLabel: opts.siteLabel } : {}),
    ...(opts.user ? { user: opts.user } : {}),
  };
  olts.push(olt);
  saveOlts(olts);
  return olt;
}

export function removeOlt(ip: string): boolean {
  const olts = loadOlts();
  const filtered = olts.filter((o) => o.ip !== ip);
  if (filtered.length === olts.length) return false;
  saveOlts(filtered);
  return true;
}

export function updateOlt(ip: string, updates: Partial<ManagedOlt>) {
  const olts = loadOlts();
  const olt = olts.find((o) => o.ip === ip);
  if (olt) {
    Object.assign(olt, updates);
    saveOlts(olts);
  }
}
