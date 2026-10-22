package main

import "fmt"

// squareAll возводит числа в квадрат с помощью пула из workers горутин
// и возвращает квадраты по возрастанию. Если workers <= 0, используйте одну горутину.
// Схема: канал заданий, workers горутин-обработчиков, WaitGroup и горутина,
// которая закрывает канал результатов, когда все обработчики закончили.
func squareAll(nums []int, workers int) []int {
	return nil
}

func main() {
	fmt.Println(squareAll([]int{3, 1, 2}, 2))
}
