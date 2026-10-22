// Package config provides a lazily loaded, shared application configuration.
package config

import "errors"

// Config is an immutable snapshot of the settings. Once returned from
// Service.Config it is shared by many goroutines and must not be modified.
type Config struct {
	Name   string
	Limits map[string]int
}

// ErrNotConfigured is returned when a Service was built without a loader.
var ErrNotConfigured = errors.New("config: no loader")

// Service loads the configuration on first use.
//
// Requirements:
//   - the loader runs exactly once, no matter how many goroutines call Config
//     at the same time, and its result (value or error) is remembered;
//   - every caller sees the fully built *Config that the loader returned;
//   - Ready returns a channel that is closed once loading has finished, with a
//     value or with an error. Receiving from it must be a valid way to wait for
//     the configuration. The channel is never closed twice.
type Service struct {
	load   func() (*Config, error)
	loaded bool
	cfg    *Config
	err    error
	ready  chan struct{}
}

// NewService returns a Service that will call load on first use. It does not
// call load itself.
func NewService(load func() (*Config, error)) *Service {
	return &Service{load: load, ready: make(chan struct{})}
}

// Config returns the configuration, loading it on first call.
func (s *Service) Config() (*Config, error) {
	if s.load == nil {
		return nil, ErrNotConfigured
	}
	// Bug: a plain bool is not synchronization. Two goroutines can both see
	// loaded == false and both run the loader; a goroutine that sees
	// loaded == true is not guaranteed to see cfg and err.
	if !s.loaded {
		s.cfg, s.err = s.load()
		s.loaded = true
	}
	return s.cfg, s.err
}

// Ready returns a channel that is closed when loading has finished.
func (s *Service) Ready() <-chan struct{} {
	return s.ready // Bug: nobody ever closes it.
}
