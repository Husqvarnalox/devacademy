package main

import "fmt"

// digitSum возвращает сумму десятичных цифр неотрицательного n.
// Подсказка: n % 10 - последняя цифра, n / 10 - число без неё.
func digitSum(n int) int {
	return 0
}

// collatzSteps считает шаги, за которые n >= 1 приходит к 1:
// чётное n делится на 2, нечётное заменяется на 3*n+1.
func collatzSteps(n int) int {
	return 0
}

// classify называет число: "negative", "zero", "even" (чётное положительное) или "odd".
func classify(n int) string {
	return ""
}

// ratio возвращает дробное частное a / b; при b == 0 возвращает 0.
func ratio(a, b int) float64 {
	return 0
}

func main() {
	var n int
	fmt.Scan(&n)
	fmt.Println(digitSum(n), collatzSteps(n+1), classify(n))
}
