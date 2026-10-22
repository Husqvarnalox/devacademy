package metrics

import (
	"sync"
	"testing"
)

// runConcurrently starts n goroutines, releases them at the same moment and
// waits for all of them.
func runConcurrently(n int, f func(id int)) {
	start := make(chan struct{})
	var wg sync.WaitGroup
	for i := 0; i < n; i++ {
		wg.Add(1)
		go func(id int) {
			defer wg.Done()
			<-start
			f(id)
		}(i)
	}
	close(start)
	wg.Wait()
}

func TestCounterBasic(t *testing.T) {
	var c Counter
	if c.Value() != 0 {
		t.Fatalf("zero value = %d", c.Value())
	}
	c.Add(5)
	c.Add(-2)
	if c.Value() != 3 {
		t.Fatalf("Value = %d, want 3", c.Value())
	}
	if got := c.Reset(); got != 3 || c.Value() != 0 {
		t.Fatalf("Reset = %d, Value after = %d; want 3 and 0", got, c.Value())
	}
}

func TestCounterConcurrentAdd(t *testing.T) {
	var c Counter
	const workers, perWorker = 32, 200
	runConcurrently(workers, func(int) {
		for i := 0; i < perWorker; i++ {
			c.Add(1)
		}
	})
	if got, want := c.Value(), int64(workers*perWorker); got != want {
		t.Fatalf("Value = %d, want %d (lost %d updates)", got, want, want-got)
	}
}

func TestCounterResetLosesNothing(t *testing.T) {
	var c Counter
	const writers, perWriter = 16, 200
	var wg sync.WaitGroup
	var reaped int64
	stop := make(chan struct{})
	wg.Add(1)
	go func() { // single reader: sums everything Reset returns
		defer wg.Done()
		for {
			reaped += c.Reset()
			select {
			case <-stop:
				return
			default:
			}
		}
	}()
	runConcurrently(writers, func(int) {
		for i := 0; i < perWriter; i++ {
			c.Add(1)
		}
	})
	close(stop)
	wg.Wait()
	total := reaped + c.Value()
	if want := int64(writers * perWriter); total != want {
		t.Fatalf("returned by Reset (%d) + final Value (%d) = %d, want %d", reaped, c.Value(), total, want)
	}
}

func TestRegistryConcurrentInc(t *testing.T) {
	r := NewRegistry()
	names := []string{"requests", "errors", "cache_hits", "cache_misses"}
	const workers, rounds = 16, 50
	runConcurrently(workers, func(int) {
		for i := 0; i < rounds; i++ {
			for _, n := range names {
				r.Inc(n)
			}
		}
	})
	for _, n := range names {
		if got, want := r.Get(n), int64(workers*rounds); got != want {
			t.Errorf("%s = %d, want %d", n, got, want)
		}
	}
}

func TestRegistrySameCounterForSameName(t *testing.T) {
	r := NewRegistry()
	got := make([]*Counter, 24)
	runConcurrently(len(got), func(id int) { got[id] = r.counter("only") })
	for i, c := range got {
		if c != got[0] {
			t.Fatalf("goroutine %d got a different *Counter for the same name", i)
		}
	}
}

func TestRegistryGetDoesNotCreate(t *testing.T) {
	r := NewRegistry()
	if r.Get("missing") != 0 {
		t.Fatal("Get of a missing name must be 0")
	}
	if len(r.Snapshot()) != 0 {
		t.Fatal("Get must not create counters")
	}
}

func TestSnapshotIsACopy(t *testing.T) {
	r := NewRegistry()
	r.Add("a", 2)
	snap := r.Snapshot()
	r.Add("a", 3)
	r.Inc("b")
	if snap["a"] != 2 || len(snap) != 1 {
		t.Fatalf("snapshot changed after later updates: %v", snap)
	}
	snap["a"] = 100
	snap["zzz"] = 1
	if r.Get("a") != 5 || r.Get("zzz") != 0 {
		t.Fatalf("modifying the snapshot affected the registry: a=%d zzz=%d", r.Get("a"), r.Get("zzz"))
	}
}

func TestSnapshotWhileUpdating(t *testing.T) {
	r := NewRegistry()
	const workers, rounds = 8, 100
	var wg sync.WaitGroup
	stop := make(chan struct{})
	wg.Add(1)
	go func() {
		defer wg.Done()
		for {
			s := r.Snapshot()
			for _, v := range s {
				if v < 0 {
					t.Errorf("negative value in snapshot")
				}
			}
			select {
			case <-stop:
				return
			default:
			}
		}
	}()
	runConcurrently(workers, func(id int) {
		for i := 0; i < rounds; i++ {
			r.Inc("k" + string(rune('a'+(i%10))))
		}
	})
	close(stop)
	wg.Wait()
	var sum int64
	for _, v := range r.Snapshot() {
		sum += v
	}
	if sum != workers*rounds {
		t.Fatalf("sum = %d, want %d", sum, workers*rounds)
	}
}
