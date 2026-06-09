"use client";

import { useEffect, useState, useCallback } from "react";
import { AddOltDialog } from "@/components/add-olt-dialog";
import { OltCard } from "@/components/olt-card";
import { OltDetail } from "@/components/olt-detail";
import type { OltInfo } from "@/lib/ui-types";

export default function Dashboard() {
  const [olts, setOlts] = useState<OltInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [showAdd, setShowAdd] = useState(false);
  const [selectedOlt, setSelectedOlt] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  // Load the config list instantly (with last-known cached status), then probe
  // each OLT live and merge the fresh device data into its card as it arrives.
  const fetchOlts = useCallback(async () => {
    try {
      const res = await fetch("/api/olts");
      const list: OltInfo[] = await res.json();
      setOlts(list);

      await Promise.all(
        list.map(async (olt) => {
          try {
            const r = await fetch(`/api/olts/${olt.ip}/device`);
            const dev = await r.json();
            setOlts((prev) =>
              prev.map((o) =>
                o.ip === olt.ip
                  ? {
                      ...o,
                      status: dev.status ?? (r.ok ? "online" : "offline"),
                      serial: dev.olt_sn ?? o.serial,
                      pn: dev.pn ?? o.pn,
                      optics: dev.optics ?? o.optics,
                      lastSeen: r.ok ? Date.now() : o.lastSeen,
                    }
                  : o
              )
            );
          } catch {
            setOlts((prev) =>
              prev.map((o) => (o.ip === olt.ip ? { ...o, status: "offline" } : o))
            );
          }
        })
      );
    } catch (err) {
      console.error("Failed to fetch OLTs:", err);
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    fetchOlts();
    const interval = setInterval(fetchOlts, 30000);
    return () => clearInterval(interval);
  }, [fetchOlts]);

  const handleRefresh = () => {
    setRefreshing(true);
    fetchOlts();
  };

  const onlineCount = olts.filter((o) => o.status === "online").length;
  const offlineCount = olts.filter((o) => o.status === "offline").length;

  if (selectedOlt) {
    const olt = olts.find((o) => o.ip === selectedOlt);
    if (olt) {
      return (
        <OltDetail
          olt={olt}
          onBack={() => setSelectedOlt(null)}
          onRemoved={() => {
            setSelectedOlt(null);
            fetchOlts();
          }}
        />
      );
    }
  }

  return (
    <div className="max-w-7xl mx-auto px-6 py-8">
      {/* Header */}
      <div
        className="flex flex-col sm:flex-row sm:items-end justify-between gap-4 mb-8"
        style={{ animation: "fade-up 0.4s ease-out" }}
      >
        <div>
          <h1 className="text-2xl font-semibold tracking-tight text-[var(--color-text-primary)]">
            OLT Fleet
          </h1>
          <p className="text-sm text-[var(--color-text-secondary)] mt-1">
            FS GPON OLT sticks (v2 firmware) — managed over the port-128 web API
          </p>
        </div>
        <div className="flex gap-2">
          <button
            onClick={handleRefresh}
            disabled={refreshing}
            className="px-4 py-2 text-sm font-medium rounded-lg border border-[var(--color-border)] bg-white text-[var(--color-text-primary)] hover:border-[var(--color-grid-400)] hover:bg-[var(--color-grid-50)] transition-all cursor-pointer disabled:opacity-50"
          >
            {refreshing ? "Refreshing..." : "Refresh"}
          </button>
          <button
            onClick={() => setShowAdd(true)}
            className="px-4 py-2 text-sm font-medium rounded-lg bg-[var(--color-text-primary)] text-white hover:opacity-85 transition-opacity cursor-pointer"
          >
            Add OLT
          </button>
        </div>
      </div>

      {/* Stats */}
      <div
        className="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-8"
        style={{ animation: "fade-up 0.4s ease-out 0.05s both" }}
      >
        <StatCard label="Total OLTs" value={loading ? "--" : String(olts.length)} />
        <StatCard
          label="Online"
          value={loading ? "--" : String(onlineCount)}
          color="var(--color-status-online)"
        />
        <StatCard
          label="Offline"
          value={loading ? "--" : String(offlineCount)}
          color="var(--color-status-offline)"
        />
      </div>

      {/* OLT Grid */}
      {loading ? (
        <div className="text-center py-20 text-[var(--color-text-muted)] text-sm">
          Loading fleet...
        </div>
      ) : olts.length === 0 ? (
        <div className="text-center py-20">
          <div className="text-[var(--color-text-muted)] text-sm">No OLTs configured</div>
          <button
            onClick={() => setShowAdd(true)}
            className="mt-4 px-4 py-2 text-sm font-medium rounded-lg bg-[var(--color-text-primary)] text-white hover:opacity-85 transition-opacity cursor-pointer"
          >
            Add your first OLT
          </button>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {olts.map((olt, i) => (
            <div
              key={olt.ip}
              style={{ animation: `fade-up 0.3s ease-out ${0.1 + i * 0.05}s both` }}
            >
              <OltCard olt={olt} onClick={() => setSelectedOlt(olt.ip)} />
            </div>
          ))}
        </div>
      )}

      {/* Add Dialog */}
      {showAdd && (
        <AddOltDialog
          onClose={() => setShowAdd(false)}
          onAdded={() => {
            setShowAdd(false);
            fetchOlts();
          }}
        />
      )}
    </div>
  );
}

function StatCard({ label, value, color }: { label: string; value: string; color?: string }) {
  return (
    <div className="rounded-xl border border-[var(--color-border)] bg-white p-5">
      <div className="text-xs font-medium text-[var(--color-text-muted)] uppercase tracking-wider">
        {label}
      </div>
      <div
        className="text-3xl font-semibold mt-1"
        style={{ color: color ?? "var(--color-text-primary)" }}
      >
        {value}
      </div>
    </div>
  );
}
