package main

import (
	"errors"
	"fmt"
	"strconv"
	"strings"
)

// ErrZero возвращается при делении на ноль.
var ErrZero = errors.New("division by zero")

// safeDivide делит a на b; при b == 0 возвращает 0 и ErrZero.
func safeDivide(a, b int) (int, error) {
	if b == 0 {
		return 0, ErrZero
	}
	return a / b, nil
}

// parseKV разбирает строку "ключ=число".
func parseKV(s string) (string, int, error) {
	key, val, found := strings.Cut(s, "=")
	if !found {
		return "", 0, fmt.Errorf("parseKV %q: нет знака =", s)
	}
	key = strings.TrimSpace(key)
	if key == "" {
		return "", 0, fmt.Errorf("parseKV %q: пустой ключ", s)
	}
	n, err := strconv.Atoi(strings.TrimSpace(val))
	if err != nil {
		return "", 0, fmt.Errorf("parseKV %q: %w", s, err)
	}
	return key, n, nil
}

func main() {
	q, err := safeDivide(7, 2)
	fmt.Println(q, err)
	k, v, err := parseKV("age=30")
	fmt.Println(k, v, err)
}
