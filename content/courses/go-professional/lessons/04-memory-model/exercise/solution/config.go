// Package config provides a lazily loaded, shared application configuration.
package config

import (
	"errors"
	"sync"
)

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
	load  func() (*Config, error)
	once  sync.Once
	cfg   *Config
	err   error
	ready chan struct{}
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
	// Do returns only after the function has finished, in every goroutine
	// that calls it: that is the happens-before edge which makes cfg and err
	// visible without any other lock.
	s.once.Do(func() {
		s.cfg, s.err = s.load()
		close(s.ready) // after the writes: receivers observe cfg and err
	})
	return s.cfg, s.err
}

// Ready returns a channel that is closed when loading has finished.
func (s *Service) Ready() <-chan struct{} {
	return s.ready
}
