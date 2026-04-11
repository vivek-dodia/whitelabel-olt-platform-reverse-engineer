package api

import (
	"crypto/subtle"
	"encoding/hex"
	"encoding/json"
	"errors"
	"io"
	"log/slog"
	"net"
	"net/http"
	"runtime/debug"
	"strings"
	"sync"
	"time"

	"github.com/vivek-dodia/grid-agent/internal/config"
	"github.com/vivek-dodia/grid-agent/internal/discovery"
	"github.com/vivek-dodia/grid-agent/internal/olt"
	"golang.org/x/time/rate"
)

const (
	MaxRequestBodyBytes = 64 * 1024 // 64 KB is plenty for JSON commands
	DefaultRateBurst    = 20
	DefaultRatePerSec   = 10
	MaxOltsInScan       = 254
)

type Server struct {
	cfg       *config.Config
	client    *olt.Client
	listener  *discovery.ARPListener
	mux       *http.ServeMux
	handler   http.Handler
	startTime time.Time
	log       *slog.Logger

	limiterMu sync.Mutex
	limiters  map[string]*rate.Limiter
}

func NewServer(cfg *config.Config, client *olt.Client, listener *discovery.ARPListener, log *slog.Logger) *Server {
	if log == nil {
		log = slog.Default()
	}
	s := &Server{
		cfg:       cfg,
		client:    client,
		listener:  listener,
		mux:       http.NewServeMux(),
		startTime: time.Now(),
		log:       log,
		limiters:  make(map[string]*rate.Limiter),
	}
	s.registerRoutes()

	// Middleware chain: recovery -> rateLimit -> auth -> handler
	s.handler = s.recover(s.mux)
	return s
}

func (s *Server) ServeHTTP(w http.ResponseWriter, r *http.Request) {
	s.handler.ServeHTTP(w, r)
}

func (s *Server) registerRoutes() {
	s.mux.HandleFunc("GET /", s.wrap(s.handleInfo))

	// OLT discovery & listing
	s.mux.HandleFunc("GET /api/olts", s.wrap(s.handleListOlts))
	s.mux.HandleFunc("POST /api/olts/discover", s.wrap(s.handleDiscover))
	s.mux.HandleFunc("POST /api/olts/scan", s.wrap(s.handleScan))
	s.mux.HandleFunc("DELETE /api/olts/{ip}", s.wrap(s.handleRemoveOlt))

	// OLT status & auth
	s.mux.HandleFunc("GET /api/olts/{ip}", s.wrap(s.handleGetOlt))
	s.mux.HandleFunc("POST /api/olts/{ip}/auth", s.wrap(s.handleAuth))
	s.mux.HandleFunc("POST /api/olts/{ip}/reboot", s.wrap(s.handleReboot))
	s.mux.HandleFunc("POST /api/olts/{ip}/ip", s.wrap(s.handleChangeIP))

	// ONU management
	s.mux.HandleFunc("GET /api/olts/{ip}/onus", s.wrap(s.handleGetOnus))
	s.mux.HandleFunc("GET /api/olts/{ip}/onus/{sn}/optics", s.wrap(s.handleGetOnuOptics))

	// Whitelist management
	s.mux.HandleFunc("GET /api/olts/{ip}/whitelist", s.wrap(s.handleGetWhitelist))
	s.mux.HandleFunc("GET /api/olts/{ip}/whitelist/mode", s.wrap(s.handleGetWhitelistMode))
	s.mux.HandleFunc("POST /api/olts/{ip}/whitelist/mode", s.wrap(s.handleSetWhitelistMode))
	s.mux.HandleFunc("POST /api/olts/{ip}/whitelist/add", s.wrap(s.handleAddToWhitelist))
	s.mux.HandleFunc("POST /api/olts/{ip}/whitelist/remove", s.wrap(s.handleRemoveFromWhitelist))

	// Service profiles
	s.mux.HandleFunc("GET /api/olts/{ip}/service-config", s.wrap(s.handleGetServiceConfig))
	s.mux.HandleFunc("POST /api/olts/{ip}/service-config", s.wrap(s.handleSetServiceConfig))

	// Alarms
	s.mux.HandleFunc("GET /api/olts/{ip}/alarms", s.wrap(s.handleGetAlarms))
	s.mux.HandleFunc("GET /api/olts/{ip}/alarms/onus", s.wrap(s.handleGetOnuAlarms))

	// Raw command
	s.mux.HandleFunc("POST /api/olts/{ip}/command", s.wrap(s.handleCommand))
}

// ── Middleware ──

// wrap applies auth + rate limit + CORS to a handler. Order matters: CORS first
// so preflight works, then rate limit (cheap), then auth.
func (s *Server) wrap(next http.HandlerFunc) http.HandlerFunc {
	return s.cors(s.rateLimit(s.auth(next)))
}

func (s *Server) recover(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		defer func() {
			if rec := recover(); rec != nil {
				s.log.Error("panic recovered",
					"err", rec,
					"path", r.URL.Path,
					"stack", string(debug.Stack()))
				writeJSON(w, http.StatusInternalServerError, map[string]string{"error": "internal server error"})
			}
		}()
		// Enforce body size limit for every request
		r.Body = http.MaxBytesReader(w, r.Body, MaxRequestBodyBytes)
		next.ServeHTTP(w, r)
	})
}

func (s *Server) cors(next http.HandlerFunc) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Access-Control-Allow-Origin", "*")
		w.Header().Set("Access-Control-Allow-Headers", "Authorization, Content-Type")
		w.Header().Set("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS")
		if r.Method == http.MethodOptions {
			w.WriteHeader(http.StatusNoContent)
			return
		}
		next(w, r)
	}
}

func (s *Server) rateLimit(next http.HandlerFunc) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		ip := clientIP(r)
		s.limiterMu.Lock()
		lim, ok := s.limiters[ip]
		if !ok {
			lim = rate.NewLimiter(rate.Limit(DefaultRatePerSec), DefaultRateBurst)
			s.limiters[ip] = lim
			// Cleanup limiter map if it grows large
			if len(s.limiters) > 1024 {
				for k := range s.limiters {
					delete(s.limiters, k)
					if len(s.limiters) <= 512 {
						break
					}
				}
			}
		}
		s.limiterMu.Unlock()

		if !lim.Allow() {
			writeJSON(w, http.StatusTooManyRequests, map[string]string{"error": "rate limit exceeded"})
			return
		}
		next(w, r)
	}
}

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

		// Constant-time compare to prevent timing attacks
		expected := []byte(s.cfg.AuthToken)
		provided := []byte(token)
		if len(provided) != len(expected) || subtle.ConstantTimeCompare(provided, expected) != 1 {
			writeJSON(w, http.StatusUnauthorized, map[string]string{"error": "unauthorized"})
			return
		}
		next(w, r)
	}
}

// ── Helpers ──

func decodeBody(r *http.Request, v any) error {
	dec := json.NewDecoder(r.Body)
	dec.DisallowUnknownFields()
	if err := dec.Decode(v); err != nil {
		if errors.Is(err, io.EOF) {
			return errors.New("empty body")
		}
		return err
	}
	return nil
}

// parseIPParam extracts and validates an IPv4 address from the URL path.
func parseIPParam(r *http.Request) (string, error) {
	raw := r.PathValue("ip")
	parsed := net.ParseIP(raw)
	if parsed == nil || parsed.To4() == nil {
		return "", errors.New("invalid ipv4 address")
	}
	return parsed.To4().String(), nil
}

func clientIP(r *http.Request) string {
	host, _, err := net.SplitHostPort(r.RemoteAddr)
	if err != nil {
		return r.RemoteAddr
	}
	return host
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
		"agent":      s.cfg.AgentName,
		"site":       s.cfg.SiteLabel,
		"version":    "0.1.0",
		"uptime_sec": int(time.Since(s.startTime).Seconds()),
		"local_ips":  ips,
		"olt_count":  len(olts),
		"olt_online": online,
		"pending":    s.client.PendingCount(),
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
	var body struct {
		IP string `json:"ip"`
	}
	if err := decodeBody(r, &body); err != nil || body.IP == "" {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "ip is required"})
		return
	}
	status, err := s.listener.DiscoverOlt(r.Context(), body.IP)
	if err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, status)
}

func (s *Server) handleScan(w http.ResponseWriter, r *http.Request) {
	var body struct {
		Subnet string `json:"subnet"`
	}
	if err := decodeBody(r, &body); err != nil || body.Subnet == "" {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "subnet required (e.g. 100.64.2)"})
		return
	}
	found, err := s.listener.ScanSubnet(r.Context(), body.Subnet)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}
	if found == nil {
		found = []*olt.OltStatus{}
	}
	writeJSON(w, http.StatusOK, found)
}

func (s *Server) handleRemoveOlt(w http.ResponseWriter, r *http.Request) {
	ip, err := parseIPParam(r)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}
	s.client.RemoveOlt(ip)
	writeJSON(w, http.StatusOK, map[string]string{"removed": ip})
}

// ── OLT Status & Auth ──

func (s *Server) handleGetOlt(w http.ResponseWriter, r *http.Request) {
	ip, err := parseIPParam(r)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}
	status, shakeErr := s.client.ShakeHand(r.Context(), ip)
	if shakeErr != nil {
		cached := s.client.GetOlt(ip)
		if cached != nil {
			cached.Status = "offline"
			writeJSON(w, http.StatusOK, cached)
			return
		}
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": shakeErr.Error()})
		return
	}
	writeJSON(w, http.StatusOK, status)
}

func (s *Server) handleAuth(w http.ResponseWriter, r *http.Request) {
	ip, err := parseIPParam(r)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}
	var body struct {
		Mode string `json:"mode"`
	}
	if err := decodeBody(r, &body); err != nil {
		body.Mode = "both"
	}
	if body.Mode == "" {
		body.Mode = "both"
	}
	result := map[string]bool{}
	if body.Mode == "write" || body.Mode == "both" {
		granted, _ := s.client.EnableWrite(r.Context(), ip)
		result["write"] = granted
	}
	if body.Mode == "read" || body.Mode == "both" {
		granted, _ := s.client.EnableRead(r.Context(), ip)
		result["read"] = granted
	}
	writeJSON(w, http.StatusOK, map[string]any{"ip": ip, "auth": result})
}

func (s *Server) handleReboot(w http.ResponseWriter, r *http.Request) {
	ip, err := parseIPParam(r)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}
	if err := s.client.RebootOlt(r.Context(), ip); err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, map[string]string{"status": "rebooting", "ip": ip})
}

func (s *Server) handleChangeIP(w http.ResponseWriter, r *http.Request) {
	ip, err := parseIPParam(r)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}
	var body struct {
		NewIP string `json:"new_ip"`
	}
	if err := decodeBody(r, &body); err != nil || body.NewIP == "" {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "new_ip required"})
		return
	}
	if parsed := net.ParseIP(body.NewIP); parsed == nil || parsed.To4() == nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "new_ip is not a valid ipv4"})
		return
	}
	if err := s.client.ChangeOltIP(r.Context(), ip, body.NewIP); err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, map[string]string{"status": "ip_changed", "old_ip": ip, "new_ip": body.NewIP})
}

// ── ONU Management ──

func (s *Server) handleGetOnus(w http.ResponseWriter, r *http.Request) {
	ip, err := parseIPParam(r)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}
	onus, err := s.client.GetOnuStatus(r.Context(), ip)
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
	ip, err := parseIPParam(r)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}
	sn := r.PathValue("sn")
	info, err := s.client.GetOnuOptics(r.Context(), ip, sn)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, info)
}

// ── Whitelist ──

func (s *Server) handleGetWhitelist(w http.ResponseWriter, r *http.Request) {
	ip, err := parseIPParam(r)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}
	count, err := s.client.GetWhitelistCount(r.Context(), ip)
	if err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	data, _ := s.client.GetWhitelist(r.Context(), ip)
	writeJSON(w, http.StatusOK, map[string]any{
		"ip":    ip,
		"count": count,
		"raw":   hex.EncodeToString(data),
	})
}

func (s *Server) handleGetWhitelistMode(w http.ResponseWriter, r *http.Request) {
	ip, err := parseIPParam(r)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}
	mode, err := s.client.GetWhitelistMode(r.Context(), ip)
	if err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{"ip": ip, "mode": mode.Mode})
}

func (s *Server) handleSetWhitelistMode(w http.ResponseWriter, r *http.Request) {
	ip, err := parseIPParam(r)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}
	var body struct {
		Mode string `json:"mode"`
	}
	if err := decodeBody(r, &body); err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "mode required (whitelist or graylist)"})
		return
	}
	var opErr error
	switch body.Mode {
	case "whitelist":
		opErr = s.client.SetWhitelistMode(r.Context(), ip)
	case "graylist":
		opErr = s.client.SetGraylistMode(r.Context(), ip)
	default:
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "mode must be 'whitelist' or 'graylist'"})
		return
	}
	if opErr != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": opErr.Error()})
		return
	}
	writeJSON(w, http.StatusOK, map[string]string{"ip": ip, "mode": body.Mode, "status": "applied"})
}

func (s *Server) handleAddToWhitelist(w http.ResponseWriter, r *http.Request) {
	ip, err := parseIPParam(r)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}
	var body struct {
		SN             string `json:"sn"`
		ServiceProfile int    `json:"service_profile"`
	}
	if err := decodeBody(r, &body); err != nil || body.SN == "" {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "sn required, service_profile optional (1-5, default 1)"})
		return
	}
	if body.ServiceProfile < 1 || body.ServiceProfile > 5 {
		body.ServiceProfile = 1
	}
	if err := s.client.AddOnuToWhitelist(r.Context(), ip, body.SN, body.ServiceProfile); err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{
		"ip":              ip,
		"sn":              body.SN,
		"service_profile": body.ServiceProfile,
		"status":          "added",
	})
}

func (s *Server) handleRemoveFromWhitelist(w http.ResponseWriter, r *http.Request) {
	ip, err := parseIPParam(r)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}
	var body struct {
		SN string `json:"sn"`
	}
	if err := decodeBody(r, &body); err != nil || body.SN == "" {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "sn required"})
		return
	}
	if err := s.client.RemoveOnuFromWhitelist(r.Context(), ip, body.SN); err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, map[string]string{"ip": ip, "sn": body.SN, "status": "removed"})
}

// ── Service Profiles ──

func (s *Server) handleGetServiceConfig(w http.ResponseWriter, r *http.Request) {
	ip, err := parseIPParam(r)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}
	data, err := s.client.GetServiceConfig(r.Context(), ip)
	if err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, map[string]string{"ip": ip, "raw": hex.EncodeToString(data)})
}

func (s *Server) handleSetServiceConfig(w http.ResponseWriter, r *http.Request) {
	ip, err := parseIPParam(r)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}
	var body struct {
		Data string `json:"data"`
	}
	if err := decodeBody(r, &body); err != nil || body.Data == "" {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "data required (hex string)"})
		return
	}
	data, hexErr := hex.DecodeString(body.Data)
	if hexErr != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "invalid hex"})
		return
	}
	if err := s.client.SetServiceConfig(r.Context(), ip, data); err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, map[string]string{"ip": ip, "status": "applied"})
}

// ── Alarms ──

func (s *Server) handleGetAlarms(w http.ResponseWriter, r *http.Request) {
	ip, err := parseIPParam(r)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}
	alarms, err := s.client.GetAlarms(r.Context(), ip)
	if err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, alarms)
}

func (s *Server) handleGetOnuAlarms(w http.ResponseWriter, r *http.Request) {
	ip, err := parseIPParam(r)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}
	data, err := s.client.GetOnuAlarms(r.Context(), ip)
	if err != nil {
		writeJSON(w, http.StatusServiceUnavailable, map[string]string{"error": err.Error()})
		return
	}
	writeJSON(w, http.StatusOK, map[string]string{"ip": ip, "raw": hex.EncodeToString(data)})
}

// ── Raw Command ──

func (s *Server) handleCommand(w http.ResponseWriter, r *http.Request) {
	ip, err := parseIPParam(r)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}
	var body struct {
		Cmd  int    `json:"cmd"`
		Data string `json:"data"`
	}
	if err := decodeBody(r, &body); err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "invalid request"})
		return
	}
	if !olt.IsValidCommand(body.Cmd) {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "unknown command code"})
		return
	}
	var data []byte
	if body.Data != "" {
		var hexErr error
		data, hexErr = hex.DecodeString(body.Data)
		if hexErr != nil {
			writeJSON(w, http.StatusBadRequest, map[string]string{"error": "invalid hex data"})
			return
		}
		if len(data) > 19 {
			writeJSON(w, http.StatusBadRequest, map[string]string{"error": "data too long (max 19 bytes)"})
			return
		}
	}
	resp, err := s.client.SendRawCommand(r.Context(), ip, olt.CommandCode(body.Cmd), data)
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

// ── Response helper ──

func writeJSON(w http.ResponseWriter, status int, data any) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	enc := json.NewEncoder(w)
	if err := enc.Encode(data); err != nil {
		slog.Error("json encode error", "err", err)
	}
}
