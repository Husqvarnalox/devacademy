package main

import (
	"bufio"
	"fmt"
	"os"
	"unicode/utf8"
)

// reverse разворачивает строку по символам (рунам), а не по байтам.
func reverse(s string) string {
	r := []rune(s)
	for i, j := 0, len(r)-1; i < j; i, j = i+1, j-1 {
		r[i], r[j] = r[j], r[i]
	}
	return string(r)
}

func main() {
	sc := bufio.NewScanner(os.Stdin)
	line := ""
	if sc.Scan() {
		line = sc.Text()
	}
	fmt.Printf("bytes: %d\n", len(line))
	fmt.Printf("runes: %d\n", utf8.RuneCountInString(line))
	fmt.Printf("reversed: %s\n", reverse(line))
	if line == "" {
		fmt.Println("first: none")
		return
	}
	r, _ := utf8.DecodeRuneInString(line)
	fmt.Printf("first: U+%04X\n", r)
}
