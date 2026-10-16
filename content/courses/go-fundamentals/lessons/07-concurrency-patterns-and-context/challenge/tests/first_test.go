package first

import (
	"context"
	"errors"
	"sync/atomic"
	"testing"
	"time"
)

const guard = 3 * time.Second

var (
	errA = errors.New("a failed")
	errB = errors.New("b failed")
)

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
		t.Fatalf("не завершилось за %v", guard)
	}
}

func ok(s string) func(context.Context) (string, error) {
	return func(context.Context) (string, error) { return s, nil }
}

func fail(err error) func(context.Context) (string, error) {
	return func(context.Context) (string, error) { return "", err }
}

func TestFirst(t *testing.T) {
	t.Run("winner-cancels-loser", func(t *testing.T) {
		within(t, func() {
			var cancelled atomic.Bool
			loser := func(ctx context.Context) (string, error) {
				<-ctx.Done() // блокируется, пока не придёт отмена
				cancelled.Store(true)
				return "", ctx.Err()
			}
			got, err := First(context.Background(), loser, ok("fast"))
			if err != nil || got != "fast" {
				t.Errorf("got %q, %v", got, err)
			}
			// First вернулась только после завершения всех горутин,
			// поэтому проигравшая функция уже увидела отмену.
			if !cancelled.Load() {
				t.Error("проигравшая функция не получила отмену до возврата First")
			}
		})
	})
	t.Run("skips-failures", func(t *testing.T) {
		within(t, func() {
			got, err := First(context.Background(), fail(errA), fail(errB), ok("third"))
			if err != nil || got != "third" {
				t.Errorf("got %q, %v", got, err)
			}
		})
	})
	t.Run("all-fail-joined", func(t *testing.T) {
		within(t, func() {
			_, err := First(context.Background(), fail(errA), fail(errB))
			if !errors.Is(err, errA) || !errors.Is(err, errB) {
				t.Errorf("err = %v, ожидались обе ошибки", err)
			}
		})
	})
	t.Run("no-functions", func(t *testing.T) {
		_, err := First(context.Background())
		if !errors.Is(err, ErrNoTasks) {
			t.Errorf("err = %v, want ErrNoTasks", err)
		}
	})
	t.Run("parent-cancelled", func(t *testing.T) {
		within(t, func() {
			ctx, cancel := context.WithCancel(context.Background())
			cancel()
			wait := func(ctx context.Context) (string, error) {
				<-ctx.Done()
				return "", ctx.Err()
			}
			_, err := First(ctx, wait, wait)
			if !errors.Is(err, context.Canceled) {
				t.Errorf("err = %v, want context.Canceled", err)
			}
		})
	})
	t.Run("many-parallel", func(t *testing.T) {
		within(t, func() {
			// 20 функций блокируются, пока не придёт отмена; одна возвращает результат.
			var fns []func(context.Context) (string, error)
			for i := 0; i < 20; i++ {
				fns = append(fns, func(ctx context.Context) (string, error) {
					<-ctx.Done()
					return "", ctx.Err()
				})
			}
			fns = append(fns, ok("only"))
			got, err := First(context.Background(), fns...)
			if err != nil || got != "only" {
				t.Errorf("got %q, %v", got, err)
			}
		})
	})
}
