package main

import (
	"fmt"
	"strings"
)

// byteAndRuneCount возвращает число байтов и число символов (рун) строки s.
func byteAndRuneCount(s string) (int, int) {
	return len(s), len([]rune(s))
}

// reverse возвращает строку s, записанную в обратном порядке (по символам, а не по байтам).
func reverse(s string) string {
	result := ""
	for _, r := range s {
		result = string(r) + result
	}
	return result
}

// capitalize возвращает строку, в которой первая буква заглавная, а остальные строчные.
func capitalize(s string) string {
	result := ""
	for i, r := range []rune(s) {
		if i == 0 {
			result += strings.ToUpper(string(r))
		} else {
			result += strings.ToLower(string(r))
		}
	}
	return result
}

func main() {
	b, r := byteAndRuneCount("привет")
	fmt.Println(b, r)
	fmt.Println(reverse("привет"))
	fmt.Println(capitalize("пРИВЕТ"))
}
