package first

import (
	"context"
	"errors"
)

var ErrNoTasks = errors.New("first: нет функций")

// First запускает все fns параллельно и возвращает результат первой
// успешной. Остальным отправляется отмена через контекст. Функция возвращается
// только после завершения всех запущенных горутин (утечек нет).
// Если все функции вернули ошибку, возвращается errors.Join всех ошибок
// в порядке индексов fns; без функций - ErrNoTasks.
func First(parent context.Context, fns ...func(context.Context) (string, error)) (string, error) {
	// TODO: сейчас функции вызываются по очереди, а отмена не используется.
	// Нужны: context.WithCancel, канал результатов с буфером len(fns),
	// sync.WaitGroup и cancel() сразу после первого успеха.
	for _, fn := range fns {
		if v, err := fn(parent); err == nil {
			return v, nil
		}
	}
	return "", ErrNoTasks
}
