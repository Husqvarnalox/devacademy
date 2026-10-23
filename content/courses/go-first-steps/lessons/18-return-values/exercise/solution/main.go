package main

import "fmt"

// minMax возвращает наименьшее и наибольшее из трёх чисел (сначала наименьшее).
func minMax(a, b, c int) (int, int) {
	lo := a
	hi := a
	if b < lo {
		lo = b
	}
	if c < lo {
		lo = c
	}
	if b > hi {
		hi = b
	}
	if c > hi {
		hi = c
	}
	return lo, hi
}

// divMod возвращает частное и остаток от деления a на b (b не равно нулю).
func divMod(a, b int) (int, int) {
	return a / b, a % b
}

func main() {
	lo, hi := minMax(4, 9, 2)
	fmt.Println("наименьшее:", lo, "наибольшее:", hi)
	q, r := divMod(17, 5)
	fmt.Println("частное:", q, "остаток:", r)
}
