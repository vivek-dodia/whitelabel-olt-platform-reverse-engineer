package api

import (
	"encoding/hex"
	"encoding/json"
	"log"
	"net"
	"net/http"
	"runtime"
	"strings"
	"time"

	"github.com/vivek-dodia/grid-agent/internal/config"
	"github.com/vivek-dodia/grid-agent/internal/discovery"
	"github.com/vivek-dodia/grid-agent/internal/olt"
)

type Server struct {
	cfg       *config.Config
	client    *olt.Client
	listener  *discovery.ARPListener
	mux       *http.ServeMux
	startTime time.Time
}

func NewServer(cfg *config.Config, client *olt.Client, listener *discovery.ARPListener) *Server {
	s := &Server{
		cfg:       cfg,
		client:    client,
		listener:  listener,
		mux:       http.NewServeMux(),
		startTime: time.Now(),
	}
	s.registerRoutes()
	return s
}

func (s *Server) ServeHTTP(w http.ResponseWriter, r *http.Request) {
	s.mux.ServeHTTP(w, r)
}

func (s *Server) registerRoutes() {
	// Agent info
	s.mux.HandleFunc("GET /", s.auth(s.handleInfo))

	// OLT discovery & listing
	s.mux.HandleFunc("GET /api/olts", s.auth(s.handleListOlts))
	s.mux.HandleFunc("POST /api/olts/discover", s.auth(s.handleDiscover))
	s.mux.HandleFunc("POST /api/olts/scan", s.auth(s.handleScan))
	s.mux.HandleFunc("DELETE /api/olts/{ip}", s.auth(s.handleRemoveOlt))

	// OLT status & auth
	s.mux.HandleFunc("GET /api/olts/{ip}", s.auth(s.handleGetOlt))
	s.mux.HandleFunc("POST /api/olts/{ip}/auth", s.auth(s.handleAuth))
	s.mux.HandleFunc("POST /api/olts/{ip}/reboot", s.auth(s.handleReboot))
	s.mux.HandleFunc("POST /api/olts/{ip}/ip", s.auth(s.handleChangeIP))

	// ONU management
	s.mux.HandleFunc("GET /api/olts/{ip}/onus", s.auth(s.handleGetOnus))
	s.mux.HandleFunc("GET /api/olts/{ip}/onus/{sn}/optics", s.auth(s.handleGetOnuOptics))

	// Whitelist management
	s.mux.HandleFunc("GET /api/olts/{ip}/whitelist", s.auth(s.handleGetWhitelist))
	s.mux.HandleFunc("GET /api/olts/{ip}/whitelist/mode", s.auth(s.handleGetWhitelistMode))
	s.mux.HandleFunc("POST /api/olts/{ip}/whitelist/mode", s.auth(s.handleSetWhitelistMode))
	s.mux.HandleFunc("POST /api/olts/{ip}/whitelist/add", s.auth(s.handleAddToWhitelist))
	s.mux.HandleFunc("POST /api/olts/{ip}/whitelist/remove", s.auth(s.handleRemoveFromWhitelist))

	// Service profiles
	s.mux.HandleFunc("GET /api/olts/{ip}/service-config", s.auth(s.handleGetServiceConfig))
	s.mux.HandleFunc("POST /api/olts/{ip}/service-config", s.auth(s.handleSetServiceConfig))

	// Alarms
	s.mux.HandleFunc("GET /api/olts/{ip}/alarms", s.auth(s.handleGetAlarms))
	s.mux.HandleFunc("GET /api/olts/{ip}/alarms/onus", s.auth(s.handleGetOnuAlarms))

	// Raw command
	s.mux.HandleFunc("POST /api/olts/{ip}/command", s.auth(s.handleCommand))
}

// ── Middleware ──

func (s *Server) auth(next http.HandlerFunc) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		if s.cfg.AuthToken == "" {
			next(w, r)
			return
		}
		token := r.Header.Get("Authorization")
		if token == "" {
			token = r.URL.Query().Get("token")
		}
		token = strings.TrimPrefix(token, "Bearer ")
		if token != s.cfg.AuthToken {
			writeJSON(w, http.StatusUnauthorized, map[string]string{"error": "unauthorized"})
			return
		}
		next(w, r)
	}
}

// ── Agent Info ──

func (s *Server) handleInfo(w http.ResponseWriter, r *http.Request) {
	olts := s.client.GetAllOlts()
	online := 0
	for _, o := range olts {
		if o.Status == "online" {
			online++
		}
	}
	var ips []string
	addrs, _ := net.InterfaceAddrs()
	for _, a := range addrs {
		if ipnet, ok := a.(*net.IPNet); ok && !ipnet.IP.IsLoopback() && ipnet.IP.To4() != nil {
			ips = append(ips, ipnet.IP.String())
		}
	}
	writeJSON(w, http.StatusOK, map[string]any{
		"agent": s.cfg.AgentName, "site": s.cfg.SiteLabel, "version": "0.1.0",
		"uptime_sec": int(time.Since(s.startTime).Seconds()),
		"os": runtime.GOOS, "arch": runtime.GOARCH, "local_ips": ips,
		"olt_count": len(olts), "olt_online": online,
	})
}

// ── OLT Discovery & Listing ──

func (s *Server) handleListOlts(w http.ResponseWriter, r *http.Request) {
	olts := s.client.GetAllOlts()
	if olts == nil {
		olts = []*olt.OltStatus{}
	}
	writeJSON(w, http.StatusOK, olts)
}

func (s *Server) handleDiscover(w http.ResponseWriter, r *http.Request) {
	var body struct{ IP string `json:"ip"` }
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil || body.IP == "" {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "ip is required"})
		return
	}
	status, err := s.listener.DiscoverOlt(body.IP)
	if err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, status)
}

func (s *Server) handleScan(w http.ResponseWriter, r *http.Request) {
	var body struct{ Subnet string `json:"subnet"` }
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil || body.Subnet == "" {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "subnet required (e.g. 100.64.2)"})
		return
	}
	found := s.listener.ScanSubnet(body.Subnet)
	if found == nil {
		found = []*olt.OltStatus{}
	}
	writeJSON(w, http.StatusOK, found)
}

func (s *Server) handleRemoveOlt(w http.ResponseWriter, r *http.Request) {
	ip := r.PathValue("ip")
	s.client.RemoveOlt(ip)
	writeJSON(w, http.StatusOK, map[string]string{"removed": ip})
}

// ── OLT Status & Auth ──

func (s *Server) handleGetOlt(w http.ResponseWriter, r *http.Request) {
	ip := r.PathValue("ip")
	status, err := s.client.ShakeHand(ip)
	if err != nil {
		cached := s.client.GetOlt(ip)
		if cached != nil {
			cached.Status = "offline"
			writeJSON(w, http.StatusOK, cached)
			return
		}
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, status)
}

func (s *Server) handleAuth(w http.ResponseWriter, r *http.Request) {
	ip := r.PathValue("ip")
	var body struct{ Mode string `json:"mode"` }
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
		body.Mode = "both"
	}
	result := map[string]bool{}
	if body.Mode == "write" || body.Mode == "both" {
		granted, _ := s.client.EnableWrite(ip)
		result["write"] = granted
	}
	if body.Mode == "read" || body.Mode == "both" {
		granted, _ := s.client.EnableRead(ip)
		result["read"] = granted
	}
	writeJSON(w, http.StatusOK, map[string]any{"ip": ip, "auth": result})
}

func (s *Server) handleReboot(w http.ResponseWriter, r *http.Request) {
	ip := r.PathValue("ip")
	err := s.client.RebootOlt(ip)
	if err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, map[string]string{"status": "rebooting", "ip": ip})
}

func (s *Server) handleChangeIP(w http.ResponseWriter, r *http.Request) {
	ip := r.PathValue("ip")
	var body struct{ NewIP string `json:"new_ip"` }
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil || body.NewIP == "" {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "new_ip required"})
		return
	}
	err := s.client.ChangeOltIP(ip, body.NewIP)
	if err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, map[string]string{"status": "ip_changed", "old_ip": ip, "new_ip": body.NewIP})
}

// ── ONU Management ──

func (s *Server) handleGetOnus(w http.ResponseWriter, r *http.Request) {
	ip := r.PathValue("ip")
	onus, err := s.client.GetOnuStatus(ip)
	if err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	if onus == nil {
		onus = []olt.OnuInfo{}
	}
	writeJSON(w, http.StatusOK, onus)
}

func (s *Server) handleGetOnuOptics(w http.ResponseWriter, r *http.Request) {
	ip := r.PathValue("ip")
	sn := r.PathValue("sn")
	info, err := s.client.GetOnuOptics(ip, sn)
	if err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, info)
}

// ── Whitelist Management ──

func (s *Server) handleGetWhitelist(w http.ResponseWriter, r *http.Request) {
	ip := r.PathValue("ip")
	count, err := s.client.GetWhitelistCount(ip)
	if err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	data, _ := s.client.GetWhitelist(ip)
	writeJSON(w, http.StatusOK, map[string]any{
		"ip": ip, "count": count, "raw": hex.EncodeToString(data),
	})
}

func (s *Server) handleGetWhitelistMode(w http.ResponseWriter, r *http.Request) {
	ip := r.PathValue("ip")
	mode, err := s.client.GetWhitelistMode(ip)
	if err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{"ip": ip, "mode": mode.Mode})
}

func (s *Server) handleSetWhitelistMode(w http.ResponseWriter, r *http.Request) {
	ip := r.PathValue("ip")
	var body struct{ Mode string `json:"mode"` }
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "mode required (whitelist or graylist)"})
		return
	}
	var err error
	switch body.Mode {
	case "whitelist":
		err = s.client.SetWhitelistMode(ip)
	case "graylist":
		err = s.client.SetGraylistMode(ip)
	default:
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "mode must be 'whitelist' or 'graylist'"})
		return
	}
	if err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, map[string]string{"ip": ip, "mode": body.Mode, "status": "applied"})
}

func (s *Server) handleAddToWhitelist(w http.ResponseWriter, r *http.Request) {
	ip := r.PathValue("ip")
	var body struct {
		SN             string `json:"sn"`
		ServiceProfile int    `json:"service_profile"`
	}
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil || body.SN == "" {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "sn required, service_profile optional (1-5, default 1)"})
		return
	}
	if body.ServiceProfile < 1 || body.ServiceProfile > 5 {
		body.ServiceProfile = 1
	}
	err := s.client.AddOnuToWhitelist(ip, body.SN, body.ServiceProfile)
	if err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{
		"ip": ip, "sn": body.SN, "service_profile": body.ServiceProfile, "status": "added",
	})
}

func (s *Server) handleRemoveFromWhitelist(w http.ResponseWriter, r *http.Request) {
	ip := r.PathValue("ip")
	var body struct{ SN string `json:"sn"` }
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil || body.SN == "" {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "sn required"})
		return
	}
	err := s.client.RemoveOnuFromWhitelist(ip, body.SN)
	if err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, map[string]string{"ip": ip, "sn": body.SN, "status": "removed"})
}

// ── Service Profiles ──

func (s *Server) handleGetServiceConfig(w http.ResponseWriter, r *http.Request) {
	ip := r.PathValue("ip")
	data, err := s.client.GetServiceConfig(ip)
	if err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, map[string]string{"ip": ip, "raw": hex.EncodeToString(data)})
}

func (s *Server) handleSetServiceConfig(w http.ResponseWriter, r *http.Request) {
	ip := r.PathValue("ip")
	var body struct{ Data string `json:"data"` }
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil || body.Data == "" {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "data required (hex string)"})
		return
	}
	data, err := hex.DecodeString(body.Data)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "invalid hex"})
		return
	}
	err = s.client.SetServiceConfig(ip, data)
	if err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, map[string]string{"ip": ip, "status": "applied"})
}

// ── Alarms ──

func (s *Server) handleGetAlarms(w http.ResponseWriter, r *http.Request) {
	ip := r.PathValue("ip")
	alarms, err := s.client.GetAlarms(ip)
	if err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, alarms)
}

func (s *Server) handleGetOnuAlarms(w http.ResponseWriter, r *http.Request) {
	ip := r.PathValue("ip")
	data, err := s.client.GetOnuAlarms(ip)
	if err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, map[string]string{"ip": ip, "raw": hex.EncodeToString(data)})
}

// ── Raw Command ──

func (s *Server) handleCommand(w http.ResponseWriter, r *http.Request) {
	ip := r.PathValue("ip")
	var body struct {
		Cmd  int    `json:"cmd"`
		Data string `json:"data"`
	}
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "invalid request"})
		return
	}
	var data []byte
	if body.Data != "" {
		var err error
		data, err = hex.DecodeString(body.Data)
		if err != nil {
			writeJSON(w, http.StatusBadRequest, map[string]string{"error": "invalid hex data"})
			return
		}
	}
	resp, err := s.client.SendRawCommand(ip, olt.CommandCode(body.Cmd), data)
	if err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{
		"ip": ip, "cmd": body.Cmd, "response": hex.EncodeToString(resp), "length": len(resp),
	})
}

func writeJSON(w http.ResponseWriter, status int, data any) {
	w.Header().Set("Content-Type", "application/json")
	w.Header().Set("Access-Control-Allow-Origin", "*")
	w.Header().Set("Access-Control-Allow-Headers", "Authorization, Content-Type")
	w.Header().Set("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS")
	w.WriteHeader(status)
	if err := json.NewEncoder(w).Encode(data); err != nil {
		log.Printf("JSON encode error: %v", err)
	}
}
