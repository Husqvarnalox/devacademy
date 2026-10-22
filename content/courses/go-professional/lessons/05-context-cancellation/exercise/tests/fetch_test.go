package fetch

import (
	"context"
	"errors"
	"fmt"
	"runtime"
	"strings"
	"sync/atomic"
	"testing"
	"time"
)

// settle waits until the number of goroutines drops back to base. Goroutines
// finish asynchronously after FetchAll returns (the runtime may need a moment
// to retire them), so we poll for up to 3 seconds instead of checking once.
func settle(t *testing.T, base int) {
	t.Helper()
	deadline := time.Now().Add(3 * time.Second)
	for {
		runtime.GC()
		n := runtime.NumGoroutine()
		if n <= base {
			return
		}
		if time.Now().After(deadline) {
			t.Fatalf("goroutine leak: %d goroutines running, expected at most %d", n, base)
		}
		time.Sleep(10 * time.Millisecond)
	}
}

func baseline() int {
	time.Sleep(20 * time.Millisecond)
	return runtime.NumGoroutine()
}

type outcome struct {
	res []Result
	err error
}

// call runs FetchAll and fails the test if it does not return within 5 seconds.
func call(t *testing.T, ctx context.Context, urls []string, workers int, f Fetcher) outcome {
	t.Helper()
	done := make(chan outcome, 1)
	go func() {
		r, err := FetchAll(ctx, urls, workers, f)
		done <- outcome{r, err}
	}()
	select {
	case o := <-done:
		return o
	case <-time.After(5 * time.Second):
		t.Fatal("FetchAll did not return within 5 seconds (it must stop promptly on cancellation or failure)")
		return outcome{}
	}
}

func blocking(ctx context.Context, _ string) (string, error) {
	<-ctx.Done()
	return "", ctx.Err()
}

func urlsN(n int) []string {
	u := make([]string, n)
	for i := range u {
		u[i] = fmt.Sprintf("http://host/%d", i)
	}
	return u
}

func TestFetchAllOrderAndLimit(t *testing.T) {
	base := baseline()
	var cur, peak atomic.Int32
	f := func(ctx context.Context, u string) (string, error) {
		n := cur.Add(1)
		for {
			p := peak.Load()
			if n <= p || peak.CompareAndSwap(p, n) {
				break
			}
		}
		time.Sleep(10 * time.Millisecond)
		cur.Add(-1)
		return "body:" + u, nil
	}
	urls := urlsN(12)
	o := call(t, context.Background(), urls, 3, f)
	if o.err != nil {
		t.Fatalf("unexpected error: %v", o.err)
	}
	if len(o.res) != len(urls) {
		t.Fatalf("got %d results, want %d", len(o.res), len(urls))
	}
	for i, r := range o.res {
		if r.URL != urls[i] || r.Body != "body:"+urls[i] {
			t.Fatalf("result %d = %+v, results must follow the order of urls", i, r)
		}
	}
	if p := peak.Load(); p > 3 {
		t.Fatalf("up to %d fetches ran at once, limit is 3 workers", p)
	}
	settle(t, base)
}

func TestFetchAllEmpty(t *testing.T) {
	o := call(t, context.Background(), nil, 4, blocking)
	if o.err != nil || len(o.res) != 0 {
		t.Fatalf("got %v, %v; want empty result and nil error", o.res, o.err)
	}
}

func TestFetchAllFirstErrorCancelsOthers(t *testing.T) {
	base := baseline()
	ctx, cancel := context.WithCancel(context.Background())
	t.Cleanup(cancel) // releases blocked fetchers of a faulty implementation after the check
	boom := errors.New("boom")
	f := func(ctx context.Context, u string) (string, error) {
		if strings.HasSuffix(u, "/bad") {
			time.Sleep(20 * time.Millisecond) // let the others start waiting
			return "", boom
		}
		return blocking(ctx, u)
	}
	urls := []string{"http://h/a", "http://h/b", "http://h/bad", "http://h/c", "http://h/d", "http://h/e"}
	o := call(t, ctx, urls, 4, f)
	if !errors.Is(o.err, boom) {
		t.Fatalf("err = %v, want it to wrap boom", o.err)
	}
	if !strings.Contains(o.err.Error(), "/bad") {
		t.Fatalf("error %q must name the failing URL", o.err)
	}
	if o.res != nil {
		t.Fatalf("results must be nil on error, got %v", o.res)
	}
	settle(t, base) // the blocked fetchers must have been cancelled by FetchAll itself
}

func TestFetchAllNoNewFetchesAfterError(t *testing.T) {
	var calls atomic.Int32
	boom := errors.New("boom")
	f := func(ctx context.Context, u string) (string, error) {
		calls.Add(1)
		return "", boom
	}
	o := call(t, context.Background(), urlsN(10), 1, f)
	if !errors.Is(o.err, boom) {
		t.Fatalf("err = %v", o.err)
	}
	if c := calls.Load(); c != 1 {
		t.Fatalf("fetch was called %d times, want 1: no new fetches after a failure", c)
	}
}

func TestFetchAllParentCancel(t *testing.T) {
	base := baseline()
	ctx, cancel := context.WithCancel(context.Background())
	t.Cleanup(cancel)
	go func() {
		time.Sleep(50 * time.Millisecond)
		cancel()
	}()
	start := time.Now()
	o := call(t, ctx, urlsN(9), 3, blocking)
	if !errors.Is(o.err, context.Canceled) {
		t.Fatalf("err = %v, want it to wrap context.Canceled", o.err)
	}
	if d := time.Since(start); d > 2*time.Second {
		t.Fatalf("took %v to react to cancellation", d)
	}
	settle(t, base)
}

func TestFetchAllDeadline(t *testing.T) {
	base := baseline()
	ctx, cancel := context.WithTimeout(context.Background(), 60*time.Millisecond)
	defer cancel()
	o := call(t, ctx, urlsN(6), 2, blocking)
	if !errors.Is(o.err, context.DeadlineExceeded) {
		t.Fatalf("err = %v, want it to wrap context.DeadlineExceeded", o.err)
	}
	settle(t, base)
}

func TestFetchAllAlreadyCancelled(t *testing.T) {
	base := baseline()
	ctx, cancel := context.WithCancel(context.Background())
	cancel()
	var calls atomic.Int32
	f := func(ctx context.Context, u string) (string, error) {
		calls.Add(1)
		return "x", nil
	}
	o := call(t, ctx, urlsN(5), 3, f)
	if !errors.Is(o.err, context.Canceled) {
		t.Fatalf("err = %v, want it to wrap context.Canceled", o.err)
	}
	if c := calls.Load(); c != 0 {
		t.Fatalf("fetch was called %d times with an already cancelled context, want 0", c)
	}
	settle(t, base)
}
