"use client";

import { useEffect, useState, useCallback } from "react";
import { AddOltDialog } from "@/components/add-olt-dialog";
import { OltCard } from "@/components/olt-card";
import { OltDetail } from "@/components/olt-detail";

interface OltInfo {
  ip: string;
  mac: string;
  serial: string;
  name: string;
  status: "online" | "offline" | "unknown";
  lastSeen?: number;
  siteLabel?: string;
}

export default function Dashboard() {
  const [olts, setOlts] = useState<OltInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [showAdd, setShowAdd] = useState(false);
  const [selectedOlt, setSelectedOlt] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const fetchOlts = useCallback(async () => {
    try {
      const res = await fetch("/api/olts");
      const data = await res.json();
      setOlts(data);
    } catch (err) {
      console.error("Failed to fetch OLTs:", err);
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    fetchOlts();
    const interval = setInterval(fetchOlts, 15000);
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
      return <OltDetail olt={olt} onBack={() => setSelectedOlt(null)} />;
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
            Manage your GPON OLT SFP sticks across all sites
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
        <div className="rounded-xl border border-[var(--color-border)] bg-white p-5">
          <div className="text-xs font-medium text-[var(--color-text-muted)] uppercase tracking-wider">
            Total OLTs
          </div>
          <div className="text-3xl font-semibold mt-1 text-[var(--color-text-primary)]">
            {loading ? "--" : olts.length}
          </div>
        </div>
        <div className="rounded-xl border border-[var(--color-border)] bg-white p-5">
          <div className="text-xs font-medium text-[var(--color-text-muted)] uppercase tracking-wider">
            Online
          </div>
          <div className="text-3xl font-semibold mt-1 text-[var(--color-status-online)]">
            {loading ? "--" : onlineCount}
          </div>
        </div>
        <div className="rounded-xl border border-[var(--color-border)] bg-white p-5">
          <div className="text-xs font-medium text-[var(--color-text-muted)] uppercase tracking-wider">
            Offline
          </div>
          <div className="text-3xl font-semibold mt-1 text-[var(--color-status-offline)]">
            {loading ? "--" : offlineCount}
          </div>
        </div>
      </div>

      {/* OLT Grid */}
      {loading ? (
        <div className="text-center py-20 text-[var(--color-text-muted)] text-sm">
          Polling OLTs...
        </div>
      ) : olts.length === 0 ? (
        <div className="text-center py-20">
          <div className="text-[var(--color-text-muted)] text-sm">
            No OLTs configured
          </div>
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
              style={{
                animation: `fade-up 0.3s ease-out ${0.1 + i * 0.05}s both`,
              }}
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
