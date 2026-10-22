package main

import "fmt"

func main() {
	var temps [7]int
	for i := 0; i < len(temps); i++ {
		fmt.Scan(&temps[i])
	}
	maxIndex := 0
	for i := 1; i < len(temps); i++ {
		if temps[i] > temps[maxIndex] {
			maxIndex = i
		}
	}
	fmt.Printf("max=%d\n", temps[maxIndex])
	fmt.Printf("day=%d\n", maxIndex+1)
}
