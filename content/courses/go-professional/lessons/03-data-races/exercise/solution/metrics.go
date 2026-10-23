// Package metrics is a tiny in-process metrics registry used by several
// request-handling goroutines at once.
package metrics

import (
	"sync"
	"sync/atomic"
)

// Counter is a monotonically adjustable 64-bit counter. The zero value is
// ready to use. It is shared between goroutines.
type Counter struct {
	n atomic.Int64
}

// Add adds delta to the counter.
func (c *Counter) Add(delta int64) {
	c.n.Add(delta)
}

// Value returns the current value.
func (c *Counter) Value() int64 {
	return c.n.Load()
}

// Reset sets the counter to zero and returns the value it had. No increment
// may be lost: every Add is counted either in a returned value or in the
// final Value.
func (c *Counter) Reset() int64 {
	return c.n.Swap(0)
}

// Registry maps metric names to counters, creating them on first use.
type Registry struct {
	mu       sync.RWMutex
	counters map[string]*Counter
}

// NewRegistry returns an empty registry.
func NewRegistry() *Registry {
	return &Registry{counters: make(map[string]*Counter)}
}

// counter returns the counter for name, creating it if needed. All callers
// asking for the same name must get the same *Counter.
func (r *Registry) counter(name string) *Counter {
	r.mu.RLock()
	c, ok := r.counters[name]
	r.mu.RUnlock()
	if ok {
		return c
	}
	r.mu.Lock()
	defer r.mu.Unlock()
	// Re-check: someone may have created it while we waited for the lock.
	if c, ok := r.counters[name]; ok {
		return c
	}
	c = &Counter{}
	r.counters[name] = c
	return c
}

// Add adds delta to the named counter.
func (r *Registry) Add(name string, delta int64) {
	r.counter(name).Add(delta)
}

// Inc adds one to the named counter.
func (r *Registry) Inc(name string) {
	r.Add(name, 1)
}

// Get returns the value of the named counter, or 0 if it does not exist.
// Get must not create the counter.
func (r *Registry) Get(name string) int64 {
	r.mu.RLock()
	c, ok := r.counters[name]
	r.mu.RUnlock()
	if !ok {
		return 0
	}
	return c.Value()
}

// Snapshot returns the current values of all counters. The caller owns the
// returned map: later changes to the registry must not show up in it, and
// changes to it must not affect the registry.
func (r *Registry) Snapshot() map[string]int64 {
	r.mu.RLock()
	defer r.mu.RUnlock()
	out := make(map[string]int64, len(r.counters))
	for name, c := range r.counters {
		out[name] = c.Value()
	}
	return out
}
