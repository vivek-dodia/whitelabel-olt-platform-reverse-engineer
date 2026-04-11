package discovery

import (
	"context"
	"fmt"
	"log/slog"
	"net"
	"strings"
	"sync"
	"time"

	"github.com/vivek-dodia/grid-agent/internal/olt"
)

// Scan/discovery tuning for RouterOS container
const (
	ScanWorkers        = 16
	ScanPerHostTimeout = 500 * time.Millisecond
	ScanMax            = 254
)

type ARPListener struct {
	client   *olt.Client
	pollIvl  time.Duration
	doneCh   chan struct{}
	closeOnce sync.Once
	wg       sync.WaitGroup
	log      *slog.Logger

	// Track consecutive poll failures per OLT to rate-limit offline logs
	failMu sync.Mutex
	fails  map[string]int
}

func NewARPListener(client *olt.Client, pollInterval time.Duration, log *slog.Logger) *ARPListener {
	if log == nil {
		log = slog.Default()
	}
	if pollInterval < 1*time.Second {
		pollInterval = 5 * time.Second
	}
	return &ARPListener{
		client:  client,
		pollIvl: pollInterval,
		doneCh:  make(chan struct{}),
		log:     log,
		fails:   make(map[string]int),
	}
}

func (a *ARPListener) Start() {
	a.wg.Add(1)
	go a.pollLoop()
}

func (a *ARPListener) Stop() {
	a.closeOnce.Do(func() { close(a.doneCh) })
	a.wg.Wait()
}

func (a *ARPListener) pollLoop() {
	defer a.wg.Done()
	// Immediate first poll so freshly-added OLTs get status quickly
	a.pollAll()
	t := time.NewTicker(a.pollIvl)
	defer t.Stop()
	for {
		select {
		case <-a.doneCh:
			return
		case <-t.C:
			a.pollAll()
		}
	}
}

func (a *ARPListener) pollAll() {
	olts := a.client.GetAllOlts()
	if len(olts) == 0 {
		return
	}

	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	for _, o := range olts {
		select {
		case <-a.doneCh:
			return
		default:
		}
		a.pollOne(ctx, o.IP)
	}
}

func (a *ARPListener) pollOne(ctx context.Context, ip string) {
	status, err := a.client.ShakeHand(ctx, ip)
	if err != nil {
		a.client.SetOltOffline(ip)
		a.failMu.Lock()
		a.fails[ip]++
		count := a.fails[ip]
		a.failMu.Unlock()
		// Only log on first failure and every 12th (~1/minute at 5s interval)
		if count == 1 || count%12 == 0 {
			a.log.Warn("olt offline", "ip", ip, "fails", count, "err", err)
		}
		return
	}
	// Reset failure counter on success
	a.failMu.Lock()
	if a.fails[ip] > 0 {
		a.log.Info("olt back online", "ip", ip, "serial", status.Serial)
		delete(a.fails, ip)
	}
	a.failMu.Unlock()

	// Auto-auth best-effort — don't block or error out the poll
	_, _ = a.client.EnableWrite(ctx, ip)
	_, _ = a.client.EnableRead(ctx, ip)
}

// DiscoverOlt adds an OLT by IP, runs shake_hand, and attempts auth.
// Uses the caller's context for cancellation.
func (a *ARPListener) DiscoverOlt(ctx context.Context, ip string) (*olt.OltStatus, error) {
	if parsed := net.ParseIP(ip); parsed == nil || parsed.To4() == nil {
		return nil, fmt.Errorf("invalid ipv4 address: %s", ip)
	}

	status, err := a.client.ShakeHand(ctx, ip)
	if err != nil {
		return nil, fmt.Errorf("olt %s not responding: %w", ip, err)
	}

	w, _ := a.client.EnableWrite(ctx, ip)
	r, _ := a.client.EnableRead(ctx, ip)
	status.WriteAuth = w
	status.ReadAuth = r
	return status, nil
}

// ScanSubnet parallelizes shake_hand across a /24 with a bounded worker pool.
// Honors ctx cancellation and returns results as a slice.
func (a *ARPListener) ScanSubnet(ctx context.Context, subnet string) ([]*olt.OltStatus, error) {
	base, err := validateSubnetBase(subnet)
	if err != nil {
		return nil, err
	}

	scanCtx, cancel := context.WithTimeout(ctx, 60*time.Second)
	defer cancel()

	type result struct {
		status *olt.OltStatus
	}

	ipCh := make(chan string, ScanWorkers)
	resCh := make(chan result, ScanMax)

	var wg sync.WaitGroup
	for i := 0; i < ScanWorkers; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			for ip := range ipCh {
				// Per-host timeout
				hctx, hcancel := context.WithTimeout(scanCtx, ScanPerHostTimeout)
				status, err := a.client.ShakeHand(hctx, ip)
				hcancel()
				if err == nil && status != nil {
					resCh <- result{status: status}
				}
			}
		}()
	}

	go func() {
		defer close(ipCh)
		for i := 1; i <= ScanMax; i++ {
			select {
			case <-scanCtx.Done():
				return
			case ipCh <- fmt.Sprintf("%s.%d", base, i):
			}
		}
	}()

	go func() {
		wg.Wait()
		close(resCh)
	}()

	var found []*olt.OltStatus
	for r := range resCh {
		found = append(found, r.status)
		a.log.Info("discovered olt via scan", "ip", r.status.IP, "serial", r.status.Serial)
	}
	if found == nil {
		found = []*olt.OltStatus{}
	}
	return found, nil
}

func validateSubnetBase(s string) (string, error) {
	parts := strings.Split(s, ".")
	if len(parts) < 3 {
		return "", fmt.Errorf("subnet must be in form a.b.c (got %q)", s)
	}
	// Reject full IPs — only /24 base allowed
	if len(parts) > 3 {
		parts = parts[:3]
	}
	// Validate each octet as 0-255 integer
	for _, p := range parts {
		if len(p) == 0 || len(p) > 3 {
			return "", fmt.Errorf("invalid octet %q", p)
		}
		for _, r := range p {
			if r < '0' || r > '9' {
				return "", fmt.Errorf("invalid octet %q", p)
			}
		}
		n := 0
		for _, r := range p {
			n = n*10 + int(r-'0')
		}
		if n > 255 {
			return "", fmt.Errorf("octet out of range %q", p)
		}
	}
	return strings.Join(parts, "."), nil
}
