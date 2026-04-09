package main

import (
	"flag"
	"fmt"
	"log"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"github.com/vivek-dodia/grid-agent/internal/api"
	"github.com/vivek-dodia/grid-agent/internal/config"
	"github.com/vivek-dodia/grid-agent/internal/discovery"
	"github.com/vivek-dodia/grid-agent/internal/olt"
)

func main() {
	configPath := flag.String("config", "config.json", "path to config file")
	listenAddr := flag.String("listen", "", "override listen address (e.g., :8420)")
	authToken := flag.String("token", "", "override auth token")
	siteName := flag.String("site", "", "override site label")
	flag.Parse()

	cfg, err := config.Load(*configPath)
	if err != nil {
		log.Fatalf("Failed to load config: %v", err)
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

	log.Printf("GRID Agent starting")
	log.Printf("  Site:   %s", cfg.SiteLabel)
	log.Printf("  Listen: %s", cfg.ListenAddr)
	log.Printf("  Poll:   %ds", cfg.PollInterval)

	// Initialize OLT UDP client
	client, err := olt.NewClient()
	if err != nil {
		log.Fatalf("Failed to create OLT client: %v", err)
	}
	defer client.Close()

	// Start ARP listener / poller
	pollDuration := time.Duration(cfg.PollInterval) * time.Second
	listener := discovery.NewARPListener(client, pollDuration)
	listener.StartPolling()
	defer listener.Stop()

	// Start HTTP API server
	server := api.NewServer(cfg, client, listener)
	httpServer := &http.Server{
		Addr:    cfg.ListenAddr,
		Handler: server,
	}

	// Graceful shutdown
	go func() {
		sigCh := make(chan os.Signal, 1)
		signal.Notify(sigCh, syscall.SIGINT, syscall.SIGTERM)
		<-sigCh
		log.Println("Shutting down...")
		httpServer.Close()
	}()

	fmt.Printf("\n  GRID Agent v0.1.0\n")
	fmt.Printf("  Site:   %s\n", cfg.SiteLabel)
	fmt.Printf("  API:    http://0.0.0.0%s\n", cfg.ListenAddr)
	fmt.Printf("  Auth:   Bearer %s\n\n", cfg.AuthToken)

	if err := httpServer.ListenAndServe(); err != http.ErrServerClosed {
		log.Fatalf("HTTP server error: %v", err)
	}
}
