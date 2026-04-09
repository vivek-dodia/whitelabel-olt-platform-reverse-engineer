"use client";

import { useState } from "react";

export function AddOltDialog({
  onClose,
  onAdded,
}: {
  onClose: () => void;
  onAdded: () => void;
}) {
  const [ip, setIp] = useState("");
  const [name, setName] = useState("");
  const [siteLabel, setSiteLabel] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    setSubmitting(true);

    try {
      const res = await fetch("/api/olts", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ ip, name, siteLabel: siteLabel || undefined }),
      });

      if (!res.ok) {
        const data = await res.json();
        setError(data.error || "Failed to add OLT");
        return;
      }

      onAdded();
    } catch {
      setError("Network error");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center"
      style={{ animation: "fade-up 0.2s ease-out" }}
    >
      {/* Overlay */}
      <div
        className="absolute inset-0 bg-[var(--color-text-primary)]/40 backdrop-blur-sm"
        onClick={onClose}
      />

      {/* Dialog */}
      <div
        className="relative bg-white rounded-2xl border border-[var(--color-border)] shadow-2xl w-full max-w-md mx-4 p-6"
        style={{ animation: "slide-up 0.3s ease-out" }}
      >
        <div className="flex items-center justify-between mb-6">
          <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">
            Add OLT
          </h2>
          <button
            onClick={onClose}
            className="w-8 h-8 flex items-center justify-center rounded-lg hover:bg-[var(--color-grid-50)] transition-colors cursor-pointer text-[var(--color-text-muted)]"
          >
            <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
              <path d="M4 4L12 12M12 4L4 12" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
            </svg>
          </button>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-xs font-medium text-[var(--color-text-muted)] uppercase tracking-wider mb-1.5">
              OLT IP Address
            </label>
            <input
              type="text"
              value={ip}
              onChange={(e) => setIp(e.target.value)}
              placeholder="100.64.2.200"
              required
              className="w-full px-3.5 py-2.5 text-sm rounded-lg border border-[var(--color-border)] bg-white outline-none focus:border-[var(--color-grid-500)] focus:ring-2 focus:ring-[var(--color-grid-500)]/10 transition-all font-[family-name:var(--font-mono)]"
            />
          </div>

          <div>
            <label className="block text-xs font-medium text-[var(--color-text-muted)] uppercase tracking-wider mb-1.5">
              Name
            </label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Site A - OLT 1"
              required
              className="w-full px-3.5 py-2.5 text-sm rounded-lg border border-[var(--color-border)] bg-white outline-none focus:border-[var(--color-grid-500)] focus:ring-2 focus:ring-[var(--color-grid-500)]/10 transition-all"
            />
          </div>

          <div>
            <label className="block text-xs font-medium text-[var(--color-text-muted)] uppercase tracking-wider mb-1.5">
              Site Label
              <span className="text-[var(--color-text-muted)] font-normal normal-case tracking-normal ml-1">
                (optional)
              </span>
            </label>
            <input
              type="text"
              value={siteLabel}
              onChange={(e) => setSiteLabel(e.target.value)}
              placeholder="Office Lab"
              className="w-full px-3.5 py-2.5 text-sm rounded-lg border border-[var(--color-border)] bg-white outline-none focus:border-[var(--color-grid-500)] focus:ring-2 focus:ring-[var(--color-grid-500)]/10 transition-all"
            />
          </div>

          {error && (
            <div className="text-sm text-[var(--color-status-offline)] bg-red-50 px-3 py-2 rounded-lg">
              {error}
            </div>
          )}

          <div className="flex gap-2 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 px-4 py-2.5 text-sm font-medium rounded-lg border border-[var(--color-border)] bg-white text-[var(--color-text-primary)] hover:bg-[var(--color-grid-50)] transition-colors cursor-pointer"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="flex-1 px-4 py-2.5 text-sm font-medium rounded-lg bg-[var(--color-text-primary)] text-white hover:opacity-85 transition-opacity cursor-pointer disabled:opacity-50"
            >
              {submitting ? "Adding..." : "Add OLT"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
