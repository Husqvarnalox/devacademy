// Package accesslog aggregates access-log lines of the form
//
//	METHOD PATH STATUS BYTES      e.g.  GET /index.html 200 5123
//
// (single spaces, STATUS 100..599, BYTES >= 0). Add runs for every request of
// a busy service, so for a valid line it must not allocate in steady state.
package accesslog

import (
	"errors"
	"fmt"
	"strconv"
	"strings"
)

// ErrBadLine is wrapped by every error returned for a malformed line.
var ErrBadLine = errors.New("accesslog: bad line")

// Entry is one parsed line.
type Entry struct {
	Method string
	Path   string
	Status int
	Bytes  int64
}

// Stats is a snapshot of the aggregate.
type Stats struct {
	Total    int    // accepted lines
	Rejected int    // lines that returned an error
	Bytes    int64  // sum of BYTES over accepted lines
	Max      int64  // largest BYTES value seen
	Class    [6]int // accepted lines by status class: Class[2] counts 2xx; indexes 0 and 1 stay 0
}

// key identifies a (method, status) pair. A struct key replaces the old
// Sprintf-built string: no formatting, no boxing, no new string per line.
type key struct {
	method string
	status int
}

// Aggregator collects statistics. It is not safe for concurrent use.
type Aggregator struct {
	stats Stats
	byKey map[key]int
}

// NewAggregator returns an empty Aggregator.
func NewAggregator() *Aggregator {
	return &Aggregator{byKey: make(map[key]int)}
}

// Add parses and records one line. A rejected line changes only Stats.Rejected.
func (a *Aggregator) Add(line string) error {
	e, err := parseEntry(line)
	if err != nil {
		a.stats.Rejected++
		return err
	}
	a.record(e)
	return nil
}

// parseEntry returns the entry by value: a small struct is copied cheaply and
// never needs a heap object of its own. Substrings share the memory of line.
func parseEntry(line string) (Entry, error) {
	method, rest, ok1 := strings.Cut(line, " ")
	path, rest, ok2 := strings.Cut(rest, " ")
	statusText, bytesText, ok3 := strings.Cut(rest, " ")
	if !ok1 || !ok2 || !ok3 {
		return Entry{}, fmt.Errorf("%w: want 4 fields", ErrBadLine)
	}
	status, err := strconv.Atoi(statusText)
	if err != nil || status < 100 || status > 599 {
		return Entry{}, fmt.Errorf("%w: status %q", ErrBadLine, statusText)
	}
	n, err := strconv.ParseInt(bytesText, 10, 64)
	if err != nil || n < 0 {
		return Entry{}, fmt.Errorf("%w: bytes %q", ErrBadLine, bytesText)
	}
	return Entry{Method: method, Path: path, Status: status, Bytes: n}, nil
}

func (a *Aggregator) record(e Entry) {
	a.stats.Total++
	a.stats.Bytes += e.Bytes
	if e.Bytes > a.stats.Max {
		a.stats.Max = e.Bytes
	}
	a.stats.Class[e.Status/100]++
	a.byKey[key{e.Method, e.Status}]++
}

// Count returns how many accepted lines had this method and status.
func (a *Aggregator) Count(method string, status int) int {
	return a.byKey[key{method, status}]
}

// Stats returns a snapshot of the aggregate.
func (a *Aggregator) Stats() Stats {
	return a.stats
}
