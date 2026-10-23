package main

import (
	"errors"
	"fmt"
	"strconv"
	"strings"
)

// ErrDivByZero возвращается, когда делитель равен нулю.
var ErrDivByZero = errors.New("division by zero")

// divmod возвращает частное и остаток; при b == 0 - ошибку ErrDivByZero.
func divmod(a, b int) (q, r int, err error) {
	if b == 0 {
		return 0, 0, ErrDivByZero
	}
	return a / b, a % b, nil
}

// parseRange разбирает строку вида "3-7".
func parseRange(s string) (lo, hi int, err error) {
	parts := strings.Split(s, "-")
	if len(parts) != 2 {
		return 0, 0, fmt.Errorf("parseRange %q: ожидалось два числа через дефис", s)
	}
	lo, err = strconv.Atoi(strings.TrimSpace(parts[0]))
	if err != nil {
		return 0, 0, fmt.Errorf("parseRange %q: %w", s, err)
	}
	hi, err = strconv.Atoi(strings.TrimSpace(parts[1]))
	if err != nil {
		return 0, 0, fmt.Errorf("parseRange %q: %w", s, err)
	}
	if lo > hi {
		return 0, 0, fmt.Errorf("parseRange %q: начало больше конца", s)
	}
	return lo, hi, nil
}

// sumRange суммирует числа диапазона s включительно.
func sumRange(s string) (int, error) {
	lo, hi, err := parseRange(s)
	if err != nil {
		return 0, err
	}
	total := 0
	for i := lo; i <= hi; i++ {
		total += i
	}
	return total, nil
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
