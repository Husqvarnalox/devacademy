package main

import (
	"context"
	"fmt"
	"time"
)

// collect читает значения из src до закрытия канала или отмены ctx.
// При отмене возвращает уже собранные значения и ctx.Err().
func collect(ctx context.Context, src <-chan int) ([]int, error) {
	var out []int
	for {
		select {
		case <-ctx.Done():
			return out, ctx.Err()
		case v, ok := <-src:
			if !ok {
				return out, nil
			}
			out = append(out, v)
		}
	}
}

// waitFor ждёт d или отмены ctx.
func waitFor(ctx context.Context, d time.Duration) error {
	timer := time.NewTimer(d)
	defer timer.Stop()
	select {
	case <-timer.C:
		return nil
	case <-ctx.Done():
		return ctx.Err()
	}
}

func main() {
	ctx, cancel := context.WithTimeout(context.Background(), 20*time.Millisecond)
	defer cancel()
	fmt.Println(waitFor(ctx, time.Hour))
}
