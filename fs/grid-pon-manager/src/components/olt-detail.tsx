"use client";

import { useCallback, useEffect, useState } from "react";
import type { OltInfo, OnuRow, WhitelistRow, NetworkInfo } from "@/lib/ui-types";
import { parseDhcpClient, type KV } from "@/lib/parse-dhcp";

// ── shared bits ──

const TABS = ["Overview", "ONUs", "Terminal", "Whitelist", "Settings", "Firmware"] as const;
type Tab = (typeof TABS)[number];

const btnBase =
  "px-4 py-2 text-sm font-medium rounded-lg transition-all cursor-pointer disabled:opacity-50";
const btnGhost = `${btnBase} border border-[var(--color-border)] bg-white hover:border-[var(--color-grid-400)] hover:bg-[var(--color-grid-50)]`;
const btnDark = `${btnBase} bg-[var(--color-text-primary)] text-white hover:opacity-85`;
const btnDanger = `${btnBase} border border-red-200 text-[var(--color-status-offline)] hover:bg-red-50`;
const inputCls =
  "px-3.5 py-2.5 text-sm rounded-lg border border-[var(--color-border)] bg-white outline-none focus:border-[var(--color-grid-500)] focus:ring-2 focus:ring-[var(--color-grid-500)]/10 transition-all";

function Card({ children, className = "" }: { children: React.ReactNode; className?: string }) {
  return (
    <div className={`rounded-xl border border-[var(--color-border)] bg-white p-6 ${className}`}>
      {children}
    </div>
  );
}

function StatusBadge({ status }: { status: string }) {
  const cfg =
    {
      online: { color: "var(--color-status-online)", bg: "rgba(34,197,94,0.1)", label: "Online" },
      offline: { color: "var(--color-status-offline)", bg: "rgba(239,68,68,0.1)", label: "Offline" },
    }[status] ?? { color: "var(--color-text-muted)", bg: "rgba(148,163,184,0.1)", label: status };
  return (
    <span
      className="inline-flex items-center gap-1.5 text-xs font-medium uppercase tracking-wider px-2.5 py-1 rounded-full"
      style={{ color: cfg.color, backgroundColor: cfg.bg }}
    >
      <span className="w-1.5 h-1.5 rounded-full" style={{ backgroundColor: cfg.color }} />
      {cfg.label}
    </span>
  );
}

function Row({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="flex items-center justify-between py-2.5 border-b border-[var(--color-border)] last:border-0">
      <span className="text-sm text-[var(--color-text-muted)]">{label}</span>
      <span
        className={`text-sm text-[var(--color-text-primary)] ${mono ? "font-[family-name:var(--font-mono)]" : ""}`}
      >
        {value || "--"}
      </span>
    </div>
  );
}

function Output({ text }: { text: string }) {
  if (!text) return null;
  return (
    <pre className="text-xs font-[family-name:var(--font-mono)] bg-[#f8fafc] rounded-lg p-3 overflow-x-auto text-[var(--color-text-secondary)] max-h-64 overflow-y-auto whitespace-pre-wrap">
      {text}
    </pre>
  );
}

async function postCmd(ip: string, cmd: string): Promise<string> {
  const r = await fetch(`/api/olts/${ip}/cmd`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ cmd }),
  });
  const d = await r.json();
  if (!r.ok) throw new Error(d.error || "command failed");
  return d.output ?? "";
}

// ── main ──

export function OltDetail({
  olt,
  onBack,
  onRemoved,
}: {
  olt: OltInfo;
  onBack: () => void;
  onRemoved: () => void;
}) {
  const [tab, setTab] = useState<Tab>("Overview");
  const [deleting, setDeleting] = useState(false);

  const handleDelete = async () => {
    if (!confirm(`Remove ${olt.name} (${olt.ip}) from management?`)) return;
    setDeleting(true);
    try {
      await fetch("/api/olts", {
        method: "DELETE",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ ip: olt.ip }),
      });
      onRemoved();
    } catch {
      setDeleting(false);
    }
  };

  return (
    <div className="max-w-5xl mx-auto px-6 py-8" style={{ animation: "fade-up 0.3s ease-out" }}>
      <button
        onClick={onBack}
        className="flex items-center gap-1.5 text-sm text-[var(--color-text-muted)] hover:text-[var(--color-text-primary)] transition-colors mb-6 cursor-pointer"
      >
        <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
          <path d="M10 4L6 8L10 12" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
        Back to fleet
      </button>

      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-6">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-semibold text-[var(--color-text-primary)]">{olt.name}</h1>
            <StatusBadge status={olt.status} />
          </div>
          <p className="text-sm text-[var(--color-text-muted)] mt-1 font-[family-name:var(--font-mono)]">
            {olt.ip}
            {olt.serial ? ` · ${olt.serial}` : ""}
          </p>
        </div>
        <button onClick={handleDelete} disabled={deleting} className={btnDanger}>
          {deleting ? "Removing..." : "Remove OLT"}
        </button>
      </div>

      {/* Tabs */}
      <div className="flex gap-1 border-b border-[var(--color-border)] mb-6 overflow-x-auto">
        {TABS.map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`px-4 py-2.5 text-sm font-medium whitespace-nowrap border-b-2 -mb-px transition-colors cursor-pointer ${
              tab === t
                ? "border-[var(--color-grid-500)] text-[var(--color-text-primary)]"
                : "border-transparent text-[var(--color-text-muted)] hover:text-[var(--color-text-primary)]"
            }`}
          >
            {t}
          </button>
        ))}
      </div>

      {tab === "Overview" && <OverviewTab olt={olt} />}
      {tab === "ONUs" && <OnusTab ip={olt.ip} />}
      {tab === "Terminal" && <TerminalTab ip={olt.ip} />}
      {tab === "Whitelist" && <WhitelistTab ip={olt.ip} />}
      {tab === "Settings" && <SettingsTab ip={olt.ip} />}
      {tab === "Firmware" && <FirmwareTab ip={olt.ip} />}
    </div>
  );
}

// ── Overview ──

interface DeviceResp {
  status?: string;
  olt_sn?: string;
  name?: string;
  pn?: string;
  uptime?: string | number;
  extra?: string;
  optics?: {
    tx_pwr_mw: number | null;
    bias_ma: number | null;
    voltage: number | null;
    temperature: number | null;
    alarm: number | null;
  };
  error?: string;
}

function OverviewTab({ olt }: { olt: OltInfo }) {
  const [dev, setDev] = useState<DeviceResp | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const r = await fetch(`/api/olts/${olt.ip}/device`);
      const d = await r.json();
      if (!r.ok) throw new Error(d.error || "unreachable");
      setDev(d);
    } catch (e) {
      setError(e instanceof Error ? e.message : "failed");
    } finally {
      setLoading(false);
    }
  }, [olt.ip]);

  useEffect(() => {
    load();
  }, [load]);

  const o = dev?.optics;

  return (
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
      <Card>
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-sm font-semibold text-[var(--color-text-primary)]">Device</h2>
          <button onClick={load} disabled={loading} className={`${btnGhost} !px-3 !py-1.5 text-xs`}>
            {loading ? "..." : "Refresh"}
          </button>
        </div>
        <Row label="Name" value={dev?.name || olt.name} />
        <Row label="IP Address" value={olt.ip} mono />
        <Row label="Serial (SN)" value={dev?.olt_sn || olt.serial || ""} mono />
        <Row label="Part Number" value={dev?.pn || olt.pn || ""} mono />
        <Row label="Uptime" value={dev?.uptime != null ? String(dev.uptime) : ""} />
        <Row label="Site" value={olt.siteLabel || ""} />
      </Card>

      <Card>
        <h2 className="text-sm font-semibold text-[var(--color-text-primary)] mb-4">OLT Optics</h2>
        {error ? (
          <div className="text-sm text-[var(--color-status-offline)]">{error}</div>
        ) : (
          <>
            <Row label="TX Power" value={o?.tx_pwr_mw != null ? `${o.tx_pwr_mw} mW` : ""} mono />
            <Row label="Supply Voltage" value={o?.voltage != null ? `${o.voltage} V` : ""} mono />
            <Row label="Temperature" value={o?.temperature != null ? `${o.temperature} °C` : ""} mono />
            <Row label="Bias Current" value={o?.bias_ma != null ? `${o.bias_ma} mA` : ""} mono />
            <Row
              label="Alarm"
              value={o?.alarm != null ? (o.alarm === 0 ? "None (0)" : `ALARM (${o.alarm})`) : ""}
            />
          </>
        )}
      </Card>
    </div>
  );
}

// ── ONUs ──

function dbm(v: number | null): string {
  if (v === null) return "--";
  if (v === -99) return "no DDM";
  return `${v} dBm`;
}

function OnusTab({ ip }: { ip: string }) {
  const [onus, setOnus] = useState<OnuRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const r = await fetch(`/api/olts/${ip}/onus`);
      const d = await r.json();
      if (!r.ok) throw new Error(d.error || "failed");
      setOnus(d.onus || []);
    } catch (e) {
      setError(e instanceof Error ? e.message : "failed");
    } finally {
      setLoading(false);
    }
  }, [ip]);

  useEffect(() => {
    load();
  }, [load]);

  const reboot = async (sn?: string) => {
    if (!sn || !confirm(`Reboot ONU ${sn}? It will drop offline briefly.`)) return;
    setBusy(sn);
    try {
      await postCmd(ip, `reboot_onu("${sn}")`);
    } catch (e) {
      alert(e instanceof Error ? e.message : "reboot failed");
    } finally {
      setBusy(null);
    }
  };

  return (
    <Card>
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-sm font-semibold text-[var(--color-text-primary)]">
          Connected ONUs {onus.length > 0 && `(${onus.length})`}
        </h2>
        <button onClick={load} disabled={loading} className={`${btnGhost} !px-3 !py-1.5 text-xs`}>
          {loading ? "Querying..." : "Refresh"}
        </button>
      </div>
      {error ? (
        <div className="text-sm text-[var(--color-status-offline)]">{error}</div>
      ) : onus.length === 0 && !loading ? (
        <div className="text-sm text-[var(--color-text-muted)] py-8 text-center">No ONUs reported.</div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-[11px] uppercase tracking-wider text-[var(--color-text-muted)] border-b border-[var(--color-border)]">
                <th className="text-left font-medium py-2 pr-3">#</th>
                <th className="text-left font-medium py-2 pr-3">Serial</th>
                <th className="text-left font-medium py-2 pr-3">State</th>
                <th className="text-right font-medium py-2 pr-3">Vcc</th>
                <th className="text-right font-medium py-2 pr-3">Temp</th>
                <th className="text-right font-medium py-2 pr-3">TX</th>
                <th className="text-right font-medium py-2 pr-3">RX</th>
                <th className="py-2"></th>
              </tr>
            </thead>
            <tbody className="font-[family-name:var(--font-mono)] text-[13px]">
              {onus.map((o) => (
                <tr key={o.id} className="border-b border-[var(--color-border)] last:border-0">
                  <td className="py-2 pr-3 text-[var(--color-text-muted)]">{o.id}</td>
                  <td className="py-2 pr-3">{o.sn || "--"}</td>
                  <td className="py-2 pr-3">
                    <span
                      className="text-[11px] px-1.5 py-0.5 rounded"
                      style={{
                        color:
                          o.state === "ON_LINE"
                            ? "var(--color-status-online)"
                            : "var(--color-text-muted)",
                        backgroundColor:
                          o.state === "ON_LINE" ? "rgba(34,197,94,0.1)" : "rgba(148,163,184,0.1)",
                      }}
                    >
                      {o.state || "--"}
                    </span>
                  </td>
                  <td className="py-2 pr-3 text-right">{o.voltage != null ? `${o.voltage}V` : "--"}</td>
                  <td className="py-2 pr-3 text-right">
                    {o.temperature != null ? `${o.temperature}°` : "--"}
                  </td>
                  <td className="py-2 pr-3 text-right">{dbm(o.tx_pwr_dbm)}</td>
                  <td className="py-2 pr-3 text-right">{dbm(o.rx_pwr_dbm)}</td>
                  <td className="py-2 text-right">
                    <button
                      onClick={() => reboot(o.sn)}
                      disabled={busy === o.sn}
                      className="text-[11px] text-[var(--color-text-muted)] hover:text-[var(--color-status-offline)] transition-colors cursor-pointer disabled:opacity-50"
                    >
                      {busy === o.sn ? "..." : "reboot"}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Card>
  );
}

// ── Terminal ──

const QUICK_CMDS = [
  "show_onu()",
  "get_olt_pn()",
  "show_ip()",
  "show_mac()",
  "show_dhcp_client()",
  "get_white_list_type()",
  "get_whitelst_number()",
  "show_gateway()",
];

function TerminalTab({ ip }: { ip: string }) {
  const [cmd, setCmd] = useState("");
  const [out, setOut] = useState("");
  const [loading, setLoading] = useState(false);

  const run = async (c?: string) => {
    const command = (c ?? cmd).trim();
    if (!command) return;
    setCmd(command);
    setLoading(true);
    setOut("");
    try {
      const text = await postCmd(ip, command);
      setOut(text || "(no output)");
    } catch (e) {
      setOut(e instanceof Error ? e.message : "command failed");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Card>
      <h2 className="text-sm font-semibold text-[var(--color-text-primary)] mb-1">Terminal</h2>
      <p className="text-xs text-[var(--color-text-muted)] mb-4">
        Firmware terminal commands (function-call syntax). Write/destructive commands are not
        blocked — use with care.
      </p>
      <div className="flex gap-2 mb-3">
        <input
          value={cmd}
          onChange={(e) => setCmd(e.target.value)}
          onKeyDown={(e) => e.key === "Enter" && run()}
          placeholder='get_onu_optics("TPLG31A11C1A")'
          className={`flex-1 ${inputCls} font-[family-name:var(--font-mono)]`}
        />
        <button onClick={() => run()} disabled={loading} className={btnDark}>
          {loading ? "Running..." : "Run"}
        </button>
      </div>
      <div className="flex flex-wrap gap-1.5 mb-3">
        {QUICK_CMDS.map((c) => (
          <button
            key={c}
            onClick={() => run(c)}
            className="px-2.5 py-1 text-[11px] font-[family-name:var(--font-mono)] rounded-md border border-[var(--color-border)] text-[var(--color-text-muted)] hover:text-[var(--color-text-primary)] hover:border-[var(--color-grid-400)] transition-all cursor-pointer"
          >
            {c}
          </button>
        ))}
      </div>
      <Output text={out} />
    </Card>
  );
}

// ── Whitelist ──

function WhitelistTab({ ip }: { ip: string }) {
  const [entries, setEntries] = useState<WhitelistRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [newSn, setNewSn] = useState("");
  const [newType, setNewType] = useState(1);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const r = await fetch(`/api/olts/${ip}/whitelist`);
      const d = await r.json();
      if (!r.ok) throw new Error(d.error || "failed");
      setEntries(d.entries || []);
      setDirty(false);
    } catch (e) {
      setError(e instanceof Error ? e.message : "failed");
    } finally {
      setLoading(false);
    }
  }, [ip]);

  useEffect(() => {
    load();
  }, [load]);

  const addEntry = () => {
    const sn = newSn.trim().toUpperCase();
    if (sn.length !== 12) {
      alert("ONU SN must be 12 characters (e.g. TPLG31A11C1A)");
      return;
    }
    if (entries.some((e) => e.sn === sn)) {
      alert("Already in the list");
      return;
    }
    setEntries((prev) => [...prev, { sn, type: newType, active: true }]);
    setNewSn("");
    setDirty(true);
  };

  const removeEntry = (sn: string) => {
    setEntries((prev) => prev.filter((e) => e.sn !== sn));
    setDirty(true);
  };

  const toggle = (sn: string) => {
    setEntries((prev) => prev.map((e) => (e.sn === sn ? { ...e, active: !e.active } : e)));
    setDirty(true);
  };

  const save = async () => {
    if (!confirm(`Write ${entries.length} whitelist entries to the OLT flash?`)) return;
    setSaving(true);
    try {
      const r = await fetch(`/api/olts/${ip}/whitelist`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ entries }),
      });
      const d = await r.json();
      if (!r.ok) throw new Error(d.error || "save failed");
      setDirty(false);
      await load();
    } catch (e) {
      alert(e instanceof Error ? e.message : "save failed");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Card>
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-sm font-semibold text-[var(--color-text-primary)]">
          ONU Whitelist {entries.length > 0 && `(${entries.length})`}
        </h2>
        <div className="flex gap-2">
          <button onClick={load} disabled={loading} className={`${btnGhost} !px-3 !py-1.5 text-xs`}>
            {loading ? "..." : "Reload"}
          </button>
          <button onClick={save} disabled={saving || !dirty} className={`${btnDark} !px-3 !py-1.5 text-xs`}>
            {saving ? "Writing..." : dirty ? "Save to OLT" : "Saved"}
          </button>
        </div>
      </div>

      {error && <div className="text-sm text-[var(--color-status-offline)] mb-3">{error}</div>}

      {/* add row */}
      <div className="flex flex-wrap gap-2 mb-4 items-end">
        <div>
          <label className="block text-[10px] uppercase tracking-wider text-[var(--color-text-muted)] mb-1">
            ONU Serial
          </label>
          <input
            value={newSn}
            onChange={(e) => setNewSn(e.target.value)}
            placeholder="TPLG31A11C1A"
            maxLength={12}
            className={`${inputCls} font-[family-name:var(--font-mono)] w-44`}
          />
        </div>
        <div>
          <label className="block text-[10px] uppercase tracking-wider text-[var(--color-text-muted)] mb-1">
            Service Type
          </label>
          <select
            value={newType}
            onChange={(e) => setNewType(Number(e.target.value))}
            className={inputCls}
          >
            {[1, 2, 3, 4, 5].map((t) => (
              <option key={t} value={t}>
                {t}
              </option>
            ))}
          </select>
        </div>
        <button onClick={addEntry} className={btnGhost}>
          Add ONU
        </button>
      </div>

      {entries.length === 0 && !loading ? (
        <div className="text-sm text-[var(--color-text-muted)] py-6 text-center">
          Whitelist is empty.
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-[11px] uppercase tracking-wider text-[var(--color-text-muted)] border-b border-[var(--color-border)]">
                <th className="text-left font-medium py-2 pr-3">Serial</th>
                <th className="text-left font-medium py-2 pr-3">Type</th>
                <th className="text-left font-medium py-2 pr-3">Active</th>
                <th className="py-2"></th>
              </tr>
            </thead>
            <tbody className="font-[family-name:var(--font-mono)] text-[13px]">
              {entries.map((e) => (
                <tr key={e.sn} className="border-b border-[var(--color-border)] last:border-0">
                  <td className="py-2 pr-3">{e.sn}</td>
                  <td className="py-2 pr-3">{e.type}</td>
                  <td className="py-2 pr-3">
                    <button
                      onClick={() => toggle(e.sn)}
                      className="cursor-pointer"
                      style={{
                        color: e.active
                          ? "var(--color-status-online)"
                          : "var(--color-text-muted)",
                      }}
                    >
                      {e.active ? "● yes" : "○ no"}
                    </button>
                  </td>
                  <td className="py-2 text-right">
                    <button
                      onClick={() => removeEntry(e.sn)}
                      className="text-[11px] text-[var(--color-text-muted)] hover:text-[var(--color-status-offline)] transition-colors cursor-pointer"
                    >
                      remove
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {dirty && (
        <p className="text-[11px] text-[var(--color-text-muted)] mt-3">
          Unsaved changes — &quot;Save to OLT&quot; writes the full list to flash.
        </p>
      )}
    </Card>
  );
}

// ── Settings ──

function KvList({ items }: { items: KV[] }) {
  return (
    <div className="space-y-1.5">
      {items.map((kv) => (
        <div key={kv.label} className="flex items-center justify-between gap-3">
          <span className="text-xs text-[var(--color-text-muted)]">{kv.label}</span>
          <span className="text-[13px] font-[family-name:var(--font-mono)] text-[var(--color-text-primary)]">
            {kv.value}
          </span>
        </div>
      ))}
    </div>
  );
}

function DhcpDetails({ raw }: { raw: string }) {
  const [showRaw, setShowRaw] = useState(false);
  const parsed = parseDhcpClient(raw);
  if (!parsed) {
    return (
      <div className="mt-3">
        <Output text={raw} />
      </div>
    );
  }

  const groups = [
    { title: "Status", items: parsed.status, accent: false },
    { title: "Active (in effect)", items: parsed.active, accent: true },
    { title: "Configured", items: parsed.configured, accent: false },
  ].filter((g) => g.items.length > 0);

  return (
    <div className="mt-4 border-t border-[var(--color-border)] pt-4">
      <div className="flex items-center justify-between mb-3">
        <span className="text-[10px] uppercase tracking-wider text-[var(--color-text-muted)]">
          Address Detail
        </span>
        <button
          onClick={() => setShowRaw((v) => !v)}
          className="text-[11px] text-[var(--color-text-muted)] hover:text-[var(--color-text-primary)] cursor-pointer"
        >
          {showRaw ? "hide raw" : "raw"}
        </button>
      </div>
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
        {groups.map((g) => (
          <div
            key={g.title}
            className={`rounded-lg border p-3.5 ${
              g.accent
                ? "border-[var(--color-grid-200)] bg-[var(--color-grid-50)]"
                : "border-[var(--color-border)]"
            }`}
          >
            <div className="text-[11px] font-semibold text-[var(--color-text-primary)] mb-2.5">
              {g.title}
            </div>
            <KvList items={g.items} />
          </div>
        ))}
      </div>
      {showRaw && (
        <div className="mt-3">
          <Output text={raw} />
        </div>
      )}
    </div>
  );
}

function SettingsTab({ ip }: { ip: string }) {
  const [net, setNet] = useState<NetworkInfo | null>(null);
  const [netLoading, setNetLoading] = useState(true);
  const [config, setConfig] = useState<Record<string, unknown> | null>(null);
  const [configLoading, setConfigLoading] = useState(true);
  const [actionOut, setActionOut] = useState("");
  const [busy, setBusy] = useState(false);

  // form state
  const [host, setHost] = useState("");
  const [staticIp, setStaticIp] = useState("");
  const [mask, setMask] = useState("255.255.255.0");
  const [gw, setGw] = useState("");
  const [prefilled, setPrefilled] = useState(false);

  const loadNetwork = useCallback(async () => {
    setNetLoading(true);
    try {
      const r = await fetch(`/api/olts/${ip}/network`);
      const d = await r.json();
      if (r.ok) setNet(d.network);
    } finally {
      setNetLoading(false);
    }
  }, [ip]);

  const loadConfig = useCallback(async () => {
    setConfigLoading(true);
    try {
      const r = await fetch(`/api/olts/${ip}/config`);
      const d = await r.json();
      if (r.ok) setConfig(d.config);
    } finally {
      setConfigLoading(false);
    }
  }, [ip]);

  useEffect(() => {
    loadNetwork();
    loadConfig();
  }, [loadNetwork, loadConfig]);

  // Prefill the static-IP form from the OLT's current settings (once).
  useEffect(() => {
    if (net && !prefilled) {
      setStaticIp(net.ip || ip);
      setMask(net.mask || "255.255.255.0");
      setGw(net.gateway || "");
      setPrefilled(true);
    }
  }, [net, prefilled, ip]);

  const runAction = async (cmd: string, confirmMsg?: string): Promise<boolean> => {
    if (confirmMsg && !confirm(confirmMsg)) return false;
    setBusy(true);
    setActionOut("");
    try {
      const text = await postCmd(ip, cmd);
      setActionOut(`${cmd}\n${text || "(ok)"}`);
      return true;
    } catch (e) {
      setActionOut(`${cmd}\n${e instanceof Error ? e.message : "failed"}`);
      return false;
    } finally {
      setBusy(false);
    }
  };

  // Safe sequence: set the address FIRST, then switch to static mode. Flipping
  // mode first would jump the OLT to a stale configured IP and lose it.
  const applyStatic = async () => {
    if (!staticIp || !gw) return;
    const changingIp = staticIp !== ip;
    const warn = changingIp
      ? `Set static IP to ${staticIp} (currently reachable at ${ip}).\n\nThe OLT's address will CHANGE — this session will drop and you'll need to reach it at ${staticIp}. Continue?`
      : `Set static IP ${staticIp} / ${mask} / gw ${gw} and switch to static mode?`;
    if (!confirm(warn)) return;
    setBusy(true);
    setActionOut("");
    try {
      const a = await postCmd(ip, `set_ipmaskgate("${staticIp}","${mask}","${gw}")`);
      const b = await postCmd(ip, "set_ip_addr_mode(0)");
      setActionOut(`set_ipmaskgate -> ${a.trim()}\nset_ip_addr_mode(0) -> ${b.trim()}`);
      await loadNetwork();
    } catch (e) {
      setActionOut(e instanceof Error ? e.message : "failed");
    } finally {
      setBusy(false);
    }
  };

  const switchDhcp = async () => {
    const ok = await runAction("set_ip_addr_mode(1)", "Switch this OLT to DHCP mode?");
    if (ok) await loadNetwork();
  };

  const modeCfg =
    net?.mode === "static"
      ? { label: "STATIC", color: "var(--color-status-online)", bg: "rgba(34,197,94,0.1)" }
      : net?.mode === "dhcp"
        ? { label: "DHCP", color: "var(--color-grid-600)", bg: "rgba(10,132,255,0.1)" }
        : { label: "UNKNOWN", color: "var(--color-text-muted)", bg: "rgba(148,163,184,0.1)" };

  const cfgIpDiffers = Boolean(net?.ip && net.ip !== ip);

  return (
    <div className="space-y-6">
      {/* Network status — the headline */}
      <Card>
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-sm font-semibold text-[var(--color-text-primary)]">Network</h2>
          <button onClick={loadNetwork} disabled={netLoading} className={`${btnGhost} !px-3 !py-1.5 text-xs`}>
            {netLoading ? "Querying..." : "Refresh"}
          </button>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          {/* mode tile */}
          <div className="rounded-lg border border-[var(--color-border)] p-4 flex flex-col gap-2">
            <span className="text-[10px] uppercase tracking-wider text-[var(--color-text-muted)]">
              IP Assignment
            </span>
            <span
              className="inline-flex w-fit items-center gap-1.5 text-sm font-semibold uppercase tracking-wider px-2.5 py-1 rounded-full"
              style={{ color: modeCfg.color, backgroundColor: modeCfg.bg }}
            >
              <span className="w-1.5 h-1.5 rounded-full" style={{ backgroundColor: modeCfg.color }} />
              {netLoading && !net ? "…" : modeCfg.label}
            </span>
          </div>
          {/* reachable-at tile */}
          <div className="rounded-lg border border-[var(--color-border)] p-4 flex flex-col gap-1">
            <span className="text-[10px] uppercase tracking-wider text-[var(--color-text-muted)]">
              Reachable At
            </span>
            <span className="text-lg font-semibold font-[family-name:var(--font-mono)] text-[var(--color-text-primary)]">
              {ip}
            </span>
            <span className="text-[11px] text-[var(--color-text-muted)]">current management IP</span>
          </div>
          {/* mac tile */}
          <div className="rounded-lg border border-[var(--color-border)] p-4 flex flex-col gap-1">
            <span className="text-[10px] uppercase tracking-wider text-[var(--color-text-muted)]">
              MAC Address
            </span>
            <span className="text-sm font-[family-name:var(--font-mono)] text-[var(--color-text-secondary)]">
              {net?.mac || "--"}
            </span>
          </div>
        </div>

        <div className="mt-4">
          <Row label={net?.mode === "dhcp" ? "Configured IP (static fallback)" : "Configured IP"} value={net?.ip || ""} mono />
          <Row label="Subnet Mask" value={net?.mask || ""} mono />
          <Row label="Gateway" value={net?.gateway || ""} mono />
        </div>

        {cfgIpDiffers && net?.mode === "dhcp" && (
          <p className="text-[11px] text-[var(--color-text-muted)] mt-3">
            In DHCP mode the OLT is live at <span className="font-[family-name:var(--font-mono)]">{ip}</span> (its lease); the
            configured IP <span className="font-[family-name:var(--font-mono)]">{net?.ip}</span> is the static fallback used
            only if you switch to static mode.
          </p>
        )}
        {net?.dhcp && <DhcpDetails raw={net.dhcp} />}
      </Card>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Configuration */}
        <Card>
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-sm font-semibold text-[var(--color-text-primary)]">Configuration</h2>
            <button onClick={loadConfig} disabled={configLoading} className={`${btnGhost} !px-3 !py-1.5 text-xs`}>
              {configLoading ? "..." : "Refresh"}
            </button>
          </div>
          {config ? (
            <>
              <Row label="Device Name" value={String(config.device_name ?? "")} />
              <Row label="Location" value={String(config.location ?? "")} />
              <Row label="VLAN ID" value={String(config.vlan_id ?? "")} mono />
              <Row label="Max ONU" value={String(config.max_onu ?? "")} mono />
              <Row label="Copyright" value={String(config.copyright ?? "")} />
              <Row
                label="Port State"
                value={Array.isArray(config.port_state) ? config.port_state.join(", ") : ""}
                mono
              />
            </>
          ) : (
            <div className="text-sm text-[var(--color-text-muted)]">
              {configLoading ? "Loading..." : "Unavailable"}
            </div>
          )}
        </Card>

        {/* Provisioning actions */}
        <Card>
          <h2 className="text-sm font-semibold text-[var(--color-text-primary)] mb-4">Provisioning</h2>

          <div className="space-y-4">
            {/* mode switch — reflects current mode */}
            <div className="flex items-center gap-2">
              {net?.mode === "static" ? (
                <button onClick={switchDhcp} disabled={busy} className={btnGhost}>
                  Switch to DHCP
                </button>
              ) : (
                <span className="text-xs text-[var(--color-text-muted)]">
                  Currently DHCP — set a static IP below to switch.
                </span>
              )}
            </div>

            {/* static IP form (prefilled from current settings) */}
            <div className="border-t border-[var(--color-border)] pt-3">
              <label className="block text-[10px] uppercase tracking-wider text-[var(--color-text-muted)] mb-1">
                Static IP / Mask / Gateway
              </label>
              <div className="grid grid-cols-3 gap-2 mb-2">
                <input value={staticIp} onChange={(e) => setStaticIp(e.target.value)} placeholder="ip" className={`${inputCls} font-[family-name:var(--font-mono)]`} />
                <input value={mask} onChange={(e) => setMask(e.target.value)} placeholder="mask" className={`${inputCls} font-[family-name:var(--font-mono)]`} />
                <input value={gw} onChange={(e) => setGw(e.target.value)} placeholder="gateway" className={`${inputCls} font-[family-name:var(--font-mono)]`} />
              </div>
              <button onClick={applyStatic} disabled={busy || !staticIp || !gw} className={btnDark}>
                {net?.mode === "static" ? "Update static IP" : "Apply & switch to static"}
              </button>
              <p className="text-[11px] text-[var(--color-text-muted)] mt-2">
                Sets the address first, then switches to static mode (safe order).
              </p>
            </div>

            {/* hostname */}
            <div className="border-t border-[var(--color-border)] pt-3 flex gap-2 items-end">
              <div className="flex-1">
                <label className="block text-[10px] uppercase tracking-wider text-[var(--color-text-muted)] mb-1">
                  Hostname (DHCP)
                </label>
                <input value={host} onChange={(e) => setHost(e.target.value)} placeholder="site1-olt1" className={`${inputCls} w-full`} />
              </div>
              <button onClick={() => host && runAction(`set_host_name("${host}")`)} disabled={busy || !host} className={btnGhost}>
                Set
              </button>
            </div>

            <div className="border-t border-[var(--color-border)] pt-3">
              <button
                onClick={() => runAction("reset_system()", "Reboot the OLT now? It will drop offline for ~1 minute.")}
                disabled={busy}
                className={btnDanger}
              >
                Reboot OLT
              </button>
            </div>
          </div>

          {actionOut && (
            <div className="mt-4">
              <Output text={actionOut} />
            </div>
          )}
        </Card>
      </div>
    </div>
  );
}

// ── Firmware ──

function FirmwareTab({ ip }: { ip: string }) {
  const [file, setFile] = useState<File | null>(null);
  const [flashing, setFlashing] = useState(false);
  const [result, setResult] = useState("");

  const flash = async () => {
    if (!file) return;
    if (
      !confirm(
        `Flash "${file.name}" (${(file.size / 1024).toFixed(0)} KB) to OLT ${ip}?\n\n` +
          `This is DESTRUCTIVE and can brick the stick. Do not interrupt power or the network ` +
          `during the upgrade.`
      )
    )
      return;
    setFlashing(true);
    setResult("");
    try {
      const buf = await file.arrayBuffer();
      const r = await fetch(`/api/olts/${ip}/firmware`, {
        method: "POST",
        headers: { "Content-Type": "application/octet-stream" },
        body: buf,
      });
      const d = await r.json();
      if (!r.ok) throw new Error(d.error || "upgrade failed");
      setResult(`Firmware flashed successfully (${d.bytes} bytes). The OLT will reboot.`);
    } catch (e) {
      setResult(e instanceof Error ? e.message : "upgrade failed");
    } finally {
      setFlashing(false);
    }
  };

  return (
    <Card>
      <h2 className="text-sm font-semibold text-[var(--color-text-primary)] mb-1">Firmware Upgrade</h2>
      <div className="text-xs text-[var(--color-status-offline)] bg-red-50 rounded-lg px-3 py-2 mb-4">
        Destructive operation. Flashing the wrong image can brick the OLT stick. Ensure stable power
        and network for the duration of the upgrade.
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <input
          type="file"
          accept=".bin,application/octet-stream"
          onChange={(e) => setFile(e.target.files?.[0] ?? null)}
          className="text-sm text-[var(--color-text-secondary)] file:mr-3 file:px-3 file:py-1.5 file:rounded-lg file:border file:border-[var(--color-border)] file:bg-white file:text-sm file:cursor-pointer"
        />
        <button onClick={flash} disabled={!file || flashing} className={btnDanger}>
          {flashing ? "Flashing… do not interrupt" : "Flash firmware"}
        </button>
      </div>

      {flashing && (
        <p className="text-xs text-[var(--color-text-muted)] mt-3">
          Uploading and flashing — this can take a few minutes. Keep this tab open.
        </p>
      )}
      {result && (
        <div className="mt-4 text-sm text-[var(--color-text-primary)]">{result}</div>
      )}
    </Card>
  );
}
