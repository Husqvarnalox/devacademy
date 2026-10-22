package accesslog

import (
	"errors"
	"fmt"
	"math/rand"
	"strconv"
	"strings"
	"testing"
)

var goodLines = []string{
	"GET /index.html 200 5123",
	"POST /api/orders 201 88",
	"GET /missing 404 0",
	"DELETE /api/orders/17 500 12",
}

func TestAddCounts(t *testing.T) {
	a := NewAggregator()
	for _, l := range goodLines {
		if err := a.Add(l); err != nil {
			t.Fatalf("Add(%q) = %v", l, err)
		}
	}
	_ = a.Add("GET /index.html 200 100")
	s := a.Stats()
	if s.Total != 5 || s.Rejected != 0 || s.Bytes != 5123+88+0+12+100 || s.Max != 5123 {
		t.Fatalf("stats = %+v", s)
	}
	if s.Class != [6]int{0, 0, 3, 0, 1, 1} {
		t.Fatalf("Class = %v, want [0 0 3 0 1 1]", s.Class)
	}
	if a.Count("GET", 200) != 2 || a.Count("POST", 201) != 1 || a.Count("GET", 500) != 0 {
		t.Fatalf("Count mismatch: GET 200=%d POST 201=%d GET 500=%d", a.Count("GET", 200), a.Count("POST", 201), a.Count("GET", 500))
	}
}

func TestAddRejectsBadLines(t *testing.T) {
	bad := []string{
		"",
		"GET /a 200",
		"GET /a 200 5 extra",
		"GET  /a 200 5",
		"GET /a abc 5",
		"GET /a 99 5",
		"GET /a 600 5",
		"GET /a 200 -1",
		"GET /a 200 x",
		"GET /a 200 5 ",
	}
	a := NewAggregator()
	for _, l := range bad {
		err := a.Add(l)
		if !errors.Is(err, ErrBadLine) {
			t.Errorf("Add(%q) = %v, want an error wrapping ErrBadLine", l, err)
		}
	}
	s := a.Stats()
	if s.Total != 0 || s.Bytes != 0 || s.Rejected != len(bad) {
		t.Fatalf("a rejected line must only count as rejected: %+v", s)
	}
}

// reference is a simple, allocation-happy model of the specification.
type reference struct {
	total, rejected int
	bytes, max      int64
	class           [6]int
	byKey           map[string]int
}

func (r *reference) add(line string) {
	f := strings.Split(line, " ")
	if len(f) != 4 {
		r.rejected++
		return
	}
	st, err := strconv.Atoi(f[2])
	if err != nil || st < 100 || st > 599 {
		r.rejected++
		return
	}
	n, err := strconv.ParseInt(f[3], 10, 64)
	if err != nil || n < 0 {
		r.rejected++
		return
	}
	r.total++
	r.bytes += n
	if n > r.max {
		r.max = n
	}
	r.class[st/100]++
	r.byKey[fmt.Sprintf("%s %d", f[0], st)]++
}

func TestAddMatchesReference(t *testing.T) {
	rng := rand.New(rand.NewSource(7))
	methods := []string{"GET", "POST", "PUT", "DELETE", "PATCH"}
	statuses := []string{"200", "201", "301", "404", "500", "99", "700", "2x0", ""}
	sizes := []string{"0", "17", "4096", "9999999999", "-3", "1e3", ""}
	a := NewAggregator()
	ref := &reference{byKey: map[string]int{}}
	for i := 0; i < 2000; i++ {
		line := fmt.Sprintf("%s /p/%d %s %s", methods[rng.Intn(len(methods))], rng.Intn(50),
			statuses[rng.Intn(len(statuses))], sizes[rng.Intn(len(sizes))])
		if rng.Intn(20) == 0 {
			line += " junk"
		}
		_ = a.Add(line)
		ref.add(line)
	}
	s := a.Stats()
	if s.Total != ref.total || s.Rejected != ref.rejected || s.Bytes != ref.bytes || s.Max != ref.max || s.Class != ref.class {
		t.Fatalf("stats %+v differ from reference total=%d rejected=%d bytes=%d max=%d class=%v",
			s, ref.total, ref.rejected, ref.bytes, ref.max, ref.class)
	}
	for k, want := range ref.byKey {
		parts := strings.Split(k, " ")
		st, _ := strconv.Atoi(parts[1])
		if got := a.Count(parts[0], st); got != want {
			t.Fatalf("Count(%s, %d) = %d, want %d", parts[0], st, got, want)
		}
	}
}

func TestAddAllocatesNothing(t *testing.T) {
	a := NewAggregator()
	for _, l := range goodLines { // warm up: the keys of the map must exist already
		if err := a.Add(l); err != nil {
			t.Fatal(err)
		}
	}
	allocs := testing.AllocsPerRun(200, func() {
		for _, l := range goodLines {
			_ = a.Add(l)
		}
	})
	if allocs != 0 {
		t.Fatalf("Add allocates %.0f times per 4 valid lines in steady state, want 0", allocs)
	}
}

func TestCountAllocatesNothing(t *testing.T) {
	a := NewAggregator()
	_ = a.Add("GET /x 200 1")
	var sink int
	allocs := testing.AllocsPerRun(200, func() {
		sink += a.Count("GET", 200)
	})
	_ = sink
	if allocs != 0 {
		t.Fatalf("Count allocates %.0f times per call, want 0", allocs)
	}
}
