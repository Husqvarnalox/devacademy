package main

import (
	"context"
	"errors"
	"fmt"
	"time"
)

// collect читает значения из src до закрытия канала или отмены ctx.
// Канал закрыт: верните все значения и nil.
// Контекст отменён: верните уже собранные значения и ctx.Err().
// Подсказка: select с ветками <-ctx.Done() и v, ok := <-src.
func collect(ctx context.Context, src <-chan int) ([]int, error) {
	return nil, errors.New("collect не написана")
}

// waitFor ждёт d (вернёт nil) или отмены ctx (вернёт ctx.Err()),
// смотря что наступит раньше. Не используйте time.Sleep.
func waitFor(ctx context.Context, d time.Duration) error {
	return errors.New("waitFor не написана")
}

func main() {
	ctx, cancel := context.WithTimeout(context.Background(), 20*time.Millisecond)
	defer cancel()
	fmt.Println(waitFor(ctx, time.Hour))
}
