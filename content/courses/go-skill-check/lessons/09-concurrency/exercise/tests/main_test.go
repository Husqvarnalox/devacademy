package main

import (
	"fmt"
	"testing"
	"time"
)

func run(t *testing.T, nums []int, workers int) []int {
	t.Helper()
	done := make(chan []int, 1)
	go func() { done <- squareAll(nums, workers) }()
	select {
	case got := <-done:
		return got
	case <-time.After(3 * time.Second):
		t.Fatalf("squareAll(%v, %d) не завершилась за 3 секунды: возможен deadlock или незакрытый канал", nums, workers)
		return nil
	}
}

func equal(a, b []int) bool {
	if len(a) != len(b) {
		return false
	}
	for i := range a {
		if a[i] != b[i] {
			return false
		}
	}
	return true
}

func TestSquareAll(t *testing.T) {
	cases := []struct {
		name    string
		nums    []int
		workers int
		want    []int
	}{
		{"basic", []int{3, 1, 2}, 2, []int{1, 4, 9}},
		{"single_worker", []int{5, 4}, 1, []int{16, 25}},
		{"negatives", []int{-3, 2, -1}, 3, []int{1, 4, 9}},
		{"duplicates", []int{2, 2, -2}, 2, []int{4, 4, 4}},
		{"more_workers_than_items", []int{7}, 8, []int{49}},
		{"zero_workers_means_one", []int{2, 1}, 0, []int{1, 4}},
		{"negative_workers_means_one", []int{2, 1}, -5, []int{1, 4}},
		{"empty", []int{}, 4, []int{}},
		{"nil_input", nil, 2, []int{}},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			got := run(t, c.nums, c.workers)
			if !equal(got, c.want) {
				t.Fatalf("squareAll(%v, %d) = %v, ожидалось %v", c.nums, c.workers, got, c.want)
			}
		})
	}
}

func TestSquareAllLarge(t *testing.T) {
	nums := make([]int, 500)
	want := make([]int, 500)
	for i := range nums {
		nums[i] = 499 - i
		want[i] = i * i
	}
	for _, workers := range []int{1, 4, 16} {
		t.Run(fmt.Sprintf("workers_%d", workers), func(t *testing.T) {
			got := run(t, nums, workers)
			if !equal(got, want) {
				t.Fatalf("squareAll(500 чисел, %d) вернула неверный результат", workers)
			}
		})
	}
}

func TestSquareAllDoesNotChangeInput(t *testing.T) {
	nums := []int{3, 1, 2}
	run(t, nums, 2)
	if !equal(nums, []int{3, 1, 2}) {
		t.Fatalf("входной срез изменён: %v", nums)
	}
}
