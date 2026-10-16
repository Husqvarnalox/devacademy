package first

import (
	"context"
	"errors"
	"sync"
)

var ErrNoTasks = errors.New("first: нет функций")

// First запускает все fns параллельно и возвращает результат первой
// успешной. Остальным отправляется отмена через контекст. Функция возвращается
// только после завершения всех запущенных горутин (утечек нет).
// Если все функции вернули ошибку, возвращается errors.Join всех ошибок
// в порядке индексов fns.
func First(parent context.Context, fns ...func(context.Context) (string, error)) (string, error) {
	if len(fns) == 0 {
		return "", ErrNoTasks
	}
	ctx, cancel := context.WithCancel(parent)
	defer cancel()

	type result struct {
		i   int
		val string
		err error
	}
	results := make(chan result, len(fns)) // буфер: отправитель никогда не блокируется
	var wg sync.WaitGroup
	for i, fn := range fns {
		wg.Go(func() {
			v, err := fn(ctx)
			results <- result{i, v, err}
		})
	}

	errs := make([]error, len(fns))
	winner, won := "", false
	for range fns {
		r := <-results
		switch {
		case r.err != nil:
			errs[r.i] = r.err
		case !won:
			winner, won = r.val, true
			cancel() // победитель найден: просим остальных остановиться
		}
	}
	wg.Wait()
	if won {
		return winner, nil
	}
	return "", errors.Join(errs...)
}
