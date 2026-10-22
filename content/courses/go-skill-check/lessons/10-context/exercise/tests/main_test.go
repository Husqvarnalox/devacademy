package main

import (
	"context"
	"errors"
	"testing"
	"time"
)

func TestCollect(t *testing.T) {
	t.Run("closed_channel", func(t *testing.T) {
		src := make(chan int, 3)
		src <- 1
		src <- 2
		src <- 3
		close(src)
		got, err := collect(context.Background(), src)
		if err != nil || len(got) != 3 || got[0] != 1 || got[1] != 2 || got[2] != 3 {
			t.Fatalf("collect = %v, %v; ожидалось [1 2 3], nil", got, err)
		}
	})
	t.Run("empty_closed", func(t *testing.T) {
		src := make(chan int)
		close(src)
		got, err := collect(context.Background(), src)
		if err != nil || len(got) != 0 {
			t.Fatalf("collect = %v, %v; ожидалось пусто и nil", got, err)
		}
	})
	t.Run("deadline_returns_partial", func(t *testing.T) {
		src := make(chan int, 2)
		src <- 1
		src <- 2
		ctx, cancel := context.WithTimeout(context.Background(), 30*time.Millisecond)
		defer cancel()
		start := time.Now()
		got, err := collect(ctx, src)
		if !errors.Is(err, context.DeadlineExceeded) {
			t.Fatalf("err = %v, ожидалась context.DeadlineExceeded", err)
		}
		if len(got) != 2 || got[0] != 1 || got[1] != 2 {
			t.Fatalf("got = %v, ожидалось [1 2]: уже полученные значения нужно вернуть", got)
		}
		if time.Since(start) > 2*time.Second {
			t.Fatal("collect слишком долго ждала отмены")
		}
	})
	t.Run("cancel_while_waiting", func(t *testing.T) {
		src := make(chan int)
		ctx, cancel := context.WithCancel(context.Background())
		time.AfterFunc(20*time.Millisecond, cancel)
		done := make(chan error, 1)
		go func() {
			_, err := collect(ctx, src)
			done <- err
		}()
		select {
		case err := <-done:
			if !errors.Is(err, context.Canceled) {
				t.Fatalf("err = %v, ожидалась context.Canceled", err)
			}
		case <-time.After(2 * time.Second):
			t.Fatal("collect не завершилась после отмены контекста")
		}
	})
}

func TestWaitFor(t *testing.T) {
	t.Run("elapses", func(t *testing.T) {
		start := time.Now()
		if err := waitFor(context.Background(), 20*time.Millisecond); err != nil {
			t.Fatalf("err = %v, ожидалось nil", err)
		}
		if time.Since(start) < 15*time.Millisecond {
			t.Fatal("waitFor вернулась раньше, чем прошло время ожидания")
		}
	})
	t.Run("deadline", func(t *testing.T) {
		ctx, cancel := context.WithTimeout(context.Background(), 20*time.Millisecond)
		defer cancel()
		start := time.Now()
		err := waitFor(ctx, time.Hour)
		if !errors.Is(err, context.DeadlineExceeded) {
			t.Fatalf("err = %v, ожидалась context.DeadlineExceeded", err)
		}
		if time.Since(start) > 2*time.Second {
			t.Fatal("waitFor не прервалась по сроку контекста")
		}
	})
	t.Run("already_canceled", func(t *testing.T) {
		ctx, cancel := context.WithCancel(context.Background())
		cancel()
		if err := waitFor(ctx, time.Hour); !errors.Is(err, context.Canceled) {
			t.Fatalf("err = %v, ожидалась context.Canceled", err)
		}
	})
}
