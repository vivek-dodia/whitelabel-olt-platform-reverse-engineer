"use client";

interface OltInfo {
  ip: string;
  mac: string;
  serial: string;
  name: string;
  status: "online" | "offline" | "unknown";
  lastSeen?: number;
  siteLabel?: string;
}

function StatusDot({ status }: { status: string }) {
  const color =
    status === "online"
      ? "var(--color-status-online)"
      : status === "offline"
        ? "var(--color-status-offline)"
        : "var(--color-status-unknown)";

  return (
    <span className="relative flex h-2.5 w-2.5">
      {status === "online" && (
        <span
          className="absolute inline-flex h-full w-full rounded-full opacity-40"
          style={{
            backgroundColor: color,
            animation: "pulse-dot 2s ease-in-out infinite",
          }}
        />
      )}
      <span
        className="relative inline-flex rounded-full h-2.5 w-2.5"
        style={{ backgroundColor: color }}
      />
    </span>
  );
}

function timeAgo(ts?: number): string {
  if (!ts) return "never";
  const diff = Math.floor((Date.now() - ts) / 1000);
  if (diff < 10) return "just now";
  if (diff < 60) return `${diff}s ago`;
  if (diff < 3600) return `${Math.floor(diff / 60)}m ago`;
  if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`;
  return `${Math.floor(diff / 86400)}d ago`;
}

export function OltCard({
  olt,
  onClick,
}: {
  olt: OltInfo;
  onClick: () => void;
}) {
  return (
    <button
      onClick={onClick}
      className="w-full text-left rounded-xl border border-[var(--color-border)] bg-white p-5 cursor-pointer transition-all hover:shadow-[0_4px_12px_rgba(0,0,0,0.06)] hover:border-[var(--color-grid-200)] group"
    >
      {/* Top row: name + status */}
      <div className="flex items-center justify-between mb-3">
        <div className="flex items-center gap-2.5">
          <StatusDot status={olt.status} />
          <span className="text-sm font-semibold text-[var(--color-text-primary)]">
            {olt.name}
          </span>
        </div>
        <span
          className="text-[11px] font-medium uppercase tracking-wider px-2 py-0.5 rounded-full"
          style={{
            color:
              olt.status === "online"
                ? "var(--color-status-online)"
                : olt.status === "offline"
                  ? "var(--color-status-offline)"
                  : "var(--color-text-muted)",
            backgroundColor:
              olt.status === "online"
                ? "rgba(34,197,94,0.1)"
                : olt.status === "offline"
                  ? "rgba(239,68,68,0.1)"
                  : "rgba(148,163,184,0.1)",
          }}
        >
          {olt.status}
        </span>
      </div>

      {/* Details */}
      <div className="space-y-1.5">
        <div className="flex items-center justify-between">
          <span className="text-xs text-[var(--color-text-muted)]">IP</span>
          <span className="font-[family-name:var(--font-mono)] text-[13px] text-[var(--color-text-secondary)]">
            {olt.ip}
          </span>
        </div>
        <div className="flex items-center justify-between">
          <span className="text-xs text-[var(--color-text-muted)]">Serial</span>
          <span className="font-[family-name:var(--font-mono)] text-[13px] text-[var(--color-text-secondary)]">
            {olt.serial || "--"}
          </span>
        </div>
        <div className="flex items-center justify-between">
          <span className="text-xs text-[var(--color-text-muted)]">MAC</span>
          <span className="font-[family-name:var(--font-mono)] text-[13px] text-[var(--color-text-secondary)]">
            {olt.mac || "--"}
          </span>
        </div>
      </div>

      {/* Footer */}
      <div className="flex items-center justify-between mt-4 pt-3 border-t border-[var(--color-border)]">
        {olt.siteLabel && (
          <span className="text-xs text-[var(--color-text-muted)]">
            {olt.siteLabel}
          </span>
        )}
        <span className="text-xs text-[var(--color-text-muted)] ml-auto">
          {timeAgo(olt.lastSeen)}
        </span>
      </div>
    </button>
  );
}
