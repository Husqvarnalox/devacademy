package main

import "fmt"

func main() {
	var n int
	fmt.Scan(&n)
	sum := 0
	for i := 1; i <= n; i++ {
		if i > 1 {
			fmt.Print(" ")
		}
		fmt.Print(i)
		sum += i
	}
	fmt.Println()
	fmt.Println("Сумма:", sum)
}
