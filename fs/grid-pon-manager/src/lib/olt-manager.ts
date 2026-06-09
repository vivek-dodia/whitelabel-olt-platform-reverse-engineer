/**
 * GRID PON Manager — OLT web-client manager (singleton).
 *
 * Holds one logged-in OltWebClient per OLT IP, each keeping a persistent SSE
 * stream open. The firmware's embedded server is fragile under concurrent
 * connections and rapid reconnects (see web-api/README.md), so we reuse a single
 * long-lived client per OLT and let the browser poll our REST routes instead of
 * each request opening a new connection.
 *
 * Persisted on globalThis so Next.js dev hot-reloads don't leak SSE connections.
 */

import { OltWebClient } from "./olt-web-client";
import { resolveCreds } from "./olt-creds";
import { loadOlts } from "./olt-store";

class OltManager {
  private clients = new Map<string, OltWebClient>();
  private creating = new Map<string, Promise<OltWebClient>>();

  /** Get a logged-in client for `ip`, creating + logging in on first use.
   *  Concurrent callers share one in-flight login. */
  async getClient(ip: string): Promise<OltWebClient> {
    const existing = this.clients.get(ip);
    if (existing?.isLoggedIn) return existing;

    const inflight = this.creating.get(ip);
    if (inflight) return inflight;

    const p = this.create(ip);
    this.creating.set(ip, p);
    try {
      return await p;
    } finally {
      this.creating.delete(ip);
    }
  }

  private async create(ip: string): Promise<OltWebClient> {
    this.clients.get(ip)?.close();
    const storeUser = loadOlts().find((o) => o.ip === ip)?.user;
    const { user, password } = resolveCreds(ip, storeUser);
    const client = new OltWebClient(ip, user, password);
    await client.login();
    this.clients.set(ip, client);
    return client;
  }

  /** Tear down and forget the client for `ip` (e.g. on auth failure or removal). */
  invalidate(ip: string) {
    const c = this.clients.get(ip);
    if (c) {
      c.close();
      this.clients.delete(ip);
    }
  }

  /** Run an operation against the OLT, re-logging-in once if the session has
   *  expired (the firmware drops idle tokens). */
  async withClient<T>(ip: string, fn: (c: OltWebClient) => Promise<T>): Promise<T> {
    let client = await this.getClient(ip);
    try {
      return await fn(client);
    } catch (err) {
      if (isAuthError(err)) {
        this.invalidate(ip);
        client = await this.getClient(ip);
        return fn(client);
      }
      throw err;
    }
  }
}

function isAuthError(err: unknown): boolean {
  const msg = err instanceof Error ? err.message : String(err);
  return /token|unauthor|forbidden|login failed|401|403/i.test(msg);
}

const g = globalThis as unknown as { __oltManager?: OltManager };
export const oltManager: OltManager = g.__oltManager ?? (g.__oltManager = new OltManager());
