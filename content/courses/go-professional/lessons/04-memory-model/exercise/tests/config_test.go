package config

import (
	"errors"
	"sync"
	"sync/atomic"
	"testing"
	"time"
)

func newCounting(delay time.Duration, err error) (*Service, *atomic.Int32) {
	var calls atomic.Int32
	s := NewService(func() (*Config, error) {
		calls.Add(1)
		time.Sleep(delay) // a slow loader keeps the window for a second caller wide open
		if err != nil {
			return nil, err
		}
		limits := make(map[string]int)
		for _, k := range []string{"a", "b", "c", "d", "e", "f", "g", "h"} {
			limits[k] = len(k) + int(k[0])
		}
		return &Config{Name: "prod", Limits: limits}, nil
	})
	return s, &calls
}

func checkFull(t *testing.T, c *Config) {
	t.Helper()
	if c == nil {
		t.Error("Config returned nil without an error")
		return
	}
	if c.Name != "prod" || len(c.Limits) != 8 || c.Limits["h"] != 1+int('h') {
		t.Errorf("incomplete configuration: %+v", c)
	}
}

func TestConfigLoadsOnce(t *testing.T) {
	s, calls := newCounting(20*time.Millisecond, nil)
	if calls.Load() != 0 {
		t.Fatal("NewService must not call the loader")
	}
	const n = 40
	got := make([]*Config, n)
	var wg sync.WaitGroup
	start := make(chan struct{})
	for i := 0; i < n; i++ {
		wg.Add(1)
		go func(i int) {
			defer wg.Done()
			<-start
			c, err := s.Config()
			if err != nil {
				t.Errorf("unexpected error: %v", err)
			}
			got[i] = c
		}(i)
	}
	close(start)
	wg.Wait()
	if c := calls.Load(); c != 1 {
		t.Fatalf("loader called %d times, want exactly 1", c)
	}
	for i, c := range got {
		checkFull(t, c)
		if c != got[0] {
			t.Fatalf("goroutine %d got a different *Config", i)
		}
	}
}

func TestConfigErrorIsRemembered(t *testing.T) {
	boom := errors.New("boom")
	s, calls := newCounting(5*time.Millisecond, boom)
	var wg sync.WaitGroup
	for i := 0; i < 10; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			if _, err := s.Config(); !errors.Is(err, boom) {
				t.Errorf("err = %v, want boom", err)
			}
		}()
	}
	wg.Wait()
	if _, err := s.Config(); !errors.Is(err, boom) {
		t.Errorf("later call: err = %v, want boom", err)
	}
	if c := calls.Load(); c != 1 {
		t.Fatalf("loader called %d times, want 1 (the failure is remembered)", c)
	}
}

func TestNoLoader(t *testing.T) {
	if _, err := NewService(nil).Config(); !errors.Is(err, ErrNotConfigured) {
		t.Fatalf("err = %v, want ErrNotConfigured", err)
	}
}

func TestReadySignal(t *testing.T) {
	s, calls := newCounting(10*time.Millisecond, nil)
	select {
	case <-s.Ready():
		t.Fatal("Ready is closed before anything was loaded")
	default:
	}

	// Waiters block on Ready, then read the configuration. After Ready is
	// closed they must see the complete value without another load.
	const waiters = 6
	var wg sync.WaitGroup
	for i := 0; i < waiters; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			select {
			case <-s.Ready():
			case <-time.After(3 * time.Second):
				t.Error("timed out waiting for Ready")
				return
			}
			c, err := s.Config()
			if err != nil {
				t.Errorf("unexpected error: %v", err)
			}
			checkFull(t, c)
		}()
	}
	time.Sleep(20 * time.Millisecond) // let the waiters park on the channel
	if _, err := s.Config(); err != nil {
		t.Fatal(err)
	}
	wg.Wait()
	if c := calls.Load(); c != 1 {
		t.Fatalf("loader called %d times, want 1", c)
	}
}

func TestReadyClosedAfterFailureToo(t *testing.T) {
	s, _ := newCounting(0, errors.New("nope"))
	_, _ = s.Config()
	select {
	case <-s.Ready():
	case <-time.After(3 * time.Second):
		t.Fatal("Ready must be closed when loading ends with an error as well")
	}
	// Further calls must not close the channel again (that would panic).
	for i := 0; i < 3; i++ {
		_, _ = s.Config()
	}
}
