package main

import (
	"fmt"
	"sort"
	"sync"
)

// squareAll возводит числа в квадрат в пуле из workers горутин
// и возвращает квадраты по возрастанию.
func squareAll(nums []int, workers int) []int {
	if workers <= 0 {
		workers = 1
	}
	jobs := make(chan int)
	results := make(chan int)

	var wg sync.WaitGroup
	for i := 0; i < workers; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			for n := range jobs {
				results <- n * n
			}
		}()
	}

	go func() {
		for _, n := range nums {
			jobs <- n
		}
		close(jobs)
	}()

	go func() {
		wg.Wait()
		close(results)
	}()

	out := make([]int, 0, len(nums))
	for r := range results {
		out = append(out, r)
	}
	sort.Ints(out)
	return out
}

func main() {
	fmt.Println(squareAll([]int{3, 1, 2}, 2))
}
