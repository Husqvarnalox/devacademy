package main

import (
	"errors"
	"fmt"
)

// ErrDivByZero возвращается, когда делитель равен нулю.
var ErrDivByZero = errors.New("division by zero")

// divmod возвращает частное и остаток; при b == 0 - ошибку ErrDivByZero.
func divmod(a, b int) (q, r int, err error) {
	return 0, 0, errors.New("divmod не написана")
}

// parseRange разбирает строку вида "3-7" (два числа через дефис, пробелы допустимы).
// Ошибка: частей не две, часть не число, lo > hi. При ошибке верните 0, 0, err.
// Понадобятся пакеты strings и strconv: добавьте их в import.
func parseRange(s string) (lo, hi int, err error) {
	return 0, 0, errors.New("parseRange не написана")
}

// sumRange суммирует все числа диапазона s включительно, например "1-4" -> 10.
func sumRange(s string) (int, error) {
	return 0, errors.New("sumRange не написана")
}

func main() {
	var text string
	fmt.Scan(&text)
	sum, err := sumRange(text)
	if err != nil {
		fmt.Println("ошибка:", err)
		return
	}
	fmt.Println(sum)
}
