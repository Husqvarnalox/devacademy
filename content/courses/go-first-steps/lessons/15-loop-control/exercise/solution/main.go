package main

import "fmt"

func main() {
	sum := 0
	for {
		var x int
		fmt.Scan(&x)
		if x == 0 {
			break
		}
		if x < 0 {
			continue
		}
		sum += x
	}
	fmt.Println("Сумма:", sum)
}
