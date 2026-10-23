package main

import "fmt"

// sumMultiples возвращает сумму чисел от 1 до n, которые делятся на k без остатка.
func sumMultiples(n, k int) int {
	sum := 0
	for i := 1; i <= n; i++ {
		if i%k == 0 {
			sum = sum + i
		}
	}
	return sum
}

// sign возвращает "отрицательное", "ноль" или "положительное".
func sign(n int) string {
	var word string
	if n < 0 {
		word = "отрицательное"
	} else if n == 0 {
		word = "ноль"
	} else {
		word = "положительное"
	}
	return word
}

func main() {
	fmt.Println(sumMultiples(10, 3))
	fmt.Println(sign(-4))
}
