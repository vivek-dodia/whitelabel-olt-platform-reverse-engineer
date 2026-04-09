"use client";

import { useState } from "react";

interface OltInfo {
  ip: string;
  mac: string;
  serial: string;
  name: string;
  status: "online" | "offline" | "unknown";
  lastSeen?: number;
  siteLabel?: string;
}

function StatusBadge({ status }: { status: string }) {
  const config = {
    online: { color: "var(--color-status-online)", bg: "rgba(34,197,94,0.1)", label: "Online" },
    offline: { color: "var(--color-status-offline)", bg: "rgba(239,68,68,0.1)", label: "Offline" },
    unknown: { color: "var(--color-status-unknown)", bg: "rgba(148,163,184,0.1)", label: "Unknown" },
  }[status] || { color: "var(--color-text-muted)", bg: "rgba(148,163,184,0.1)", label: status };

  return (
    <span
      className="inline-flex items-center gap-1.5 text-xs font-medium uppercase tracking-wider px-2.5 py-1 rounded-full"
      style={{ color: config.color, backgroundColor: config.bg }}
    >
      <span
        className="w-1.5 h-1.5 rounded-full"
        style={{ backgroundColor: config.color }}
      />
      {config.label}
    </span>
  );
}

function DetailRow({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
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

export function OltDetail({ olt, onBack }: { olt: OltInfo; onBack: () => void }) {
  const [authStatus, setAuthStatus] = useState<{ read?: boolean; write?: boolean }>({});
  const [authLoading, setAuthLoading] = useState(false);
  const [cmdInput, setCmdInput] = useState("");
  const [cmdResult, setCmdResult] = useState<string | null>(null);
  const [cmdLoading, setCmdLoading] = useState(false);
  const [onuData, setOnuData] = useState<string | null>(null);
  const [onuLoading, setOnuLoading] = useState(false);
  const [deleting, setDeleting] = useState(false);

  const handleAuth = async (mode: "read" | "write" | "both") => {
    setAuthLoading(true);
    try {
      const res = await fetch(`/api/olts/${olt.ip}/auth`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ mode }),
      });
      const data = await res.json();
      setAuthStatus(data.auth || {});
    } catch {
      console.error("Auth failed");
    } finally {
      setAuthLoading(false);
    }
  };

  const handleCommand = async () => {
    if (!cmdInput.trim()) return;
    setCmdLoading(true);
    setCmdResult(null);
    try {
      const cmdNum = parseInt(cmdInput.trim());
      const res = await fetch(`/api/olts/${olt.ip}/command`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ cmd: isNaN(cmdNum) ? 1 : cmdNum }),
      });
      const data = await res.json();
      setCmdResult(JSON.stringify(data, null, 2));
    } catch {
      setCmdResult("Error sending command");
    } finally {
      setCmdLoading(false);
    }
  };

  const handleQueryOnus = async () => {
    setOnuLoading(true);
    try {
      const res = await fetch(`/api/olts/${olt.ip}/onus`);
      const data = await res.json();
      setOnuData(JSON.stringify(data, null, 2));
    } catch {
      setOnuData("Error querying ONUs");
    } finally {
      setOnuLoading(false);
    }
  };

  const handleDelete = async () => {
    if (!confirm(`Remove ${olt.name} (${olt.ip}) from management?`)) return;
    setDeleting(true);
    try {
      await fetch("/api/olts", {
        method: "DELETE",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ ip: olt.ip }),
      });
      onBack();
    } catch {
      setDeleting(false);
    }
  };

  return (
    <div className="max-w-4xl mx-auto px-6 py-8" style={{ animation: "fade-up 0.3s ease-out" }}>
      {/* Back + Header */}
      <button
        onClick={onBack}
        className="flex items-center gap-1.5 text-sm text-[var(--color-text-muted)] hover:text-[var(--color-text-primary)] transition-colors mb-6 cursor-pointer"
      >
        <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
          <path d="M10 4L6 8L10 12" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
        Back to fleet
      </button>

      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-8">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-semibold text-[var(--color-text-primary)]">{olt.name}</h1>
            <StatusBadge status={olt.status} />
          </div>
          <p className="text-sm text-[var(--color-text-muted)] mt-1 font-[family-name:var(--font-mono)]">
            {olt.ip}
          </p>
        </div>
        <button
          onClick={handleDelete}
          disabled={deleting}
          className="px-4 py-2 text-sm font-medium rounded-lg border border-red-200 text-[var(--color-status-offline)] hover:bg-red-50 transition-colors cursor-pointer disabled:opacity-50"
        >
          {deleting ? "Removing..." : "Remove OLT"}
        </button>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* OLT Info Card */}
        <div className="rounded-xl border border-[var(--color-border)] bg-white p-6">
          <h2 className="text-sm font-semibold text-[var(--color-text-primary)] mb-4">
            Device Information
          </h2>
          <DetailRow label="IP Address" value={olt.ip} mono />
          <DetailRow label="MAC Address" value={olt.mac} mono />
          <DetailRow label="Serial Number" value={olt.serial} mono />
          <DetailRow label="Site" value={olt.siteLabel || "--"} />
          <DetailRow
            label="Last Seen"
            value={olt.lastSeen ? new Date(olt.lastSeen).toLocaleString() : "Never"}
          />
        </div>

        {/* Auth Controls */}
        <div className="rounded-xl border border-[var(--color-border)] bg-white p-6">
          <h2 className="text-sm font-semibold text-[var(--color-text-primary)] mb-4">
            Access Control
          </h2>
          <p className="text-xs text-[var(--color-text-muted)] mb-4">
            Enable read or write access to query and configure this OLT.
          </p>
          <div className="flex flex-wrap gap-2 mb-4">
            <button
              onClick={() => handleAuth("read")}
              disabled={authLoading}
              className="px-4 py-2 text-sm font-medium rounded-lg border border-[var(--color-border)] bg-white hover:border-[var(--color-grid-400)] hover:bg-[var(--color-grid-50)] transition-all cursor-pointer disabled:opacity-50"
            >
              Enable Read
            </button>
            <button
              onClick={() => handleAuth("write")}
              disabled={authLoading}
              className="px-4 py-2 text-sm font-medium rounded-lg border border-[var(--color-border)] bg-white hover:border-[var(--color-grid-400)] hover:bg-[var(--color-grid-50)] transition-all cursor-pointer disabled:opacity-50"
            >
              Enable Write
            </button>
            <button
              onClick={() => handleAuth("both")}
              disabled={authLoading}
              className="px-4 py-2 text-sm font-medium rounded-lg bg-[var(--color-text-primary)] text-white hover:opacity-85 transition-opacity cursor-pointer disabled:opacity-50"
            >
              {authLoading ? "Authenticating..." : "Enable Both"}
            </button>
          </div>
          {(authStatus.read !== undefined || authStatus.write !== undefined) && (
            <div className="flex gap-4 text-sm">
              {authStatus.read !== undefined && (
                <span className={authStatus.read ? "text-[var(--color-status-online)]" : "text-[var(--color-status-offline)]"}>
                  Read: {authStatus.read ? "Granted" : "Denied"}
                </span>
              )}
              {authStatus.write !== undefined && (
                <span className={authStatus.write ? "text-[var(--color-status-online)]" : "text-[var(--color-status-offline)]"}>
                  Write: {authStatus.write ? "Granted" : "Denied"}
                </span>
              )}
            </div>
          )}
        </div>

        {/* ONU Query */}
        <div className="rounded-xl border border-[var(--color-border)] bg-white p-6">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-sm font-semibold text-[var(--color-text-primary)]">
              Connected ONUs
            </h2>
            <button
              onClick={handleQueryOnus}
              disabled={onuLoading}
              className="px-3 py-1.5 text-xs font-medium rounded-lg border border-[var(--color-border)] bg-white hover:border-[var(--color-grid-400)] hover:bg-[var(--color-grid-50)] transition-all cursor-pointer disabled:opacity-50"
            >
              {onuLoading ? "Querying..." : "Query ONUs"}
            </button>
          </div>
          {onuData ? (
            <pre className="text-xs font-[family-name:var(--font-mono)] bg-[#f8fafc] rounded-lg p-3 overflow-x-auto text-[var(--color-text-secondary)] max-h-48 overflow-y-auto">
              {onuData}
            </pre>
          ) : (
            <div className="text-xs text-[var(--color-text-muted)] py-8 text-center">
              Click "Query ONUs" to fetch connected ONU data
            </div>
          )}
        </div>

        {/* Command Panel */}
        <div className="rounded-xl border border-[var(--color-border)] bg-white p-6">
          <h2 className="text-sm font-semibold text-[var(--color-text-primary)] mb-4">
            Send Command
          </h2>
          <div className="flex gap-2 mb-3">
            <input
              type="text"
              value={cmdInput}
              onChange={(e) => setCmdInput(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && handleCommand()}
              placeholder="Command code (e.g., 1 for shake_hand)"
              className="flex-1 px-3.5 py-2.5 text-sm rounded-lg border border-[var(--color-border)] bg-white outline-none focus:border-[var(--color-grid-500)] focus:ring-2 focus:ring-[var(--color-grid-500)]/10 transition-all font-[family-name:var(--font-mono)]"
            />
            <button
              onClick={handleCommand}
              disabled={cmdLoading}
              className="px-4 py-2.5 text-sm font-medium rounded-lg bg-[var(--color-text-primary)] text-white hover:opacity-85 transition-opacity cursor-pointer disabled:opacity-50 whitespace-nowrap"
            >
              {cmdLoading ? "..." : "Send"}
            </button>
          </div>
          <div className="flex flex-wrap gap-1.5 mb-3">
            {[
              { label: "Shake Hand", cmd: "1" },
              { label: "ONU Status", cmd: "12" },
              { label: "OLT Alarms", cmd: "8" },
              { label: "Whitelist Count", cmd: "4" },
            ].map((shortcut) => (
              <button
                key={shortcut.cmd}
                onClick={() => {
                  setCmdInput(shortcut.cmd);
                }}
                className="px-2.5 py-1 text-[11px] font-medium rounded-md border border-[var(--color-border)] text-[var(--color-text-muted)] hover:text-[var(--color-text-primary)] hover:border-[var(--color-grid-400)] transition-all cursor-pointer"
              >
                {shortcut.label}
              </button>
            ))}
          </div>
          {cmdResult && (
            <pre className="text-xs font-[family-name:var(--font-mono)] bg-[#f8fafc] rounded-lg p-3 overflow-x-auto text-[var(--color-text-secondary)] max-h-48 overflow-y-auto">
              {cmdResult}
            </pre>
          )}
        </div>
      </div>
    </div>
  );
}
