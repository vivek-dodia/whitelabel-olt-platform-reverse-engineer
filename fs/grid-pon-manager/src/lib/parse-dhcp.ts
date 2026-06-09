// Parser for the firmware's show_dhcp_client() text blob.
//
// The output mixes two syntaxes and three logical groups:
//   - leading `label:value` pairs (duplicate the configured values — ignored)
//   - `key=value` status + configured fields
//   - `active <field>=value` — the addressing actually in effect
//
// Example (static OLT):
//   src  ip addr:100.64.2.142  ... IP mode=Static  client MAC=00:00:50:00:22:c4
//   ip addr=100.64.2.142 ... gatewayMAC=04:f4:1c:a8:4d:5b
//   active local_ip=100.64.2.142 ... active gatewayMAC=04:f4:1c:a8:4d:5b

export interface KV {
  label: string;
  value: string;
}

export interface DhcpClientInfo {
  status: KV[];
  active: KV[];
  configured: KV[];
}

function grab(re: RegExp, s: string): string | undefined {
  const m = re.exec(s);
  return m ? m[1] : undefined;
}

export function parseDhcpClient(raw: string): DhcpClientInfo | null {
  if (!raw) return null;

  const push = (arr: KV[], label: string, value?: string) => {
    if (value !== undefined && value !== "") arr.push({ label, value });
  };

  const status: KV[] = [];
  push(status, "IP Mode", grab(/IP mode\s*=\s*(\S+)/i, raw));
  const state = grab(/\bstate\s*=\s*(\S+)/i, raw);
  push(status, "DHCP State", state);
  const running = grab(/\brunning\s*=\s*(\S+)/i, raw);
  push(status, "Running", running === "1" ? "yes" : running === "0" ? "no" : running);
  push(status, "VLAN", grab(/\bvlan\s*=\s*(\S+)/i, raw));
  push(status, "Retry Count", grab(/retry count\s*=\s*(\S+)/i, raw));
  push(status, "XID", grab(/\bxid\s*=\s*(\S+)/i, raw));
  push(status, "Client MAC", grab(/client MAC\s*=\s*([0-9A-Fa-f:]+)/i, raw));

  const active: KV[] = [];
  push(active, "IP Address", grab(/active local_ip\s*=\s*([\d.]+)/i, raw));
  push(active, "Subnet Mask", grab(/active subnet_mask\s*=\s*([\d.]+)/i, raw));
  push(active, "Gateway", grab(/active gateway\s*=\s*([\d.]+)/i, raw));
  push(active, "CAPWAP IP", grab(/active CAPWAP_ip\s*=\s*([\d.]+)/i, raw));
  push(active, "Gateway MAC", grab(/active gatewayMAC\s*=\s*([0-9A-Fa-f:]+)/i, raw));

  const configured: KV[] = [];
  push(configured, "IP Address", grab(/(?:^|\s)ip addr\s*=\s*([\d.]+)/i, raw));
  push(configured, "Subnet Mask", grab(/(?:^|\s)subnet mask\s*=\s*([\d.]+)/i, raw));
  push(configured, "Gateway", grab(/(?<!active )gateway\s*=\s*([\d.]+)/i, raw));
  push(configured, "CAPWAP IP", grab(/(?:^|\s)CAPWAP ip\s*=\s*([\d.]+)/i, raw));
  push(configured, "Gateway MAC", grab(/(?<!active )gatewayMAC\s*=\s*([0-9A-Fa-f:]+)/i, raw));

  if (status.length === 0 && active.length === 0 && configured.length === 0) return null;
  return { status, active, configured };
}
