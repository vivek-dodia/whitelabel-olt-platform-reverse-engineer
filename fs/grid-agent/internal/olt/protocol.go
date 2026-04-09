package olt

import (
	"encoding/hex"
	"fmt"
	"net"
	"strings"
	"sync"
	"time"
)

const (
	TxPort = 64219
	RxPort = 64218
)

// Command codes from FS PON Manager reverse engineering
type CommandCode byte

const (
	CmdShakeHand          CommandCode = 1
	CmdIpConfiguration    CommandCode = 2
	CmdWhiteListSend      CommandCode = 3
	CmdWhiteListReadQty   CommandCode = 4
	CmdOnuWlistRpt        CommandCode = 5
	CmdIllegalCpeReport   CommandCode = 6
	CmdCpeAlarmReport     CommandCode = 7
	CmdOltAlarmReport     CommandCode = 8
	CmdServiceTypeSend    CommandCode = 9
	CmdWhiteListDel       CommandCode = 10
	CmdCpeOptParaReport   CommandCode = 11
	CmdCpeSnStatus        CommandCode = 12
	CmdServiceConfigRpt   CommandCode = 13
	CmdPasswordCmd        CommandCode = 66
	CmdPasswordCheckCmd   CommandCode = 67
	CmdOltUpdateBin       CommandCode = 68
	CmdOltResetMaster     CommandCode = 69
	CmdOltResetSlave      CommandCode = 70
	CmdOltSoftreset       CommandCode = 71
)

var writeAuthKey, _ = hex.DecodeString("5774b87337454200d4d33f80c4663dc5e5")
var readAuthKey, _ = hex.DecodeString("5274b87337454200d4d33f80c4663dc5e5")

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
	SN     string `json:"sn"`
	Status int    `json:"status"`
}

type AlarmInfo struct {
	Raw string `json:"raw"`
}

// Client manages UDP communication with FS OLT sticks on the local subnet
type Client struct {
	mu       sync.Mutex
	rxConn   *net.UDPConn
	txConn   *net.UDPConn
	seq      uint16
	pending  map[string]chan []byte // key: "ip:seq"
	olts     map[string]*OltStatus
	oltsMu   sync.RWMutex
}

func NewClient() (*Client, error) {
	rxAddr, err := net.ResolveUDPAddr("udp4", fmt.Sprintf(":%d", RxPort))
	if err != nil {
		return nil, fmt.Errorf("resolve rx addr: %w", err)
	}
	rxConn, err := net.ListenUDP("udp4", rxAddr)
	if err != nil {
		return nil, fmt.Errorf("listen rx: %w", err)
	}

	txAddr, err := net.ResolveUDPAddr("udp4", fmt.Sprintf(":%d", TxPort))
	if err != nil {
		rxConn.Close()
		return nil, fmt.Errorf("resolve tx addr: %w", err)
	}
	txConn, err := net.ListenUDP("udp4", txAddr)
	if err != nil {
		rxConn.Close()
		return nil, fmt.Errorf("listen tx: %w", err)
	}

	c := &Client{
		rxConn:  rxConn,
		txConn:  txConn,
		pending: make(map[string]chan []byte),
		olts:    make(map[string]*OltStatus),
	}

	go c.receiveLoop()
	return c, nil
}

func (c *Client) Close() {
	c.rxConn.Close()
	c.txConn.Close()
}

func (c *Client) receiveLoop() {
	buf := make([]byte, 4096)
	for {
		n, addr, err := c.rxConn.ReadFromUDP(buf)
		if err != nil {
			return
		}
		if n < 3 {
			continue
		}

		data := make([]byte, n)
		copy(data, buf[:n])

		seq := uint16(data[1])<<8 | uint16(data[2])
		key := fmt.Sprintf("%s:%d", addr.IP.String(), seq)

		c.mu.Lock()
		ch, ok := c.pending[key]
		if ok {
			delete(c.pending, key)
		}
		c.mu.Unlock()

		if ok {
			ch <- data
		}
	}
}

func (c *Client) nextSeq() uint16 {
	c.mu.Lock()
	defer c.mu.Unlock()
	c.seq++
	return c.seq
}

func (c *Client) buildPacket(cmd CommandCode, seq uint16, data []byte) []byte {
	pkt := make([]byte, 22)
	pkt[0] = byte(cmd)
	pkt[1] = byte(seq >> 8)
	pkt[2] = byte(seq & 0xff)
	if data != nil {
		copy(pkt[3:], data)
	}
	return pkt
}

func (c *Client) SendAndWait(oltIP string, cmd CommandCode, data []byte, timeout time.Duration) ([]byte, error) {
	seq := c.nextSeq()
	pkt := c.buildPacket(cmd, seq, data)

	key := fmt.Sprintf("%s:%d", oltIP, seq)
	ch := make(chan []byte, 1)

	c.mu.Lock()
	c.pending[key] = ch
	c.mu.Unlock()

	dst, err := net.ResolveUDPAddr("udp4", fmt.Sprintf("%s:%d", oltIP, TxPort))
	if err != nil {
		c.mu.Lock()
		delete(c.pending, key)
		c.mu.Unlock()
		return nil, err
	}

	_, err = c.txConn.WriteToUDP(pkt, dst)
	if err != nil {
		c.mu.Lock()
		delete(c.pending, key)
		c.mu.Unlock()
		return nil, err
	}

	select {
	case resp := <-ch:
		return resp, nil
	case <-time.After(timeout):
		c.mu.Lock()
		delete(c.pending, key)
		c.mu.Unlock()
		return nil, fmt.Errorf("timeout waiting for response from %s", oltIP)
	}
}

// ShakeHand sends a handshake and returns OLT info
func (c *Client) ShakeHand(oltIP string) (*OltStatus, error) {
	resp, err := c.SendAndWait(oltIP, CmdShakeHand, nil, 3*time.Second)
	if err != nil {
		return nil, err
	}

	if len(resp) < 20 {
		return nil, fmt.Errorf("shake_hand response too short: %d bytes", len(resp))
	}

	mac := fmt.Sprintf("00:00:%02x:%02x:%02x:%02x:%02x:%02x",
		resp[4], resp[5], resp[6], resp[7], resp[8], resp[9])
	serial := strings.TrimLeft(strings.TrimRight(string(resp[20:]), "\x00 "), "\x00")

	status := &OltStatus{
		IP:       oltIP,
		MAC:      mac,
		Serial:   serial,
		Status:   "online",
		LastSeen: time.Now().UnixMilli(),
	}

	c.oltsMu.Lock()
	c.olts[oltIP] = status
	c.oltsMu.Unlock()

	return status, nil
}

// EnableWrite sends write auth
func (c *Client) EnableWrite(oltIP string) (bool, error) {
	resp, err := c.SendAndWait(oltIP, CmdPasswordCmd, writeAuthKey, 3*time.Second)
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

// EnableRead sends read auth
func (c *Client) EnableRead(oltIP string) (bool, error) {
	resp, err := c.SendAndWait(oltIP, CmdPasswordCmd, readAuthKey, 3*time.Second)
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

// GetOnuStatus queries connected ONUs
func (c *Client) GetOnuStatus(oltIP string) ([]OnuInfo, error) {
	resp, err := c.SendAndWait(oltIP, CmdCpeSnStatus, nil, 3*time.Second)
	if err != nil {
		return nil, err
	}

	var onus []OnuInfo
	if len(resp) > 3 {
		// Parse ONU SN data: [cmd(1)][seq(2)][sn(10)][status(1)]...
		data := resp[3:]
		if len(data) >= 11 {
			sn := strings.TrimRight(string(data[:10]), "\x00 ")
			status := int(data[10])
			onus = append(onus, OnuInfo{SN: sn, Status: status})
		}
	}
	return onus, nil
}

// GetAlarms queries OLT alarm status
func (c *Client) GetAlarms(oltIP string) (*AlarmInfo, error) {
	resp, err := c.SendAndWait(oltIP, CmdOltAlarmReport, nil, 3*time.Second)
	if err != nil {
		return nil, err
	}
	return &AlarmInfo{Raw: hex.EncodeToString(resp)}, nil
}

// SendRawCommand sends any command code with optional data
func (c *Client) SendRawCommand(oltIP string, cmd CommandCode, data []byte) ([]byte, error) {
	return c.SendAndWait(oltIP, cmd, data, 5*time.Second)
}

// GetOlt returns cached OLT status
func (c *Client) GetOlt(ip string) *OltStatus {
	c.oltsMu.RLock()
	defer c.oltsMu.RUnlock()
	return c.olts[ip]
}

// GetAllOlts returns all known OLTs
func (c *Client) GetAllOlts() []*OltStatus {
	c.oltsMu.RLock()
	defer c.oltsMu.RUnlock()
	result := make([]*OltStatus, 0, len(c.olts))
	for _, olt := range c.olts {
		result = append(result, olt)
	}
	return result
}

// SetOltOffline marks an OLT as offline
func (c *Client) SetOltOffline(ip string) {
	c.oltsMu.Lock()
	defer c.oltsMu.Unlock()
	if s, ok := c.olts[ip]; ok {
		s.Status = "offline"
	}
}

// RemoveOlt removes an OLT from tracking
func (c *Client) RemoveOlt(ip string) {
	c.oltsMu.Lock()
	defer c.oltsMu.Unlock()
	delete(c.olts, ip)
}
