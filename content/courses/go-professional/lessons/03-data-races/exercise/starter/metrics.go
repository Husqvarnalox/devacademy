// Package metrics is a tiny in-process metrics registry used by several
// request-handling goroutines at once.
package metrics

import (
	"runtime"
	"sync"
)

// Counter is a monotonically adjustable 64-bit counter. The zero value is
// ready to use. It is shared between goroutines.
type Counter struct {
	n int64
}

// Add adds delta to the counter.
func (c *Counter) Add(delta int64) {
	// Bug: read-modify-write without synchronization. The Gosched call makes
	// the window between the read and the write explicit so that the lost
	// update reproduces on every run, not once in a thousand.
	v := c.n
	runtime.Gosched()
	c.n = v + delta
}

// Value returns the current value.
func (c *Counter) Value() int64 {
	return c.n
}

// Reset sets the counter to zero and returns the value it had. No increment
// may be lost: every Add is counted either in a returned value or in the
// final Value.
func (c *Counter) Reset() int64 {
	v := c.n
	runtime.Gosched()
	c.n = 0
	return v
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
	// Bug: check-then-act. Between RUnlock and Lock another goroutine may
	// have created the counter, and we overwrite it.
	runtime.Gosched()
	r.mu.Lock()
	c = &Counter{}
	r.counters[name] = c
	r.mu.Unlock()
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
	// Bug: iterates the map without holding the lock, so a concurrent Inc
	// that creates a counter makes the runtime abort the program.
	out := make(map[string]int64, len(r.counters))
	for name, c := range r.counters {
		out[name] = c.Value()
	}
	return out
}
