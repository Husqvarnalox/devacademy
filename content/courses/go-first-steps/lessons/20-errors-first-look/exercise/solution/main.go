package main

import (
	"errors"
	"fmt"
	"strconv"
)

// parseAge переводит строку s в возраст.
func parseAge(s string) (int, error) {
	age, err := strconv.Atoi(s)
	if err != nil {
		return 0, err
	}
	if age < 0 || age > 150 {
		return 0, errors.New("возраст должен быть от 0 до 150")
	}
	return age, nil
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
