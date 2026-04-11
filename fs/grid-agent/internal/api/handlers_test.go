package api

import (
	"bytes"
	"encoding/json"
	"io"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/vivek-dodia/grid-agent/internal/config"
	"github.com/vivek-dodia/grid-agent/internal/discovery"
	"github.com/vivek-dodia/grid-agent/internal/olt"
)

func newTestServer(t *testing.T) *Server {
	t.Helper()
	client, err := olt.NewClient(slog.Default())
	if err != nil {
		t.Skipf("cannot bind olt client: %v", err)
	}
	t.Cleanup(func() { _ = client.Close() })
	listener := discovery.NewARPListener(client, 60, slog.Default())
	cfg := &config.Config{
		AuthToken:    "test-token",
		SiteLabel:    "test",
		AgentName:    "grid-agent-test",
		PollInterval: 60,
	}
	return NewServer(cfg, client, listener, slog.Default())
}

func TestAuthMiddleware(t *testing.T) {
	srv := newTestServer(t)
	if srv == nil {
		return
	}

	req := httptest.NewRequest(http.MethodGet, "/", nil)
	rec := httptest.NewRecorder()
	srv.ServeHTTP(rec, req)
	if rec.Code != http.StatusUnauthorized {
		t.Errorf("no token: status = %d, want 401", rec.Code)
	}

	req = httptest.NewRequest(http.MethodGet, "/", nil)
	req.Header.Set("Authorization", "Bearer wrong")
	rec = httptest.NewRecorder()
	srv.ServeHTTP(rec, req)
	if rec.Code != http.StatusUnauthorized {
		t.Errorf("wrong token: status = %d, want 401", rec.Code)
	}

	req = httptest.NewRequest(http.MethodGet, "/", nil)
	req.Header.Set("Authorization", "Bearer test-token")
	rec = httptest.NewRecorder()
	srv.ServeHTTP(rec, req)
	if rec.Code != http.StatusOK {
		t.Errorf("correct token: status = %d, want 200", rec.Code)
	}
}

func TestBodySizeLimit(t *testing.T) {
	srv := newTestServer(t)
	if srv == nil {
		return
	}

	huge := make([]byte, MaxRequestBodyBytes+1024)
	for i := range huge {
		huge[i] = 'a'
	}
	payload := `{"ip":"` + string(huge) + `"}`
	req := httptest.NewRequest(http.MethodPost, "/api/olts/discover", strings.NewReader(payload))
	req.Header.Set("Authorization", "Bearer test-token")
	req.Header.Set("Content-Type", "application/json")
	rec := httptest.NewRecorder()
	srv.ServeHTTP(rec, req)
	if rec.Code == http.StatusOK {
		t.Errorf("oversize body accepted; status = %d", rec.Code)
	}
}

func TestInvalidIPParam(t *testing.T) {
	srv := newTestServer(t)
	if srv == nil {
		return
	}

	req := httptest.NewRequest(http.MethodGet, "/api/olts/not-an-ip/onus", nil)
	req.Header.Set("Authorization", "Bearer test-token")
	rec := httptest.NewRecorder()
	srv.ServeHTTP(rec, req)
	if rec.Code != http.StatusBadRequest {
		t.Errorf("invalid IP: status = %d, want 400", rec.Code)
	}
}

func TestInvalidCommandCode(t *testing.T) {
	srv := newTestServer(t)
	if srv == nil {
		return
	}

	body := map[string]any{"cmd": 999, "data": ""}
	buf, _ := json.Marshal(body)
	req := httptest.NewRequest(http.MethodPost, "/api/olts/127.0.0.1/command", bytes.NewReader(buf))
	req.Header.Set("Authorization", "Bearer test-token")
	req.Header.Set("Content-Type", "application/json")
	rec := httptest.NewRecorder()
	srv.ServeHTTP(rec, req)
	if rec.Code != http.StatusBadRequest {
		t.Errorf("invalid cmd code: status = %d, want 400", rec.Code)
	}
}

func TestCORSPreflight(t *testing.T) {
	srv := newTestServer(t)
	if srv == nil {
		return
	}

	req := httptest.NewRequest(http.MethodOptions, "/api/olts", nil)
	req.Header.Set("Origin", "http://example.com")
	req.Header.Set("Access-Control-Request-Method", "GET")
	rec := httptest.NewRecorder()
	srv.ServeHTTP(rec, req)
	if rec.Code != http.StatusNoContent {
		t.Errorf("preflight: status = %d, want 204", rec.Code)
	}
	if rec.Header().Get("Access-Control-Allow-Origin") != "*" {
		t.Error("CORS header not set")
	}
}

func TestInfoEndpoint(t *testing.T) {
	srv := newTestServer(t)
	if srv == nil {
		return
	}

	req := httptest.NewRequest(http.MethodGet, "/", nil)
	req.Header.Set("Authorization", "Bearer test-token")
	rec := httptest.NewRecorder()
	srv.ServeHTTP(rec, req)
	if rec.Code != http.StatusOK {
		t.Fatalf("info: status = %d", rec.Code)
	}
	body, _ := io.ReadAll(rec.Body)
	var data map[string]any
	if err := json.Unmarshal(body, &data); err != nil {
		t.Fatalf("invalid JSON: %v", err)
	}
	if data["site"] != "test" {
		t.Errorf("site = %v, want 'test'", data["site"])
	}
}
