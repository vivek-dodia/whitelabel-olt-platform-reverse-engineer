import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "GRID PON Manager",
  description: "GPON OLT SFP Stick Management Platform",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className="h-full antialiased">
      <body className="min-h-full flex flex-col font-[family-name:var(--font-sans)]">
        <nav className="sticky top-0 z-40 border-b border-[var(--color-border)] bg-white/80 backdrop-blur-xl">
          <div className="max-w-7xl mx-auto px-6 h-14 flex items-center justify-between">
            <a href="/" className="flex items-center gap-2.5">
              <div className="w-7 h-7 rounded-lg bg-[var(--color-text-primary)] flex items-center justify-center">
                <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
                  <rect x="1" y="1" width="5" height="5" rx="1" fill="white" />
                  <rect x="8" y="1" width="5" height="5" rx="1" fill="white" opacity="0.5" />
                  <rect x="1" y="8" width="5" height="5" rx="1" fill="white" opacity="0.5" />
                  <rect x="8" y="8" width="5" height="5" rx="1" fill="white" opacity="0.3" />
                </svg>
              </div>
              <span className="text-[15px] font-semibold tracking-tight text-[var(--color-text-primary)]">
                GRID
              </span>
              <span className="text-[13px] font-normal text-[var(--color-text-muted)] hidden sm:inline">
                PON Manager
              </span>
            </a>
            <div className="flex items-center gap-3">
              <span className="text-[12px] font-mono text-[var(--color-text-muted)]">v0.1.0</span>
            </div>
          </div>
        </nav>
        <main className="flex-1">{children}</main>
      </body>
    </html>
  );
}
