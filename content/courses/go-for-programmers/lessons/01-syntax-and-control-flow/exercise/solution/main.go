package main

import "fmt"

// digitSum возвращает сумму десятичных цифр неотрицательного n.
func digitSum(n int) int {
	sum := 0
	for n > 0 {
		sum += n % 10
		n /= 10
	}
	return sum
}

// collatzSteps считает шаги, за которые n >= 1 приходит к 1.
func collatzSteps(n int) int {
	steps := 0
	for n != 1 {
		if n%2 == 0 {
			n /= 2
		} else {
			n = 3*n + 1
		}
		steps++
	}
	return steps
}

// classify называет число: negative, zero, even или odd.
func classify(n int) string {
	switch {
	case n < 0:
		return "negative"
	case n == 0:
		return "zero"
	case n%2 == 0:
		return "even"
	default:
		return "odd"
	}
}

// ratio возвращает дробное частное a / b; при b == 0 возвращает 0.
func ratio(a, b int) float64 {
	if b == 0 {
		return 0
	}
	return float64(a) / float64(b)
}

func main() {
	var n int
	fmt.Scan(&n)
	fmt.Println(digitSum(n), collatzSteps(n+1), classify(n))
}
