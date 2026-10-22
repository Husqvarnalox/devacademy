package main

import "fmt"

// minMax возвращает наименьшее и наибольшее из трёх чисел (сначала наименьшее).
func minMax(a, b, c int) (int, int) {
	// TODO: найдите наименьшее и наибольшее
	return 0, 0
}

// divMod возвращает частное и остаток от деления a на b (b не равно нулю).
func divMod(a, b int) (int, int) {
	// TODO: верните два значения через запятую
	return 0, 0
}

func main() {
	lo, hi := minMax(4, 9, 2)
	fmt.Println("наименьшее:", lo, "наибольшее:", hi)
	q, r := divMod(17, 5)
	fmt.Println("частное:", q, "остаток:", r)
}
