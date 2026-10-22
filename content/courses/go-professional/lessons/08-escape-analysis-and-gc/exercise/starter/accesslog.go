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

// Aggregator collects statistics. It is not safe for concurrent use.
type Aggregator struct {
	stats Stats
	byKey map[string]int // key is "METHOD STATUS", e.g. "GET 200"
}

// NewAggregator returns an empty Aggregator.
func NewAggregator() *Aggregator {
	return &Aggregator{byKey: make(map[string]int)}
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

// parseEntry returns a pointer so that callers can pass the entry around cheaply.
func parseEntry(line string) (*Entry, error) {
	fields := strings.Split(line, " ")
	if len(fields) != 4 {
		return nil, fmt.Errorf("%w: want 4 fields, got %d", ErrBadLine, len(fields))
	}
	status, err := strconv.Atoi(fields[2])
	if err != nil || status < 100 || status > 599 {
		return nil, fmt.Errorf("%w: status %q", ErrBadLine, fields[2])
	}
	n, err := strconv.ParseInt(fields[3], 10, 64)
	if err != nil || n < 0 {
		return nil, fmt.Errorf("%w: bytes %q", ErrBadLine, fields[3])
	}
	return &Entry{Method: fields[0], Path: fields[1], Status: status, Bytes: n}, nil
}

func (a *Aggregator) record(e *Entry) {
	a.stats.Total++
	a.stats.Bytes += e.Bytes
	if e.Bytes > a.stats.Max {
		a.stats.Max = e.Bytes
	}
	a.stats.Class[e.Status/100]++
	a.byKey[fmt.Sprintf("%s %d", e.Method, e.Status)]++
}

// Count returns how many accepted lines had this method and status.
func (a *Aggregator) Count(method string, status int) int {
	return a.byKey[fmt.Sprintf("%s %d", method, status)]
}

// Stats returns a snapshot of the aggregate.
func (a *Aggregator) Stats() Stats {
	return a.stats
}
