package main

import "fmt"

// byteAndRuneCount возвращает число байтов и число символов (рун) строки s.
func byteAndRuneCount(s string) (int, int) {
	return 0, 0
}

// reverse возвращает строку s, записанную в обратном порядке (по символам, а не по байтам).
func reverse(s string) string {
	return ""
}

// capitalize возвращает строку, в которой первая буква заглавная, а остальные строчные.
// Для пустой строки возвращает пустую строку.
// Подсказка: пакет strings, функции ToUpper и ToLower; не забудьте добавить его в import.
func capitalize(s string) string {
	return ""
}

func main() {
	b, r := byteAndRuneCount("привет")
	fmt.Println(b, r)
	fmt.Println(reverse("привет"))
	fmt.Println(capitalize("пРИВЕТ"))
}
