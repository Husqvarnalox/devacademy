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
	for i := len(nums) - 1; i >= 0; i-- {
		if i < len(nums)-1 {
			fmt.Print(" ")
		}
		fmt.Print(nums[i])
	}
	fmt.Println()
}
