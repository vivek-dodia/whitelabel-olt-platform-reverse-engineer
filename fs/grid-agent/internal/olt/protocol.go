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

var writeAuthKey, _ = hex.DecodeString("5774b87337454200d4d33f80c4663dc5e5")
var readAuthKey, _ = hex.DecodeString("5274b87337454200d4d33f80c4663dc5e5")

// Whitelist mode bytes from decompiled FS source
var whitelistModeEnable = []byte{0x57, 0x01} // set_white_list_type(0x57,1)
var graylistModeEnable = []byte{0x47, 0x01}  // set_white_list_type(0x47,1)
var whitelistModeQuery = []byte{0x00}        // get_white_list_type()

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
	SN     string  `json:"sn"`
	Status int     `json:"status"`
	TxPwr  float64 `json:"tx_pwr,omitempty"`
	RxPwr  float64 `json:"rx_pwr,omitempty"`
	Bias   float64 `json:"bias,omitempty"`
	Temp   float64 `json:"temperature,omitempty"`
	Volt   float64 `json:"voltage,omitempty"`
}

type AlarmInfo struct {
	Raw       string `json:"raw"`
	HasAlarm  bool   `json:"has_alarm"`
}

type WhitelistMode struct {
	Mode string `json:"mode"` // "whitelist" or "graylist"
}

type Client struct {
	mu      sync.Mutex
	rxConn  *net.UDPConn
	txConn  *net.UDPConn
	seq     uint16
	pending map[string]chan []byte
	olts    map[string]*OltStatus
	oltsMu  sync.RWMutex
}

func NewClient() (*Client, error) {
	rxAddr, _ := net.ResolveUDPAddr("udp4", fmt.Sprintf(":%d", RxPort))
	rxConn, err := net.ListenUDP("udp4", rxAddr)
	if err != nil {
		return nil, fmt.Errorf("listen rx: %w", err)
	}

	txAddr, _ := net.ResolveUDPAddr("udp4", fmt.Sprintf(":%d", TxPort))
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

	dst, _ := net.ResolveUDPAddr("udp4", fmt.Sprintf("%s:%d", oltIP, TxPort))
	_, err := c.txConn.WriteToUDP(pkt, dst)
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

// ── Discovery & Status ──

func (c *Client) ShakeHand(oltIP string) (*OltStatus, error) {
	resp, err := c.SendAndWait(oltIP, CmdShakeHand, nil, 3*time.Second)
	if err != nil {
		return nil, err
	}
	if len(resp) < 20 {
		return nil, fmt.Errorf("response too short: %d bytes", len(resp))
	}

	mac := fmt.Sprintf("00:00:%02x:%02x:%02x:%02x:%02x:%02x",
		resp[4], resp[5], resp[6], resp[7], resp[8], resp[9])
	serial := strings.TrimLeft(strings.TrimRight(string(resp[20:]), "\x00 "), "\x00")

	status := &OltStatus{
		IP: oltIP, MAC: mac, Serial: serial,
		Status: "online", LastSeen: time.Now().UnixMilli(),
	}

	c.oltsMu.Lock()
	c.olts[oltIP] = status
	c.oltsMu.Unlock()
	return status, nil
}

// ── Auth ──

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

// ── ONU Management ──

func (c *Client) GetOnuStatus(oltIP string) ([]OnuInfo, error) {
	resp, err := c.SendAndWait(oltIP, CmdCpeSnStatus, nil, 3*time.Second)
	if err != nil {
		return nil, err
	}
	var onus []OnuInfo
	if len(resp) > 3 {
		data := resp[3:]
		if len(data) >= 11 {
			sn := strings.TrimRight(string(data[:10]), "\x00 ")
			status := int(data[10])
			onus = append(onus, OnuInfo{SN: sn, Status: status})
		}
	}
	return onus, nil
}

func (c *Client) GetOnuOptics(oltIP string, onuSN string) (*OnuInfo, error) {
	// Build get_onu_optics payload: SN as ASCII bytes
	snBytes := make([]byte, 16)
	copy(snBytes, []byte(onuSN))
	resp, err := c.SendAndWait(oltIP, CmdCpeOptParaReport, snBytes, 3*time.Second)
	if err != nil {
		return nil, err
	}
	if len(resp) < 14 {
		return &OnuInfo{SN: onuSN}, nil
	}
	// Parse optical params from response
	return &OnuInfo{
		SN:  onuSN,
		// Raw data — proper parsing depends on exact response format
	}, nil
}

// ── Whitelist Management ──

func (c *Client) GetWhitelistMode(oltIP string) (*WhitelistMode, error) {
	resp, err := c.SendAndWait(oltIP, CmdWhiteListReadQty, whitelistModeQuery, 3*time.Second)
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

func (c *Client) SetWhitelistMode(oltIP string) error {
	_, err := c.SendAndWait(oltIP, CmdWhiteListSend, whitelistModeEnable, 3*time.Second)
	return err
}

func (c *Client) SetGraylistMode(oltIP string) error {
	_, err := c.SendAndWait(oltIP, CmdWhiteListSend, graylistModeEnable, 3*time.Second)
	return err
}

func (c *Client) AddOnuToWhitelist(oltIP string, onuSN string, serviceProfile int) error {
	// Payload: SN (up to 16 bytes) + service profile (1 byte)
	data := make([]byte, 17)
	copy(data, []byte(onuSN))
	data[16] = byte(serviceProfile)
	_, err := c.SendAndWait(oltIP, CmdWhiteListSend, data, 3*time.Second)
	return err
}

func (c *Client) RemoveOnuFromWhitelist(oltIP string, onuSN string) error {
	data := make([]byte, 16)
	copy(data, []byte(onuSN))
	_, err := c.SendAndWait(oltIP, CmdWhiteListDel, data, 3*time.Second)
	return err
}

func (c *Client) GetWhitelist(oltIP string) ([]byte, error) {
	return c.SendAndWait(oltIP, CmdOnuWlistRpt, nil, 3*time.Second)
}

func (c *Client) GetWhitelistCount(oltIP string) (int, error) {
	resp, err := c.SendAndWait(oltIP, CmdWhiteListReadQty, nil, 3*time.Second)
	if err != nil {
		return 0, err
	}
	if len(resp) > 3 {
		return int(resp[3]), nil
	}
	return 0, nil
}

// ── Service Profiles ──

func (c *Client) GetServiceConfig(oltIP string) ([]byte, error) {
	return c.SendAndWait(oltIP, CmdServiceConfigRpt, nil, 3*time.Second)
}

func (c *Client) SetServiceConfig(oltIP string, data []byte) error {
	_, err := c.SendAndWait(oltIP, CmdServiceTypeSend, data, 3*time.Second)
	return err
}

// ── Alarms ──

func (c *Client) GetAlarms(oltIP string) (*AlarmInfo, error) {
	resp, err := c.SendAndWait(oltIP, CmdOltAlarmReport, nil, 3*time.Second)
	if err != nil {
		return nil, err
	}
	raw := hex.EncodeToString(resp)
	hasAlarm := len(resp) > 3 && resp[3] != 0
	return &AlarmInfo{Raw: raw, HasAlarm: hasAlarm}, nil
}

func (c *Client) GetOnuAlarms(oltIP string) ([]byte, error) {
	return c.SendAndWait(oltIP, CmdCpeAlarmReport, nil, 3*time.Second)
}

// ── OLT Configuration ──

func (c *Client) ChangeOltIP(oltIP string, newIP string) error {
	ip := net.ParseIP(newIP).To4()
	if ip == nil {
		return fmt.Errorf("invalid IP: %s", newIP)
	}
	_, err := c.SendAndWait(oltIP, CmdIpConfiguration, ip, 3*time.Second)
	return err
}

func (c *Client) RebootOlt(oltIP string) error {
	_, err := c.SendAndWait(oltIP, CmdOltSoftreset, nil, 3*time.Second)
	return err
}

func (c *Client) UpgradeFirmware(oltIP string, data []byte) error {
	_, err := c.SendAndWait(oltIP, CmdOltUpdateBin, data, 10*time.Second)
	return err
}

// ── Raw Command ──

func (c *Client) SendRawCommand(oltIP string, cmd CommandCode, data []byte) ([]byte, error) {
	return c.SendAndWait(oltIP, cmd, data, 5*time.Second)
}

// ── OLT Cache ──

func (c *Client) GetOlt(ip string) *OltStatus {
	c.oltsMu.RLock()
	defer c.oltsMu.RUnlock()
	return c.olts[ip]
}

func (c *Client) GetAllOlts() []*OltStatus {
	c.oltsMu.RLock()
	defer c.oltsMu.RUnlock()
	result := make([]*OltStatus, 0, len(c.olts))
	for _, o := range c.olts {
		result = append(result, o)
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
