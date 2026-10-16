package work

import (
	"context"
	"sync"
	"time"
)

// Pool применяет fn к каждому заданию не более чем в workers горутинах.
// results[i] соответствует jobs[i]. При первой ошибке остальные задания
// отменяются через контекст, а возвращается именно первая ошибка.
// workers <= 0 считается единицей.
func Pool(parent context.Context, workers int, jobs []int,
	fn func(context.Context, int) (int, error)) ([]int, error) {
	if workers <= 0 {
		workers = 1
	}
	ctx, cancel := context.WithCancel(parent)
	defer cancel()

	results := make([]int, len(jobs))
	indexes := make(chan int)

	var (
		mu       sync.Mutex
		firstErr error
	)
	fail := func(err error) {
		mu.Lock()
		if firstErr == nil {
			firstErr = err
		}
		mu.Unlock()
		cancel() // сначала запомнили первую ошибку, потом отменили остальных
	}

	var wg sync.WaitGroup
	for range workers {
		wg.Go(func() {
			for i := range indexes {
				if ctx.Err() != nil {
					continue // отмена: оставшиеся задания пропускаем
				}
				v, err := fn(ctx, jobs[i])
				if err != nil {
					fail(err)
					continue
				}
				results[i] = v // у каждого задания свой элемент: гонки нет
			}
		})
	}

feed:
	for i := range jobs {
		select {
		case indexes <- i:
		case <-ctx.Done():
			break feed
		}
	}
	close(indexes)
	wg.Wait()

	mu.Lock()
	defer mu.Unlock()
	if firstErr != nil {
		return nil, firstErr
	}
	if err := parent.Err(); err != nil {
		return nil, err
	}
	return results, nil
}

// SafeCounter - счётчики по ключам, безопасные для параллельного использования.
// Нулевое значение готово к работе.
type SafeCounter struct {
	mu sync.Mutex
	m  map[string]int
}

func (c *SafeCounter) Inc(key string) {
	c.mu.Lock()
	defer c.mu.Unlock()
	if c.m == nil {
		c.m = make(map[string]int)
	}
	c.m[key]++
}

func (c *SafeCounter) Get(key string) int {
	c.mu.Lock()
	defer c.mu.Unlock()
	return c.m[key]
}

// Snapshot возвращает копию всех счётчиков.
func (c *SafeCounter) Snapshot() map[string]int {
	c.mu.Lock()
	defer c.mu.Unlock()
	out := make(map[string]int, len(c.m))
	for k, v := range c.m {
		out[k] = v
	}
	return out
}

// RunWithTimeout запускает fn с контекстом, который истекает через d
// (или раньше, если отменён parent). Возвращает ошибку fn.
func RunWithTimeout(parent context.Context, d time.Duration, fn func(context.Context) error) error {
	ctx, cancel := context.WithTimeout(parent, d)
	defer cancel() // освобождает таймер, даже если fn вернулась раньше
	return fn(ctx)
}
