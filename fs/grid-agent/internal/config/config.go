package config

import (
	"encoding/json"
	"os"
)

type Config struct {
	ListenAddr  string `json:"listen_addr"`
	Interface   string `json:"interface"`
	AuthToken   string `json:"auth_token"`
	AgentName   string `json:"agent_name"`
	SiteLabel   string `json:"site_label"`
	PollInterval int   `json:"poll_interval_seconds"`
}

func DefaultConfig() *Config {
	return &Config{
		ListenAddr:   ":8420",
		Interface:    "",
		AuthToken:    "grid-agent-secret",
		AgentName:    "grid-agent",
		SiteLabel:    "default",
		PollInterval: 5,
	}
}

func Load(path string) (*Config, error) {
	cfg := DefaultConfig()
	data, err := os.ReadFile(path)
	if err != nil {
		return cfg, nil // use defaults if no config file
	}
	if err := json.Unmarshal(data, cfg); err != nil {
		return nil, err
	}
	return cfg, nil
}
