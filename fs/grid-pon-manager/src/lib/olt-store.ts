/**
 * GRID PON Manager — OLT Store
 *
 * Simple JSON file-based store for managed OLTs.
 * Tracks which OLTs to poll, their last known status, etc.
 */

import { readFileSync, writeFileSync, existsSync } from "fs";
import path from "path";

export interface ManagedOlt {
  ip: string;
  name: string;
  serial?: string;
  mac?: string;
  status: "online" | "offline" | "unknown";
  lastSeen?: number;
  addedAt: number;
  siteLabel?: string;
}

const STORE_PATH = path.join(process.cwd(), "data", "olts.json");

function ensureDir() {
  const dir = path.dirname(STORE_PATH);
  if (!existsSync(dir)) {
    require("fs").mkdirSync(dir, { recursive: true });
  }
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

export function addOlt(ip: string, name: string, siteLabel?: string): ManagedOlt {
  const olts = loadOlts();
  const existing = olts.find((o) => o.ip === ip);
  if (existing) {
    existing.name = name;
    if (siteLabel) existing.siteLabel = siteLabel;
    saveOlts(olts);
    return existing;
  }
  const olt: ManagedOlt = {
    ip,
    name,
    status: "unknown",
    addedAt: Date.now(),
    siteLabel,
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
