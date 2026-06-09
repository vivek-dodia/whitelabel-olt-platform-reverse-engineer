/**
 * GRID PON Manager — OLT credential resolution.
 *
 * The v2 web API requires a username + password to log in. Credentials resolve
 * in this order, most specific first:
 *   1. Per-OLT secret in data/olt-secrets.json (gitignored, never committed)
 *   2. The `user` field stored alongside the OLT in olts.json (username only)
 *   3. The OLT_USER / OLT_PW environment variables (the fleet-wide default)
 *
 * Passwords NEVER live in olts.json (which is committed). Per-OLT passwords go
 * only in the gitignored secrets file; the fleet default lives in .env.
 */

import { readFileSync, writeFileSync, existsSync, mkdirSync } from "fs";
import path from "path";

export interface OltCreds {
  user: string;
  password: string;
}

interface SecretRecord {
  user?: string;
  password: string;
}

const SECRETS_PATH = path.join(process.cwd(), "data", "olt-secrets.json");

function loadSecrets(): Record<string, SecretRecord> {
  if (!existsSync(SECRETS_PATH)) return {};
  try {
    return JSON.parse(readFileSync(SECRETS_PATH, "utf-8"));
  } catch {
    return {};
  }
}

function saveSecrets(secrets: Record<string, SecretRecord>) {
  const dir = path.dirname(SECRETS_PATH);
  if (!existsSync(dir)) mkdirSync(dir, { recursive: true });
  writeFileSync(SECRETS_PATH, JSON.stringify(secrets, null, 2));
}

/** Resolve credentials for an OLT. `storeUser` is the optional username saved in
 *  olts.json. Throws a clear error if no password can be found anywhere. */
export function resolveCreds(ip: string, storeUser?: string): OltCreds {
  const secrets = loadSecrets();
  const sec = secrets[ip];

  const user =
    sec?.user ?? storeUser ?? process.env.OLT_USER ?? "admin";
  const password = sec?.password ?? process.env.OLT_PW;

  if (!password) {
    throw new Error(
      `No password for OLT ${ip}: set OLT_PW in .env, or add a per-OLT password ` +
        `(stored in data/olt-secrets.json).`
    );
  }
  return { user, password };
}

/** Store (or clear) a per-OLT password override in the gitignored secrets file. */
export function setOltSecret(ip: string, password: string, user?: string) {
  const secrets = loadSecrets();
  if (!password) {
    delete secrets[ip];
  } else {
    secrets[ip] = { password, ...(user ? { user } : {}) };
  }
  saveSecrets(secrets);
}

/** Remove any stored secret for an OLT (called when an OLT is deleted). */
export function deleteOltSecret(ip: string) {
  const secrets = loadSecrets();
  if (secrets[ip]) {
    delete secrets[ip];
    saveSecrets(secrets);
  }
}

/** Whether a per-OLT password override exists (used for UI display only). */
export function hasOltSecret(ip: string): boolean {
  return Boolean(loadSecrets()[ip]?.password);
}
