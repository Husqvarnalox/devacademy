package work

import (
	"context"
	"errors"
	"runtime"
	"slices"
	"sync"
	"sync/atomic"
	"testing"
	"time"
)

// guard - только защита от зависания при ошибке в решении, а не средство
// синхронизации: корректные решения укладываются в него с огромным запасом.
const guard = 3 * time.Second

var errBoom = errors.New("boom")

// within запускает f и падает, если она не завершилась за guard.
func within(t *testing.T, f func()) {
	t.Helper()
	done := make(chan struct{})
	go func() {
		defer close(done)
		f()
	}()
	select {
	case <-done:
	case <-time.After(guard):
		t.Fatalf("не завершилось за %v (взаимная блокировка?)", guard)
	}
}

func TestPool(t *testing.T) {
	double := func(_ context.Context, n int) (int, error) { return n * 2, nil }

	t.Run("results-in-order", func(t *testing.T) {
		within(t, func() {
			jobs := []int{5, 1, 4, 2, 3, 9, 8, 7}
			got, err := Pool(context.Background(), 3, jobs, double)
			if err != nil || !slices.Equal(got, []int{10, 2, 8, 4, 6, 18, 16, 14}) {
				t.Errorf("got %v, %v", got, err)
			}
		})
	})
	t.Run("empty", func(t *testing.T) {
		within(t, func() {
			got, err := Pool(context.Background(), 4, nil, double)
			if err != nil || len(got) != 0 {
				t.Errorf("got %v, %v", got, err)
			}
		})
	})
	t.Run("workers-nonpositive", func(t *testing.T) {
		within(t, func() {
			got, err := Pool(context.Background(), 0, []int{1, 2, 3}, double)
			if err != nil || !slices.Equal(got, []int{2, 4, 6}) {
				t.Errorf("got %v, %v", got, err)
			}
		})
	})
	t.Run("concurrency-limit", func(t *testing.T) {
		within(t, func() {
			var cur, peak atomic.Int32
			fn := func(_ context.Context, n int) (int, error) {
				c := cur.Add(1)
				for {
					p := peak.Load()
					if c <= p || peak.CompareAndSwap(p, c) {
						break
					}
				}
				for i := 0; i < 50; i++ { // дать горутинам шанс пересечься
					runtime.Gosched()
				}
				cur.Add(-1)
				return n, nil
			}
			jobs := make([]int, 40)
			if _, err := Pool(context.Background(), 4, jobs, fn); err != nil {
				t.Errorf("err = %v", err)
			}
			if p := peak.Load(); p > 4 {
				t.Errorf("одновременно работало %d заданий, лимит 4", p)
			}
		})
	})
	t.Run("really-concurrent", func(t *testing.T) {
		// Три задания ждут друг друга у барьера: если Pool выполняет их
		// по одному, барьер не откроется (спасает guard внутри within).
		within(t, func() {
			var wg sync.WaitGroup
			wg.Add(3)
			fn := func(ctx context.Context, n int) (int, error) {
				wg.Done()
				wg.Wait()
				return n, nil
			}
			got, err := Pool(context.Background(), 3, []int{1, 2, 3}, fn)
			if err != nil || !slices.Equal(got, []int{1, 2, 3}) {
				t.Errorf("got %v, %v", got, err)
			}
		})
	})
	t.Run("first-error-cancels-others", func(t *testing.T) {
		within(t, func() {
			fn := func(ctx context.Context, n int) (int, error) {
				if n == 3 {
					return 0, errBoom
				}
				<-ctx.Done() // остальные ждут отмены
				return 0, ctx.Err()
			}
			got, err := Pool(context.Background(), 4, []int{0, 1, 2, 3, 4, 5, 6, 7}, fn)
			if !errors.Is(err, errBoom) {
				t.Errorf("err = %v, want boom (первая ошибка, а не context.Canceled)", err)
			}
			if got != nil {
				t.Errorf("при ошибке результаты должны быть nil, got %v", got)
			}
		})
	})
	t.Run("parent-cancelled", func(t *testing.T) {
		within(t, func() {
			ctx, cancel := context.WithCancel(context.Background())
			cancel()
			_, err := Pool(ctx, 2, []int{1, 2, 3}, double)
			if !errors.Is(err, context.Canceled) {
				t.Errorf("err = %v, want context.Canceled", err)
			}
		})
	})
}

func TestSafeCounter(t *testing.T) {
	t.Run("zero-value-usable", func(t *testing.T) {
		var c SafeCounter
		c.Inc("a")
		c.Inc("a")
		c.Inc("b")
		if c.Get("a") != 2 || c.Get("b") != 1 || c.Get("zzz") != 0 {
			t.Fatalf("snapshot = %v", c.Snapshot())
		}
	})
	t.Run("parallel-increments", func(t *testing.T) {
		within(t, func() {
			var c SafeCounter
			var wg sync.WaitGroup
			for g := 0; g < 50; g++ {
				wg.Go(func() {
					for i := 0; i < 1000; i++ {
						c.Inc("hits")
						c.Inc("g")
					}
				})
			}
			wg.Wait()
			if c.Get("hits") != 50000 || c.Get("g") != 50000 {
				t.Errorf("hits=%d g=%d, want 50000", c.Get("hits"), c.Get("g"))
			}
		})
	})
	t.Run("snapshot-is-copy", func(t *testing.T) {
		var c SafeCounter
		c.Inc("a")
		s := c.Snapshot()
		s["a"] = 100
		s["new"] = 1
		if c.Get("a") != 1 || c.Get("new") != 0 {
			t.Fatalf("Snapshot отдаёт внутреннюю карту: %v", c.Snapshot())
		}
	})
	t.Run("snapshot-empty", func(t *testing.T) {
		var c SafeCounter
		if s := c.Snapshot(); s == nil || len(s) != 0 {
			t.Fatalf("snapshot = %#v, want пустая не-nil карта", s)
		}
	})
}

func TestRunWithTimeout(t *testing.T) {
	waitCtx := func(ctx context.Context) error {
		<-ctx.Done()
		return ctx.Err()
	}
	t.Run("deadline", func(t *testing.T) {
		within(t, func() {
			err := RunWithTimeout(context.Background(), 20*time.Millisecond, waitCtx)
			if !errors.Is(err, context.DeadlineExceeded) {
				t.Errorf("err = %v, want DeadlineExceeded", err)
			}
		})
	})
	t.Run("fn-returns-first", func(t *testing.T) {
		within(t, func() {
			err := RunWithTimeout(context.Background(), time.Hour, func(ctx context.Context) error {
				if _, ok := ctx.Deadline(); !ok {
					return errors.New("у контекста нет срока")
				}
				return errBoom
			})
			if !errors.Is(err, errBoom) {
				t.Errorf("err = %v", err)
			}
		})
	})
	t.Run("parent-cancel", func(t *testing.T) {
		within(t, func() {
			parent, cancel := context.WithCancel(context.Background())
			cancel()
			err := RunWithTimeout(parent, time.Hour, waitCtx)
			if !errors.Is(err, context.Canceled) {
				t.Errorf("err = %v, want Canceled", err)
			}
		})
	})
}
