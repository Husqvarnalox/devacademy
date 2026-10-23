package main

import (
	"errors"
	"fmt"
)

// ErrZero возвращается при делении на ноль.
var ErrZero = errors.New("division by zero")

// safeDivide делит a на b; при b == 0 возвращает 0 и ErrZero.
func safeDivide(a, b int) (int, error) {
	return 0, errors.New("safeDivide не написана")
}

// parseKV разбирает строку вида "ключ=число" (пробелы вокруг частей допустимы).
// Ошибка: нет знака =, пустой ключ, значение не число. При ошибке верните "", 0, err.
// Подсказка: strings.Cut, strings.TrimSpace, strconv.Atoi.
func parseKV(s string) (string, int, error) {
	return "", 0, errors.New("parseKV не написана")
}

func main() {
	q, err := safeDivide(7, 2)
	fmt.Println(q, err)
	k, v, err := parseKV("age=30")
	fmt.Println(k, v, err)
}
