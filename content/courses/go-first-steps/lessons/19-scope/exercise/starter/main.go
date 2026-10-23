package main

import "fmt"

// sumMultiples возвращает сумму чисел от 1 до n, которые делятся на k без остатка.
// Программа собирается, но возвращает неверный результат: причина в затенении переменной.
func sumMultiples(n, k int) int {
	sum := 0
	for i := 1; i <= n; i++ {
		if i%k == 0 {
			sum := sum + i
			fmt.Println("добавили", i, "- сумма теперь", sum)
		}
	}
	return sum
}

// sign возвращает "отрицательное", "ноль" или "положительное".
// Эта функция не компилируется: переменная word объявлена не там, где она нужна.
func sign(n int) string {
	if n < 0 {
		word := "отрицательное"
	} else if n == 0 {
		word := "ноль"
	} else {
		word := "положительное"
	}
	return word
}

func main() {
	fmt.Println(sumMultiples(10, 3))
	fmt.Println(sign(-4))
}
