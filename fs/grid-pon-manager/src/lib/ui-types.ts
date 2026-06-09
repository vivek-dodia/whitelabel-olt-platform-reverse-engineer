// Shared types for the dashboard UI (mirrors the API responses).

export interface OltOpticsSummary {
  tx_pwr_mw: number | null;
  voltage: number | null;
  temperature: number | null;
  bias_ma: number | null;
  alarm: number | null;
}

export interface OltInfo {
  ip: string;
  name: string;
  status: "online" | "offline" | "unknown";
  siteLabel?: string;
  serial?: string;
  pn?: string;
  lastSeen?: number;
  hasPassword?: boolean;
  optics?: OltOpticsSummary;
}

export interface OnuRow {
  id: number;
  sn?: string;
  state?: string;
  uptime?: string | number;
  voltage: number | null;
  temperature: number | null;
  bias: number | null;
  tx_pwr_dbm: number | null;
  rx_pwr_dbm: number | null;
}

export interface WhitelistRow {
  sn: string;
  type: number;
  active: boolean;
}

export interface NetworkInfo {
  mode: "static" | "dhcp" | "unknown";
  modeRaw: number | null;
  ip: string | null;
  mask: string | null;
  gateway: string | null;
  mac: string | null;
  dhcp: string | null;
}
