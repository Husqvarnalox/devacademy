package pipe

import "sync"

// Generate отправляет nums в возвращаемый канал и закрывает его.
func Generate(nums ...int) <-chan int {
	out := make(chan int)
	go func() {
		defer close(out) // закрывает отправитель, и только он
		for _, n := range nums {
			out <- n
		}
	}()
	return out
}

// Square читает in, отправляет квадраты и закрывает выход, когда in закрыт.
func Square(in <-chan int) <-chan int {
	out := make(chan int)
	go func() {
		defer close(out)
		for n := range in {
			out <- n * n
		}
	}()
	return out
}

// Merge объединяет каналы (fan-in). Выходной канал закрывается, когда
// закрыты и полностью прочитаны все входные.
func Merge(cs ...<-chan int) <-chan int {
	out := make(chan int)
	var wg sync.WaitGroup
	for _, c := range cs {
		wg.Go(func() {
			for n := range c {
				out <- n
			}
		})
	}
	go func() {
		wg.Wait()
		close(out)
	}()
	return out
}

// Collect читает канал до закрытия и возвращает значения в порядке получения.
func Collect(ch <-chan int) []int {
	var out []int
	for n := range ch {
		out = append(out, n)
	}
	return out
}

// SumConcurrent делит nums на parts примерно равных частей и суммирует каждую
// в своей горутине. parts <= 0 считается единицей.
func SumConcurrent(nums []int, parts int) int {
	if parts <= 0 {
		parts = 1
	}
	if parts > len(nums) {
		parts = max(len(nums), 1)
	}
	sums := make([]int, parts) // каждая горутина пишет в свой элемент
	size := (len(nums) + parts - 1) / parts
	var wg sync.WaitGroup
	for i := range parts {
		lo := min(i*size, len(nums))
		hi := min(lo+size, len(nums))
		wg.Go(func() {
			for _, n := range nums[lo:hi] {
				sums[i] += n
			}
		})
	}
	wg.Wait()
	total := 0
	for _, s := range sums {
		total += s
	}
	return total
}
