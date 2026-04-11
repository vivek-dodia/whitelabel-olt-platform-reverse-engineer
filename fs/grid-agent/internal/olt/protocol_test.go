package olt

import (
	"context"
	"log/slog"
	"net"
	"sync"
	"sync/atomic"
	"testing"
	"time"
)

// fakeOlt is a minimal test responder that listens on RxPort and echoes
// requests back with the expected FS response shape (cmd + seq + padding).
type fakeOlt struct {
	t    *testing.T
	conn *net.UDPConn
	done chan struct{}
	wg   sync.WaitGroup
	// If responderFn returns nil the request is ignored (simulate timeout)
	responderFn func(cmd byte, seq uint16, data []byte) []byte
	received    atomic.Int64
}

func newFakeOlt(t *testing.T, ip string, respond func(cmd byte, seq uint16, data []byte) []byte) *fakeOlt {
	t.Helper()
	addr := &net.UDPAddr{IP: net.ParseIP(ip), Port: TxPort}
	conn, err := net.ListenUDP("udp4", addr)
	if err != nil {
		t.Skipf("cannot bind fake OLT on %s:%d: %v", ip, TxPort, err)
		return nil
	}
	f := &fakeOlt{
		t:           t,
		conn:        conn,
		done:        make(chan struct{}),
		responderFn: respond,
	}
	f.wg.Add(1)
	go f.serve()
	return f
}

func (f *fakeOlt) serve() {
	defer f.wg.Done()
	buf := make([]byte, 1024)
	for {
		select {
		case <-f.done:
			return
		default:
		}
		_ = f.conn.SetReadDeadline(time.Now().Add(100 * time.Millisecond))
		n, src, err := f.conn.ReadFromUDP(buf)
		if err != nil {
			continue
		}
		if n < 3 {
			continue
		}
		f.received.Add(1)
		cmd := buf[0]
		seq := uint16(buf[1])<<8 | uint16(buf[2])
		data := make([]byte, n-3)
		copy(data, buf[3:n])

		resp := f.responderFn(cmd, seq, data)
		if resp == nil {
			continue
		}
		// Send response back on RxPort of the requester
		dst := &net.UDPAddr{IP: src.IP, Port: RxPort}
		_, _ = f.conn.WriteToUDP(resp, dst)
	}
}

func (f *fakeOlt) stop() {
	close(f.done)
	_ = f.conn.Close()
	f.wg.Wait()
}

// buildResponse creates a valid shake_hand-style response
func buildShakeHandResponse(seq uint16) []byte {
	resp := make([]byte, 41)
	resp[0] = byte(CmdShakeHand)
	resp[1] = byte(seq >> 8)
	resp[2] = byte(seq & 0xff)
	// bytes 4-9: fake MAC
	resp[4] = 0x00
	resp[5] = 0x50
	resp[6] = 0x00
	resp[7] = 0x20
	resp[8] = 0xee
	resp[9] = 0x64
	// bytes 20+: fake serial
	serial := "TESTSERIAL1234  "
	copy(resp[20:], []byte(serial))
	return resp
}

func TestBuildPacket(t *testing.T) {
	pkt := buildPacket(CmdShakeHand, 0x0123, nil)
	if len(pkt) != 22 {
		t.Fatalf("expected 22 bytes, got %d", len(pkt))
	}
	if pkt[0] != byte(CmdShakeHand) {
		t.Errorf("cmd byte = 0x%x, want 0x%x", pkt[0], CmdShakeHand)
	}
	if pkt[1] != 0x01 || pkt[2] != 0x23 {
		t.Errorf("seq bytes = 0x%x 0x%x, want 0x01 0x23", pkt[1], pkt[2])
	}
}

func TestBuildPacketDataTruncation(t *testing.T) {
	// Data longer than 19 bytes should be truncated, not panic
	big := make([]byte, 500)
	for i := range big {
		big[i] = 0xAB
	}
	pkt := buildPacket(CmdPasswordCmd, 1, big)
	if len(pkt) != 22 {
		t.Fatalf("expected 22 bytes, got %d", len(pkt))
	}
	for i := 3; i < 22; i++ {
		if pkt[i] != 0xAB {
			t.Errorf("pkt[%d] = 0x%x, want 0xAB", i, pkt[i])
		}
	}
}

func TestIsValidCommand(t *testing.T) {
	valid := []int{1, 2, 3, 8, 12, 66, 71}
	for _, v := range valid {
		if !IsValidCommand(v) {
			t.Errorf("IsValidCommand(%d) = false, want true", v)
		}
	}
	invalid := []int{-1, 0, 14, 99, 200, 256, 1000}
	for _, v := range invalid {
		if IsValidCommand(v) {
			t.Errorf("IsValidCommand(%d) = true, want false", v)
		}
	}
}

func TestValidateOnuSN(t *testing.T) {
	ok := []string{"TPLGD092299A", "ABCD1234", "WSTG12341207"}
	for _, s := range ok {
		if err := validateOnuSN(s); err != nil {
			t.Errorf("validateOnuSN(%q) = %v, want nil", s, err)
		}
	}
	bad := []string{"", "this_is_way_too_long_to_be_a_serial", "WITH\x00NUL", "WITH SPACE IS OK NO WAIT ITS FINE"}
	_ = bad
	if err := validateOnuSN(""); err == nil {
		t.Error("empty SN should error")
	}
	if err := validateOnuSN("way-too-long-serial-number-here"); err == nil {
		t.Error("overlong SN should error")
	}
	if err := validateOnuSN("WITH\x00NUL"); err == nil {
		t.Error("SN with null byte should error")
	}
}

func TestSendAndWaitTimeout(t *testing.T) {
	if testing.Short() {
		t.Skip("short mode")
	}
	// Create client that talks to an IP where no OLT is listening
	c, err := NewClient(slog.Default())
	if err != nil {
		t.Skipf("cannot bind client: %v", err)
	}
	defer c.Close()

	ctx, cancel := context.WithTimeout(context.Background(), 2*time.Second)
	defer cancel()
	_, err = c.SendAndWait(ctx, "127.0.0.99", CmdShakeHand, nil, 500*time.Millisecond)
	if err == nil {
		t.Fatal("expected timeout error")
	}
	// Pending map should be clean after timeout
	if n := c.PendingCount(); n != 0 {
		t.Errorf("pending leaked: %d entries remain", n)
	}
}

func TestSendAndWaitContextCancel(t *testing.T) {
	c, err := NewClient(slog.Default())
	if err != nil {
		t.Skipf("cannot bind client: %v", err)
	}
	defer c.Close()

	ctx, cancel := context.WithCancel(context.Background())
	// Cancel immediately
	cancel()
	_, err = c.SendAndWait(ctx, "127.0.0.99", CmdShakeHand, nil, 5*time.Second)
	if err == nil {
		t.Fatal("expected context cancelled error")
	}
	if c.PendingCount() != 0 {
		t.Errorf("pending leaked after ctx cancel: %d", c.PendingCount())
	}
}

func TestClientCloseUnblocksSenders(t *testing.T) {
	c, err := NewClient(slog.Default())
	if err != nil {
		t.Skipf("cannot bind client: %v", err)
	}

	errCh := make(chan error, 1)
	go func() {
		_, e := c.SendAndWait(context.Background(), "127.0.0.99", CmdShakeHand, nil, 10*time.Second)
		errCh <- e
	}()

	// Give the goroutine time to register its pending entry
	time.Sleep(100 * time.Millisecond)
	_ = c.Close()

	select {
	case <-errCh:
		// goroutine returned
	case <-time.After(3 * time.Second):
		t.Fatal("sender goroutine did not unblock after Close")
	}
}

func TestConcurrentSends(t *testing.T) {
	if testing.Short() {
		t.Skip("short mode")
	}
	c, err := NewClient(slog.Default())
	if err != nil {
		t.Skipf("cannot bind client: %v", err)
	}
	defer c.Close()

	// Fire 100 concurrent requests to a non-existent OLT; they should all
	// time out without corrupting state
	var wg sync.WaitGroup
	for i := 0; i < 100; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			ctx, cancel := context.WithTimeout(context.Background(), 2*time.Second)
			defer cancel()
			_, _ = c.SendAndWait(ctx, "127.0.0.99", CmdShakeHand, nil, 500*time.Millisecond)
		}()
	}
	wg.Wait()

	// Wait a bit for janitor to catch any stragglers
	time.Sleep(500 * time.Millisecond)
	if n := c.PendingCount(); n != 0 {
		t.Errorf("pending leaked under concurrent load: %d entries remain", n)
	}
}

func TestOltCacheConcurrentAccess(t *testing.T) {
	c, err := NewClient(slog.Default())
	if err != nil {
		t.Skipf("cannot bind client: %v", err)
	}
	defer c.Close()

	// Populate some fake OLTs through the exported methods that don't require UDP
	for i := 0; i < 10; i++ {
		ip := net.IPv4(10, 0, 0, byte(i+1)).String()
		c.oltsMu.Lock()
		c.olts[ip] = &OltStatus{IP: ip, Status: "online"}
		c.oltsMu.Unlock()
	}

	var wg sync.WaitGroup
	for i := 0; i < 50; i++ {
		wg.Add(1)
		go func(i int) {
			defer wg.Done()
			ip := net.IPv4(10, 0, 0, byte((i%10)+1)).String()
			switch i % 4 {
			case 0:
				_ = c.GetOlt(ip)
			case 1:
				_ = c.GetAllOlts()
			case 2:
				c.SetOltOffline(ip)
			case 3:
				_ = c.PendingCount()
			}
		}(i)
	}
	wg.Wait()
}

func TestTooManyPending(t *testing.T) {
	c, err := NewClient(slog.Default())
	if err != nil {
		t.Skipf("cannot bind client: %v", err)
	}
	defer c.Close()

	// Directly fill the pending map to test the cap
	c.pendingMu.Lock()
	for i := 0; i < MaxPendingRequests; i++ {
		key := "127.0.0.1:" + string(rune('A'+i%26)) + string(rune('A'+(i/26)%26))
		c.pending[key] = &pendingRequest{ch: make(chan []byte, 1), created: time.Now()}
	}
	c.pendingMu.Unlock()

	ctx, cancel := context.WithTimeout(context.Background(), 1*time.Second)
	defer cancel()
	_, err = c.SendAndWait(ctx, "127.0.0.99", CmdShakeHand, nil, 100*time.Millisecond)
	if err != ErrTooManyPending {
		t.Errorf("expected ErrTooManyPending, got %v", err)
	}
}

// Integration test: fake OLT responds to requests
func TestLoopbackShakeHand(t *testing.T) {
	if testing.Short() {
		t.Skip("short mode")
	}
	fake := newFakeOlt(t, "127.0.0.1", func(cmd byte, seq uint16, _ []byte) []byte {
		if cmd == byte(CmdShakeHand) {
			return buildShakeHandResponse(seq)
		}
		return nil
	})
	if fake == nil {
		return
	}
	defer fake.stop()

	c, err := NewClient(slog.Default())
	if err != nil {
		t.Skipf("client init failed: %v", err)
	}
	defer c.Close()

	// Small pause so the fake is ready
	time.Sleep(50 * time.Millisecond)

	ctx, cancel := context.WithTimeout(context.Background(), 2*time.Second)
	defer cancel()
	status, err := c.ShakeHand(ctx, "127.0.0.1")
	if err != nil {
		t.Fatalf("ShakeHand failed: %v", err)
	}
	if status.Serial != "TESTSERIAL1234" {
		t.Errorf("serial = %q, want TESTSERIAL1234", status.Serial)
	}
	if fake.received.Load() == 0 {
		t.Error("fake OLT did not receive any requests")
	}
}
