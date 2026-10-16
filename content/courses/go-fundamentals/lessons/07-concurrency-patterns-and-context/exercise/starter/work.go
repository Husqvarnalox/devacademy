package work

import (
	"context"
	"time"
)

// Pool применяет fn к каждому заданию не более чем в workers горутинах.
// results[i] соответствует jobs[i]. При первой ошибке остальные задания
// отменяются через контекст, а возвращается именно первая ошибка
// (workers <= 0 считается единицей). Если ошибок нет, но parent отменён,
// возвращается parent.Err().
func Pool(parent context.Context, workers int, jobs []int,
	fn func(context.Context, int) (int, error)) ([]int, error) {
	// TODO: сейчас задания выполняются последовательно и без отмены.
	// Нужны: context.WithCancel, канал индексов, workers горутин (sync.WaitGroup),
	// запись первой ошибки под sync.Mutex и cancel() после неё.
	results := make([]int, len(jobs))
	for i, j := range jobs {
		v, err := fn(parent, j)
		if err != nil {
			return nil, err
		}
		results[i] = v
	}
	return results, nil
}

// SafeCounter - счётчики по ключам, безопасные для параллельного использования.
// Нулевое значение должно быть готово к работе.
type SafeCounter struct {
	// TODO: добавьте sync.Mutex и карту
	m map[string]int
}

func (c *SafeCounter) Inc(key string) {
	// TODO: защитите доступ мьютексом; карта может быть nil
	if c.m == nil {
		c.m = make(map[string]int)
	}
	c.m[key]++
}

func (c *SafeCounter) Get(key string) int {
	// TODO
	return c.m[key]
}

// Snapshot возвращает копию всех счётчиков.
func (c *SafeCounter) Snapshot() map[string]int {
	// TODO
	return c.m
}

// RunWithTimeout запускает fn с контекстом, который истекает через d
// (или раньше, если отменён parent). Возвращает ошибку fn.
func RunWithTimeout(parent context.Context, d time.Duration, fn func(context.Context) error) error {
	// TODO: используйте context.WithTimeout и не забудьте defer cancel()
	return fn(parent)
}
