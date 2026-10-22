package main

import (
	"errors"
	"fmt"
)

// parseAge переводит строку s в возраст.
//
//   - если s не число, верните 0 и ошибку;
//   - если число меньше 0 или больше 150, верните 0 и ошибку (создайте её через errors.New);
//   - иначе верните возраст и nil.
//
// Для перевода строки в число понадобится пакет strconv: добавьте его в import.
func parseAge(s string) (int, error) {
	return 0, errors.New("parseAge не написана")
}

func main() {
	var text string
	fmt.Scan(&text)
	age, err := parseAge(text)
	if err != nil {
		fmt.Println("ошибка:", err)
		return
	}
	fmt.Println("возраст:", age)
}
