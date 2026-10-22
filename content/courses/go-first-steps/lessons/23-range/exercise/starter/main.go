package main

import "fmt"

func main() {
	var n int
	fmt.Scan(&n)
	var nums []int
	for i := 0; i < n; i++ {
		var x int
		fmt.Scan(&x)
		nums = append(nums, x)
	}
	// Найдите среднее и посчитайте, сколько чисел строго больше него (используйте range)
	fmt.Println(0)
}
