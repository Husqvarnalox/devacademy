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
	sum := 0
	for _, v := range nums {
		sum += v
	}
	avg := float64(sum) / float64(len(nums))
	count := 0
	for _, v := range nums {
		if float64(v) > avg {
			count++
		}
	}
	fmt.Println(count)
}
