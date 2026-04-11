package main

import (
	"context"
	"flag"
	"fmt"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"runtime/debug"
	"syscall"
	"time"

	"github.com/vivek-dodia/grid-agent/internal/api"
	"github.com/vivek-dodia/grid-agent/internal/config"
	"github.com/vivek-dodia/grid-agent/internal/discovery"
	"github.com/vivek-dodia/grid-agent/internal/olt"
)

const (
	readHeaderTimeout = 5 * time.Second
	readTimeout       = 10 * time.Second
	writeTimeout      = 30 * time.Second
	idleTimeout       = 60 * time.Second
	shutdownTimeout   = 10 * time.Second

	// Memory limit for Go runtime — RouterOS containers are tight
	// 64 MB is plenty for our workload (the full RSS is typically ~15-25 MB)
	defaultMemLimit = int64(64 << 20)
)

func main() {
	configPath := flag.String("config", "config.json", "path to config file")
	listenAddr := flag.String("listen", "", "override listen address (e.g., :8420)")
	authToken := flag.String("token", "", "override auth token")
	siteName := flag.String("site", "", "override site label")
	logLevel := flag.String("log", "info", "log level: debug, info, warn, error")
	flag.Parse()

	// Set runtime memory limit for tight-memory environments
	debug.SetMemoryLimit(defaultMemLimit)
	// Default to single-threaded operation which is plenty for our workload
	// and reduces memory overhead on RouterOS. Override with GOMAXPROCS env var.
	if os.Getenv("GOMAXPROCS") == "" {
		debug.SetGCPercent(100)
	}

	// Structured logger
	var lvl slog.Level
	switch *logLevel {
	case "debug":
		lvl = slog.LevelDebug
	case "warn":
		lvl = slog.LevelWarn
	case "error":
		lvl = slog.LevelError
	default:
		lvl = slog.LevelInfo
	}
	logger := slog.New(slog.NewTextHandler(os.Stdout, &slog.HandlerOptions{
		Level: lvl,
	}))
	slog.SetDefault(logger)

	cfg, err := config.Load(*configPath)
	if err != nil {
		logger.Error("config load failed", "err", err)
		os.Exit(1)
	}
	if *listenAddr != "" {
		cfg.ListenAddr = *listenAddr
	}
	if *authToken != "" {
		cfg.AuthToken = *authToken
	}
	if *siteName != "" {
		cfg.SiteLabel = *siteName
	}

	logger.Info("grid-agent starting",
		"site", cfg.SiteLabel,
		"listen", cfg.ListenAddr,
		"poll_seconds", cfg.PollInterval,
		"log_level", *logLevel)

	// Initialize OLT client
	client, err := olt.NewClient(logger)
	if err != nil {
		logger.Error("olt client init failed", "err", err)
		os.Exit(1)
	}

	// Start ARP listener / poller
	pollDuration := time.Duration(cfg.PollInterval) * time.Second
	listener := discovery.NewARPListener(client, pollDuration, logger)
	listener.Start()

	// Start HTTP API server with conservative timeouts
	server := api.NewServer(cfg, client, listener, logger)
	httpServer := &http.Server{
		Addr:              cfg.ListenAddr,
		Handler:           server,
		ReadHeaderTimeout: readHeaderTimeout,
		ReadTimeout:       readTimeout,
		WriteTimeout:      writeTimeout,
		IdleTimeout:       idleTimeout,
		MaxHeaderBytes:    8 * 1024, // 8 KB max headers
		ErrorLog:          nil,
	}

	// Graceful shutdown channel
	shutdownCh := make(chan struct{})
	go func() {
		sigCh := make(chan os.Signal, 1)
		signal.Notify(sigCh, syscall.SIGINT, syscall.SIGTERM)
		sig := <-sigCh
		logger.Info("shutdown signal received", "signal", sig.String())

		ctx, cancel := context.WithTimeout(context.Background(), shutdownTimeout)
		defer cancel()

		if err := httpServer.Shutdown(ctx); err != nil {
			logger.Warn("http shutdown error", "err", err)
		}
		listener.Stop()
		_ = client.Close()
		close(shutdownCh)
	}()

	fmt.Printf("\n  grid-agent v0.1.0\n")
	fmt.Printf("  site:   %s\n", cfg.SiteLabel)
	fmt.Printf("  api:    http://0.0.0.0%s\n", cfg.ListenAddr)
	if cfg.AuthToken != "" {
		fmt.Printf("  auth:   Bearer ••••••••\n\n")
	} else {
		fmt.Printf("  auth:   (disabled)\n\n")
	}

	if err := httpServer.ListenAndServe(); err != http.ErrServerClosed {
		logger.Error("http server error", "err", err)
		listener.Stop()
		_ = client.Close()
		os.Exit(1)
	}
	<-shutdownCh
	logger.Info("grid-agent stopped")
}
