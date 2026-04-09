/**
 * GRID PON Manager — FS OLT UDP Client
 *
 * Single persistent RX socket on port 64218.
 * TX uses ephemeral ports (no bind needed).
 * Requests queued sequentially to avoid race conditions.
 * Background poller keeps OLT status fresh every 5 seconds.
 */

import dgram from "dgram";

const OLT_TX_PORT = 64219;
const OLT_RX_PORT = 64218;

const WRITE_AUTH = Buffer.from("5774b87337454200d4d33f80c4663dc5e5", "hex");
const READ_AUTH = Buffer.from("5274b87337454200d4d33f80c4663dc5e5", "hex");

export enum CommandCode {
  None = 0,
  ShakeHand = 1,
  IpConfiguration = 2,
  CpeWhiteListSend = 3,
  CpeWhiteListReadQty = 4,
  OnuWlistRpt = 5,
  CpeIllegalCpeReport = 6,
  CpeAlarmReport = 7,
  OltAlarmReport = 8,
  CpeServiceTypeSend = 9,
  CpeWhiteListDel = 10,
  CpeOptParaReport = 11,
  CpeSnStatus = 12,
  ServiceConfigRpt = 13,
  PasswordCmd = 66,
  PasswordCheckCmd = 67,
  OltUpdateBinCmd = 68,
}

export interface OltInfo {
  ip: string;
  mac: string;
  serial: string;
  status: "online" | "offline" | "unknown";
  lastSeen: number;
  readEnabled: boolean;
  writeEnabled: boolean;
}

interface PendingRequest {
  resolve: (data: Buffer) => void;
  timer: ReturnType<typeof setTimeout>;
}

// Cached status from background polling
export interface CachedOltStatus {
  ip: string;
  mac: string;
  serial: string;
  status: "online" | "offline";
  lastSeen: number;
}

const statusCache: Map<string, CachedOltStatus> = new Map();
let pollerStarted = false;

export function getCachedStatus(ip: string): CachedOltStatus | undefined {
  return statusCache.get(ip);
}

export function getAllCachedStatus(): CachedOltStatus[] {
  return Array.from(statusCache.values());
}

export class OltClient {
  private seq = 0;
  private rxSocket: dgram.Socket | null = null;
  private pending: Map<string, PendingRequest> = new Map();
  private ready = false;
  private initPromise: Promise<void> | null = null;
  private mutex = Promise.resolve();

  private async ensureInit(): Promise<void> {
    if (this.ready) return;
    if (this.initPromise) return this.initPromise;

    this.initPromise = new Promise<void>((resolve, reject) => {
      this.rxSocket = dgram.createSocket({ type: "udp4", reuseAddr: true });

      this.rxSocket.on("message", (msg, rinfo) => {
        const data = Buffer.from(msg);
        const seq = data.readUInt16BE(1);
        const key = `${rinfo.address}:${seq}`;
        const req = this.pending.get(key);
        if (req) {
          clearTimeout(req.timer);
          this.pending.delete(key);
          req.resolve(data);
        }
      });

      this.rxSocket.on("error", (err) => {
        console.error("OLT RX socket error:", err.message);
        this.ready = false;
        this.initPromise = null;
        reject(err);
      });

      this.rxSocket.bind(OLT_RX_PORT, () => {
        this.ready = true;
        resolve();
      });
    });

    return this.initPromise;
  }

  private nextSeq(): number {
    this.seq = (this.seq + 1) & 0xffff;
    return this.seq;
  }

  private buildPacket(cmd: CommandCode, seq: number, data?: Buffer): Buffer {
    const payload = Buffer.alloc(22);
    payload[0] = cmd;
    payload.writeUInt16BE(seq, 1);
    if (data) {
      data.copy(payload, 3, 0, Math.min(data.length, 19));
    }
    return payload;
  }

  // Serialized send — prevents concurrent socket issues
  private enqueue<T>(fn: () => Promise<T>): Promise<T> {
    const p = this.mutex.then(fn, fn);
    this.mutex = p.then(() => {}, () => {});
    return p;
  }

  async sendAndWait(
    oltIp: string,
    cmd: CommandCode,
    data?: Buffer,
    timeoutMs = 3000
  ): Promise<Buffer> {
    return this.enqueue(async () => {
      await this.ensureInit();

      const seq = this.nextSeq();
      const packet = this.buildPacket(cmd, seq, data);

      return new Promise<Buffer>((resolve, reject) => {
        const key = `${oltIp}:${seq}`;
        const timer = setTimeout(() => {
          this.pending.delete(key);
          reject(new Error(`Timeout from ${oltIp}`));
        }, timeoutMs);

        this.pending.set(key, { resolve, timer });

        // Send from port 64219 to match protocol spec
        const txSock = dgram.createSocket({ type: "udp4", reuseAddr: true });
        txSock.bind(OLT_TX_PORT, () => {
          txSock.send(packet, 0, packet.length, OLT_TX_PORT, oltIp, () => {
            txSock.close();
          });
        });
      });
    });
  }

  async shakeHand(oltIp: string): Promise<OltInfo | null> {
    try {
      const resp = await this.sendAndWait(oltIp, CommandCode.ShakeHand);
      const mac = Array.from(resp.subarray(4, 10))
        .map((b) => b.toString(16).padStart(2, "0"))
        .join(":");
      const serial = resp.subarray(20).toString("ascii").replace(/\0/g, "").trim();

      const info: OltInfo = {
        ip: oltIp,
        mac: `00:00:${mac}`,
        serial,
        status: "online",
        lastSeen: Date.now(),
        readEnabled: false,
        writeEnabled: false,
      };

      // Update cache
      statusCache.set(oltIp, {
        ip: oltIp,
        mac: info.mac,
        serial,
        status: "online",
        lastSeen: Date.now(),
      });

      return info;
    } catch {
      statusCache.set(oltIp, {
        ip: oltIp,
        mac: statusCache.get(oltIp)?.mac || "",
        serial: statusCache.get(oltIp)?.serial || "",
        status: "offline",
        lastSeen: statusCache.get(oltIp)?.lastSeen || 0,
      });
      return null;
    }
  }

  async enableWrite(oltIp: string): Promise<boolean> {
    try {
      const resp = await this.sendAndWait(oltIp, CommandCode.PasswordCmd, WRITE_AUTH);
      return resp.length > 3 && resp[3] === 0x77;
    } catch {
      return false;
    }
  }

  async enableRead(oltIp: string): Promise<boolean> {
    try {
      const resp = await this.sendAndWait(oltIp, CommandCode.PasswordCmd, READ_AUTH);
      return resp.length > 3 && resp[3] === 0x72;
    } catch {
      return false;
    }
  }

  async getOnuStatus(oltIp: string): Promise<Buffer | null> {
    try {
      return await this.sendAndWait(oltIp, CommandCode.CpeSnStatus);
    } catch {
      return null;
    }
  }

  async getAlarms(oltIp: string): Promise<Buffer | null> {
    try {
      return await this.sendAndWait(oltIp, CommandCode.OltAlarmReport);
    } catch {
      return null;
    }
  }

  async sendFCmd(oltIp: string, cmd: CommandCode, data?: Buffer): Promise<Buffer | null> {
    try {
      return await this.sendAndWait(oltIp, cmd, data);
    } catch {
      return null;
    }
  }
}

// Singleton
let client: OltClient | null = null;

export function getOltClient(): OltClient {
  if (!client) {
    client = new OltClient();
  }
  return client;
}

// Background poller — runs every 5 seconds, polls all known OLTs
export function startPoller(getOltIps: () => string[]) {
  if (pollerStarted) return;
  pollerStarted = true;

  const poll = async () => {
    const c = getOltClient();
    const ips = getOltIps();
    for (const ip of ips) {
      await c.shakeHand(ip);
    }
  };

  // Initial poll
  poll();
  // Repeat every 5 seconds
  setInterval(poll, 5000);
}
