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
	s.mux.HandleFunc("GET /", s.auth(s.handleInfo))
	s.mux.HandleFunc("GET /api/olts", s.auth(s.handleListOlts))
	s.mux.HandleFunc("POST /api/olts/discover", s.auth(s.handleDiscover))
	s.mux.HandleFunc("POST /api/olts/scan", s.auth(s.handleScan))
	s.mux.HandleFunc("DELETE /api/olts/{ip}", s.auth(s.handleRemoveOlt))
	s.mux.HandleFunc("GET /api/olts/{ip}", s.auth(s.handleGetOlt))
	s.mux.HandleFunc("GET /api/olts/{ip}/onus", s.auth(s.handleGetOnus))
	s.mux.HandleFunc("GET /api/olts/{ip}/alarms", s.auth(s.handleGetAlarms))
	s.mux.HandleFunc("POST /api/olts/{ip}/auth", s.auth(s.handleAuth))
	s.mux.HandleFunc("POST /api/olts/{ip}/command", s.auth(s.handleCommand))
}

// Auth middleware
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

// GET / — agent info
func (s *Server) handleInfo(w http.ResponseWriter, r *http.Request) {
	olts := s.client.GetAllOlts()
	online := 0
	for _, o := range olts {
		if o.Status == "online" {
			online++
		}
	}

	// Get local IPs
	var ips []string
	addrs, _ := net.InterfaceAddrs()
	for _, a := range addrs {
		if ipnet, ok := a.(*net.IPNet); ok && !ipnet.IP.IsLoopback() && ipnet.IP.To4() != nil {
			ips = append(ips, ipnet.IP.String())
		}
	}

	writeJSON(w, http.StatusOK, map[string]any{
		"agent":      s.cfg.AgentName,
		"site":       s.cfg.SiteLabel,
		"version":    "0.1.0",
		"uptime_sec": int(time.Since(s.startTime).Seconds()),
		"os":         runtime.GOOS,
		"arch":       runtime.GOARCH,
		"local_ips":  ips,
		"olt_count":  len(olts),
		"olt_online": online,
	})
}

// GET /api/olts — list all tracked OLTs
func (s *Server) handleListOlts(w http.ResponseWriter, r *http.Request) {
	olts := s.client.GetAllOlts()
	if olts == nil {
		olts = []*olt.OltStatus{}
	}
	writeJSON(w, http.StatusOK, olts)
}

// POST /api/olts/discover — add OLT by IP
func (s *Server) handleDiscover(w http.ResponseWriter, r *http.Request) {
	var body struct {
		IP string `json:"ip"`
	}
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

// POST /api/olts/scan — scan subnet for OLTs
func (s *Server) handleScan(w http.ResponseWriter, r *http.Request) {
	var body struct {
		Subnet string `json:"subnet"`
	}
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil || body.Subnet == "" {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "subnet is required (e.g., 100.64.2)"})
		return
	}

	found := s.listener.ScanSubnet(body.Subnet)
	if found == nil {
		found = []*olt.OltStatus{}
	}
	writeJSON(w, http.StatusOK, found)
}

// DELETE /api/olts/{ip} — remove OLT from tracking
func (s *Server) handleRemoveOlt(w http.ResponseWriter, r *http.Request) {
	ip := r.PathValue("ip")
	s.client.RemoveOlt(ip)
	writeJSON(w, http.StatusOK, map[string]string{"removed": ip})
}

// GET /api/olts/{ip} — get single OLT status (live shake_hand)
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

// GET /api/olts/{ip}/onus — get ONU list
func (s *Server) handleGetOnus(w http.ResponseWriter, r *http.Request) {
	ip := r.PathValue("ip")
	onus, err := s.client.GetOnuStatus(ip)
	if err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, onus)
}

// GET /api/olts/{ip}/alarms — get OLT alarms
func (s *Server) handleGetAlarms(w http.ResponseWriter, r *http.Request) {
	ip := r.PathValue("ip")
	alarms, err := s.client.GetAlarms(ip)
	if err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, alarms)
}

// POST /api/olts/{ip}/auth — enable read/write
func (s *Server) handleAuth(w http.ResponseWriter, r *http.Request) {
	ip := r.PathValue("ip")
	var body struct {
		Mode string `json:"mode"` // "read", "write", "both"
	}
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

// POST /api/olts/{ip}/command — send raw command
func (s *Server) handleCommand(w http.ResponseWriter, r *http.Request) {
	ip := r.PathValue("ip")
	var body struct {
		Cmd  int    `json:"cmd"`
		Data string `json:"data"` // hex string
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
		"ip":       ip,
		"cmd":      body.Cmd,
		"response": hex.EncodeToString(resp),
		"length":   len(resp),
	})
}

func writeJSON(w http.ResponseWriter, status int, data any) {
	w.Header().Set("Content-Type", "application/json")
	w.Header().Set("Access-Control-Allow-Origin", "*")
	w.WriteHeader(status)
	if err := json.NewEncoder(w).Encode(data); err != nil {
		log.Printf("JSON encode error: %v", err)
	}
}
