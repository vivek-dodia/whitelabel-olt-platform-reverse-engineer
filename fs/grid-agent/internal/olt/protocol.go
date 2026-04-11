package olt

import (
	"context"
	"crypto/rand"
	"encoding/binary"
	"encoding/hex"
	"errors"
	"fmt"
	"log/slog"
	"net"
	"strings"
	"sync"
	"sync/atomic"
	"time"
)

const (
	TxPort = 64219
	RxPort = 64218

	// Resource limits — tuned for RouterOS container deployment
	MaxPendingRequests = 512      // Hard cap on concurrent in-flight requests
	MaxTrackedOlts     = 256      // Hard cap on known OLTs
	UDPReadBufSize     = 2048     // Per-receive buffer; packets are ≤ 300B
	MaxSerialLen       = 32       // OLT serial strings are ≤ 16 chars
	MaxOnuSnLen        = 16       // ONU serial numbers

	DefaultTimeout = 3 * time.Second
	MaxTimeout     = 30 * time.Second
)

type CommandCode byte

const (
	CmdShakeHand        CommandCode = 1
	CmdIpConfiguration  CommandCode = 2
	CmdWhiteListSend    CommandCode = 3
	CmdWhiteListReadQty CommandCode = 4
	CmdOnuWlistRpt      CommandCode = 5
	CmdIllegalCpeReport CommandCode = 6
	CmdCpeAlarmReport   CommandCode = 7
	CmdOltAlarmReport   CommandCode = 8
	CmdServiceTypeSend  CommandCode = 9
	CmdWhiteListDel     CommandCode = 10
	CmdCpeOptParaReport CommandCode = 11
	CmdCpeSnStatus      CommandCode = 12
	CmdServiceConfigRpt CommandCode = 13
	CmdPasswordCmd      CommandCode = 66
	CmdPasswordCheckCmd CommandCode = 67
	CmdOltUpdateBin     CommandCode = 68
	CmdOltResetMaster   CommandCode = 69
	CmdOltResetSlave    CommandCode = 70
	CmdOltSoftreset     CommandCode = 71
)

// IsValidCommand reports whether cmd is a known command code
func IsValidCommand(cmd int) bool {
	if cmd < 0 || cmd > 255 {
		return false
	}
	switch CommandCode(cmd) {
	case CmdShakeHand, CmdIpConfiguration, CmdWhiteListSend, CmdWhiteListReadQty,
		CmdOnuWlistRpt, CmdIllegalCpeReport, CmdCpeAlarmReport, CmdOltAlarmReport,
		CmdServiceTypeSend, CmdWhiteListDel, CmdCpeOptParaReport, CmdCpeSnStatus,
		CmdServiceConfigRpt, CmdPasswordCmd, CmdPasswordCheckCmd, CmdOltUpdateBin,
		CmdOltResetMaster, CmdOltResetSlave, CmdOltSoftreset:
		return true
	}
	return false
}

var (
	writeAuthKey, _ = hex.DecodeString("5774b87337454200d4d33f80c4663dc5e5")
	readAuthKey, _  = hex.DecodeString("5274b87337454200d4d33f80c4663dc5e5")

	whitelistModeEnable = []byte{0x57, 0x01}
	graylistModeEnable  = []byte{0x47, 0x01}

	ErrClientClosed    = errors.New("olt client closed")
	ErrTooManyPending  = errors.New("too many pending requests")
	ErrTooManyOlts     = errors.New("olt tracking limit reached")
	ErrInvalidResponse = errors.New("invalid response")
	ErrInvalidIP       = errors.New("invalid ip address")
)

type OltStatus struct {
	IP        string `json:"ip"`
	MAC       string `json:"mac"`
	Serial    string `json:"serial"`
	Status    string `json:"status"`
	LastSeen  int64  `json:"last_seen"`
	ReadAuth  bool   `json:"read_auth"`
	WriteAuth bool   `json:"write_auth"`
}

type OnuInfo struct {
	SN          string  `json:"sn"`
	Status      int     `json:"status"`
	TxPwr       float64 `json:"tx_pwr,omitempty"`
	RxPwr       float64 `json:"rx_pwr,omitempty"`
	Bias        float64 `json:"bias,omitempty"`
	Temperature float64 `json:"temperature,omitempty"`
	Voltage     float64 `json:"voltage,omitempty"`
}

type AlarmInfo struct {
	Raw      string `json:"raw"`
	HasAlarm bool   `json:"has_alarm"`
}

type WhitelistMode struct {
	Mode string `json:"mode"`
}

type pendingRequest struct {
	ch      chan []byte
	created time.Time
}

type Client struct {
	rxConn *net.UDPConn
	txConn *net.UDPConn

	seq atomic.Uint32 // using uint32 to avoid conflict; wraps to uint16 on use

	pendingMu sync.Mutex
	pending   map[string]*pendingRequest

	oltsMu sync.RWMutex
	olts   map[string]*OltStatus

	closedFlag atomic.Bool
	closeOnce  sync.Once
	doneCh     chan struct{}
	wg         sync.WaitGroup

	log *slog.Logger
}

// NewClient binds the UDP sockets and starts the receive loop
func NewClient(log *slog.Logger) (*Client, error) {
	if log == nil {
		log = slog.Default()
	}

	rxAddr := &net.UDPAddr{IP: net.IPv4zero, Port: RxPort}
	rxConn, err := net.ListenUDP("udp4", rxAddr)
	if err != nil {
		return nil, fmt.Errorf("bind rx :%d: %w", RxPort, err)
	}
	// Limit socket kernel buffer to cap memory consumption
	_ = rxConn.SetReadBuffer(64 * 1024)

	txAddr := &net.UDPAddr{IP: net.IPv4zero, Port: TxPort}
	txConn, err := net.ListenUDP("udp4", txAddr)
	if err != nil {
		_ = rxConn.Close()
		return nil, fmt.Errorf("bind tx :%d: %w", TxPort, err)
	}
	_ = txConn.SetWriteBuffer(64 * 1024)

	// Seed sequence from a random value so restarts don't collide
	var seed [2]byte
	_, _ = rand.Read(seed[:])
	c := &Client{
		rxConn:  rxConn,
		txConn:  txConn,
		pending: make(map[string]*pendingRequest),
		olts:    make(map[string]*OltStatus),
		doneCh:  make(chan struct{}),
		log:     log,
	}
	c.seq.Store(uint32(binary.BigEndian.Uint16(seed[:])))

	c.wg.Add(2)
	go c.receiveLoop()
	go c.janitorLoop()

	return c, nil
}

// Close stops the receive loop and releases all sockets. Safe to call multiple times.
func (c *Client) Close() error {
	c.closeOnce.Do(func() {
		c.closedFlag.Store(true)
		close(c.doneCh)
		_ = c.rxConn.SetReadDeadline(time.Now())
		_ = c.rxConn.Close()
		_ = c.txConn.Close()

		// Unblock any pending waiters
		c.pendingMu.Lock()
		for k, p := range c.pending {
			close(p.ch)
			delete(c.pending, k)
		}
		c.pendingMu.Unlock()

		c.wg.Wait()
	})
	return nil
}

func (c *Client) closed() bool { return c.closedFlag.Load() }

// receiveLoop dispatches incoming UDP packets to pending request channels.
func (c *Client) receiveLoop() {
	defer c.wg.Done()
	buf := make([]byte, UDPReadBufSize)
	for {
		if c.closed() {
			return
		}
		// Set a read deadline so we can periodically check closed flag
		_ = c.rxConn.SetReadDeadline(time.Now().Add(500 * time.Millisecond))
		n, addr, err := c.rxConn.ReadFromUDP(buf)
		if err != nil {
			if c.closed() {
				return
			}
			var ne net.Error
			if errors.As(err, &ne) && ne.Timeout() {
				continue
			}
			c.log.Warn("udp receive error", "err", err)
			continue
		}
		if n < 3 {
			continue
		}

		// Copy into a fresh slice so the shared buf can be reused
		data := make([]byte, n)
		copy(data, buf[:n])

		seq := uint16(data[1])<<8 | uint16(data[2])
		key := fmt.Sprintf("%s:%d", addr.IP.String(), seq)

		c.pendingMu.Lock()
		p, ok := c.pending[key]
		if ok {
			delete(c.pending, key)
		}
		c.pendingMu.Unlock()

		if ok {
			select {
			case p.ch <- data:
			default: // receiver gave up; drop
			}
		}
	}
}

// janitorLoop evicts pending entries older than 2× the max timeout, in case
// a sender leaks (e.g., context cancelled before timeout expired).
func (c *Client) janitorLoop() {
	defer c.wg.Done()
	t := time.NewTicker(5 * time.Second)
	defer t.Stop()
	for {
		select {
		case <-c.doneCh:
			return
		case <-t.C:
			cutoff := time.Now().Add(-2 * MaxTimeout)
			c.pendingMu.Lock()
			for k, p := range c.pending {
				if p.created.Before(cutoff) {
					delete(c.pending, k)
					close(p.ch)
				}
			}
			c.pendingMu.Unlock()
		}
	}
}

func (c *Client) nextSeq() uint16 {
	return uint16(c.seq.Add(1) & 0xFFFF)
}

func buildPacket(cmd CommandCode, seq uint16, data []byte) []byte {
	pkt := make([]byte, 22)
	pkt[0] = byte(cmd)
	pkt[1] = byte(seq >> 8)
	pkt[2] = byte(seq & 0xff)
	if len(data) > 0 {
		n := len(data)
		if n > 19 {
			n = 19
		}
		copy(pkt[3:], data[:n])
	}
	return pkt
}

// SendAndWait sends a command to an OLT and waits for its response.
// Honors context cancellation and enforces timeout bounds.
func (c *Client) SendAndWait(ctx context.Context, oltIP string, cmd CommandCode, data []byte, timeout time.Duration) ([]byte, error) {
	if c.closed() {
		return nil, ErrClientClosed
	}
	if net.ParseIP(oltIP) == nil {
		return nil, ErrInvalidIP
	}
	if timeout <= 0 {
		timeout = DefaultTimeout
	}
	if timeout > MaxTimeout {
		timeout = MaxTimeout
	}

	// Reserve pending slot with cap enforcement
	seq := c.nextSeq()
	key := fmt.Sprintf("%s:%d", oltIP, seq)

	c.pendingMu.Lock()
	if len(c.pending) >= MaxPendingRequests {
		c.pendingMu.Unlock()
		return nil, ErrTooManyPending
	}
	ch := make(chan []byte, 1)
	c.pending[key] = &pendingRequest{ch: ch, created: time.Now()}
	c.pendingMu.Unlock()

	// Always clean up pending entry if we leave without a response
	cleanup := func() {
		c.pendingMu.Lock()
		delete(c.pending, key)
		c.pendingMu.Unlock()
	}

	pkt := buildPacket(cmd, seq, data)
	dst := &net.UDPAddr{IP: net.ParseIP(oltIP).To4(), Port: TxPort}
	_ = c.txConn.SetWriteDeadline(time.Now().Add(500 * time.Millisecond))
	if _, err := c.txConn.WriteToUDP(pkt, dst); err != nil {
		cleanup()
		return nil, fmt.Errorf("send to %s: %w", oltIP, err)
	}

	timer := time.NewTimer(timeout)
	defer timer.Stop()

	select {
	case resp, ok := <-ch:
		if !ok {
			return nil, ErrClientClosed
		}
		return resp, nil
	case <-timer.C:
		cleanup()
		return nil, fmt.Errorf("timeout waiting for %s (cmd=%d)", oltIP, cmd)
	case <-ctx.Done():
		cleanup()
		return nil, ctx.Err()
	case <-c.doneCh:
		cleanup()
		return nil, ErrClientClosed
	}
}

// ── Discovery & Status ──

func (c *Client) ShakeHand(ctx context.Context, oltIP string) (*OltStatus, error) {
	resp, err := c.SendAndWait(ctx, oltIP, CmdShakeHand, nil, DefaultTimeout)
	if err != nil {
		return nil, err
	}
	if len(resp) < 20 {
		return nil, fmt.Errorf("%w: shake_hand too short (%d bytes)", ErrInvalidResponse, len(resp))
	}

	mac := fmt.Sprintf("00:00:%02x:%02x:%02x:%02x:%02x:%02x",
		resp[4], resp[5], resp[6], resp[7], resp[8], resp[9])

	rawSerial := resp[20:]
	if len(rawSerial) > MaxSerialLen {
		rawSerial = rawSerial[:MaxSerialLen]
	}
	serial := strings.Trim(string(rawSerial), "\x00 ")

	status := &OltStatus{
		IP:       oltIP,
		MAC:      mac,
		Serial:   serial,
		Status:   "online",
		LastSeen: time.Now().UnixMilli(),
	}

	c.oltsMu.Lock()
	defer c.oltsMu.Unlock()
	existing, ok := c.olts[oltIP]
	if !ok {
		if len(c.olts) >= MaxTrackedOlts {
			return nil, ErrTooManyOlts
		}
		c.olts[oltIP] = status
		return status, nil
	}
	existing.MAC = status.MAC
	existing.Serial = status.Serial
	existing.Status = "online"
	existing.LastSeen = status.LastSeen
	return existing, nil
}

// ── Auth ──

func (c *Client) EnableWrite(ctx context.Context, oltIP string) (bool, error) {
	resp, err := c.SendAndWait(ctx, oltIP, CmdPasswordCmd, writeAuthKey, DefaultTimeout)
	if err != nil {
		return false, err
	}
	granted := len(resp) > 3 && resp[3] == 0x77
	c.oltsMu.Lock()
	if s, ok := c.olts[oltIP]; ok {
		s.WriteAuth = granted
	}
	c.oltsMu.Unlock()
	return granted, nil
}

func (c *Client) EnableRead(ctx context.Context, oltIP string) (bool, error) {
	resp, err := c.SendAndWait(ctx, oltIP, CmdPasswordCmd, readAuthKey, DefaultTimeout)
	if err != nil {
		return false, err
	}
	granted := len(resp) > 3 && resp[3] == 0x72
	c.oltsMu.Lock()
	if s, ok := c.olts[oltIP]; ok {
		s.ReadAuth = granted
	}
	c.oltsMu.Unlock()
	return granted, nil
}

// ── ONU Management ──

func (c *Client) GetOnuStatus(ctx context.Context, oltIP string) ([]OnuInfo, error) {
	resp, err := c.SendAndWait(ctx, oltIP, CmdCpeSnStatus, nil, DefaultTimeout)
	if err != nil {
		return nil, err
	}
	onus := []OnuInfo{}
	if len(resp) > 3 {
		data := resp[3:]
		if len(data) >= 11 {
			sn := strings.Trim(string(data[:10]), "\x00 ")
			if sn != "" {
				onus = append(onus, OnuInfo{SN: sn, Status: int(data[10])})
			}
		}
	}
	return onus, nil
}

func (c *Client) GetOnuOptics(ctx context.Context, oltIP, onuSN string) (*OnuInfo, error) {
	if err := validateOnuSN(onuSN); err != nil {
		return nil, err
	}
	snBytes := make([]byte, 16)
	copy(snBytes, []byte(onuSN))
	_, err := c.SendAndWait(ctx, oltIP, CmdCpeOptParaReport, snBytes, DefaultTimeout)
	if err != nil {
		return nil, err
	}
	return &OnuInfo{SN: onuSN}, nil
}

// ── Whitelist Management ──

func (c *Client) GetWhitelistMode(ctx context.Context, oltIP string) (*WhitelistMode, error) {
	resp, err := c.SendAndWait(ctx, oltIP, CmdWhiteListReadQty, []byte{0x00}, DefaultTimeout)
	if err != nil {
		return nil, err
	}
	mode := "unknown"
	if len(resp) > 3 {
		switch resp[3] {
		case 'W':
			mode = "whitelist"
		case 'G':
			mode = "graylist"
		}
	}
	return &WhitelistMode{Mode: mode}, nil
}

func (c *Client) SetWhitelistMode(ctx context.Context, oltIP string) error {
	_, err := c.SendAndWait(ctx, oltIP, CmdWhiteListSend, whitelistModeEnable, DefaultTimeout)
	return err
}

func (c *Client) SetGraylistMode(ctx context.Context, oltIP string) error {
	_, err := c.SendAndWait(ctx, oltIP, CmdWhiteListSend, graylistModeEnable, DefaultTimeout)
	return err
}

func (c *Client) AddOnuToWhitelist(ctx context.Context, oltIP, onuSN string, serviceProfile int) error {
	if err := validateOnuSN(onuSN); err != nil {
		return err
	}
	if serviceProfile < 1 || serviceProfile > 5 {
		return fmt.Errorf("service_profile must be 1-5, got %d", serviceProfile)
	}
	data := make([]byte, 17)
	copy(data, []byte(onuSN))
	data[16] = byte(serviceProfile)
	_, err := c.SendAndWait(ctx, oltIP, CmdWhiteListSend, data, DefaultTimeout)
	return err
}

func (c *Client) RemoveOnuFromWhitelist(ctx context.Context, oltIP, onuSN string) error {
	if err := validateOnuSN(onuSN); err != nil {
		return err
	}
	data := make([]byte, 16)
	copy(data, []byte(onuSN))
	_, err := c.SendAndWait(ctx, oltIP, CmdWhiteListDel, data, DefaultTimeout)
	return err
}

func (c *Client) GetWhitelist(ctx context.Context, oltIP string) ([]byte, error) {
	return c.SendAndWait(ctx, oltIP, CmdOnuWlistRpt, nil, DefaultTimeout)
}

func (c *Client) GetWhitelistCount(ctx context.Context, oltIP string) (int, error) {
	resp, err := c.SendAndWait(ctx, oltIP, CmdWhiteListReadQty, nil, DefaultTimeout)
	if err != nil {
		return 0, err
	}
	if len(resp) > 3 {
		return int(resp[3]), nil
	}
	return 0, nil
}

// ── Service Profiles ──

func (c *Client) GetServiceConfig(ctx context.Context, oltIP string) ([]byte, error) {
	return c.SendAndWait(ctx, oltIP, CmdServiceConfigRpt, nil, DefaultTimeout)
}

func (c *Client) SetServiceConfig(ctx context.Context, oltIP string, data []byte) error {
	if len(data) > 19 {
		return fmt.Errorf("service config data too large (max 19 bytes, got %d)", len(data))
	}
	_, err := c.SendAndWait(ctx, oltIP, CmdServiceTypeSend, data, DefaultTimeout)
	return err
}

// ── Alarms ──

func (c *Client) GetAlarms(ctx context.Context, oltIP string) (*AlarmInfo, error) {
	resp, err := c.SendAndWait(ctx, oltIP, CmdOltAlarmReport, nil, DefaultTimeout)
	if err != nil {
		return nil, err
	}
	return &AlarmInfo{
		Raw:      hex.EncodeToString(resp),
		HasAlarm: len(resp) > 3 && resp[3] != 0,
	}, nil
}

func (c *Client) GetOnuAlarms(ctx context.Context, oltIP string) ([]byte, error) {
	return c.SendAndWait(ctx, oltIP, CmdCpeAlarmReport, nil, DefaultTimeout)
}

// ── OLT Configuration ──

func (c *Client) ChangeOltIP(ctx context.Context, oltIP, newIP string) error {
	ip := net.ParseIP(newIP).To4()
	if ip == nil {
		return fmt.Errorf("%w: %s", ErrInvalidIP, newIP)
	}
	_, err := c.SendAndWait(ctx, oltIP, CmdIpConfiguration, ip, DefaultTimeout)
	return err
}

func (c *Client) RebootOlt(ctx context.Context, oltIP string) error {
	_, err := c.SendAndWait(ctx, oltIP, CmdOltSoftreset, nil, DefaultTimeout)
	return err
}

func (c *Client) UpgradeFirmware(ctx context.Context, oltIP string, data []byte) error {
	if len(data) > 19 {
		return fmt.Errorf("firmware data chunk too large (max 19 bytes, got %d)", len(data))
	}
	_, err := c.SendAndWait(ctx, oltIP, CmdOltUpdateBin, data, 10*time.Second)
	return err
}

// ── Raw Command ──

func (c *Client) SendRawCommand(ctx context.Context, oltIP string, cmd CommandCode, data []byte) ([]byte, error) {
	if !IsValidCommand(int(cmd)) {
		return nil, fmt.Errorf("unknown command code %d", cmd)
	}
	return c.SendAndWait(ctx, oltIP, cmd, data, 5*time.Second)
}

// ── OLT Cache ──

func (c *Client) GetOlt(ip string) *OltStatus {
	c.oltsMu.RLock()
	defer c.oltsMu.RUnlock()
	orig, ok := c.olts[ip]
	if !ok {
		return nil
	}
	copy := *orig
	return &copy
}

func (c *Client) GetAllOlts() []*OltStatus {
	c.oltsMu.RLock()
	defer c.oltsMu.RUnlock()
	result := make([]*OltStatus, 0, len(c.olts))
	for _, o := range c.olts {
		copy := *o
		result = append(result, &copy)
	}
	return result
}

func (c *Client) SetOltOffline(ip string) {
	c.oltsMu.Lock()
	defer c.oltsMu.Unlock()
	if s, ok := c.olts[ip]; ok {
		s.Status = "offline"
	}
}

func (c *Client) RemoveOlt(ip string) {
	c.oltsMu.Lock()
	defer c.oltsMu.Unlock()
	delete(c.olts, ip)
}

func (c *Client) PendingCount() int {
	c.pendingMu.Lock()
	defer c.pendingMu.Unlock()
	return len(c.pending)
}

// ── Validation helpers ──

func validateOnuSN(sn string) error {
	if sn == "" {
		return fmt.Errorf("onu sn required")
	}
	if len(sn) > MaxOnuSnLen {
		return fmt.Errorf("onu sn too long (max %d chars)", MaxOnuSnLen)
	}
	for _, r := range sn {
		if r < 32 || r > 126 {
			return fmt.Errorf("onu sn contains non-printable characters")
		}
	}
	return nil
}
