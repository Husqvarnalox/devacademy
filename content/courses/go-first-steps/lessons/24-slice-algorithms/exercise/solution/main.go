package main

import (
	"fmt"
	"slices"
)

func main() {
	var n int
	fmt.Scan(&n)
	var scores []int
	for i := 0; i < n; i++ {
		var x int
		fmt.Scan(&x)
		scores = append(scores, x)
	}
	var passed []int
	for _, s := range scores {
		if s >= 60 {
			passed = append(passed, s)
		}
	}
	if len(passed) == 0 {
		fmt.Println("нет")
		return
	}
	slices.Sort(passed)
	for i, s := range passed {
		if i > 0 {
			fmt.Print(" ")
		}
		fmt.Print(s)
	}
	fmt.Println()
}
