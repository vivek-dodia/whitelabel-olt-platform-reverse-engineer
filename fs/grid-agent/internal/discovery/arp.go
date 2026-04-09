package discovery

import (
	"fmt"
	"log"
	"net"
	"strings"
	"time"

	"github.com/vivek-dodia/grid-agent/internal/olt"
)

// ARPListener listens for OLT ARP discovery broadcasts
// OLTs send: ARP Request with sender_ip=<OLT IP>, target_ip=0.0.0.0,
// and "OLT" marker appended after ARP payload
type ARPListener struct {
	client   *olt.Client
	pollIvl  time.Duration
	stopCh   chan struct{}
}

func NewARPListener(client *olt.Client, pollInterval time.Duration) *ARPListener {
	return &ARPListener{
		client:  client,
		pollIvl: pollInterval,
		stopCh:  make(chan struct{}),
	}
}

// StartPolling continuously polls known OLTs for status
func (a *ARPListener) StartPolling() {
	go func() {
		ticker := time.NewTicker(a.pollIvl)
		defer ticker.Stop()
		for {
			select {
			case <-ticker.C:
				a.pollAll()
			case <-a.stopCh:
				return
			}
		}
	}()
}

func (a *ARPListener) pollAll() {
	olts := a.client.GetAllOlts()
	for _, o := range olts {
		status, err := a.client.ShakeHand(o.IP)
		if err != nil {
			a.client.SetOltOffline(o.IP)
			log.Printf("OLT %s offline: %v", o.IP, err)
			continue
		}

		// Auto-auth after successful shake_hand
		a.client.EnableWrite(o.IP)
		a.client.EnableRead(o.IP)

		log.Printf("OLT %s online: SN=%s", status.IP, status.Serial)
	}
}

// DiscoverOlt manually adds an OLT by IP and attempts handshake
func (a *ARPListener) DiscoverOlt(ip string) (*olt.OltStatus, error) {
	if net.ParseIP(ip) == nil {
		return nil, fmt.Errorf("invalid IP: %s", ip)
	}

	status, err := a.client.ShakeHand(ip)
	if err != nil {
		return nil, fmt.Errorf("OLT at %s not responding: %w", ip, err)
	}

	// Auto-auth
	w, _ := a.client.EnableWrite(ip)
	r, _ := a.client.EnableRead(ip)
	status.WriteAuth = w
	status.ReadAuth = r

	return status, nil
}

// ScanSubnet scans a /24 subnet for OLTs by sending shake_hand to each IP
func (a *ARPListener) ScanSubnet(subnet string) []*olt.OltStatus {
	// Parse base like "100.64.2"
	parts := strings.Split(subnet, ".")
	if len(parts) < 3 {
		return nil
	}
	base := strings.Join(parts[:3], ".")

	var found []*olt.OltStatus
	for i := 1; i < 255; i++ {
		ip := fmt.Sprintf("%s.%d", base, i)
		status, err := a.client.ShakeHand(ip)
		if err == nil && status != nil {
			log.Printf("Discovered OLT at %s: SN=%s", ip, status.Serial)
			found = append(found, status)
		}
	}
	return found
}

func (a *ARPListener) Stop() {
	close(a.stopCh)
}
