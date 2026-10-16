package main

import (
	"bufio"
	"fmt"
	"os"
)

// reverse должна разворачивать строку по символам (рунам).
func reverse(s string) string {
	// TODO: сейчас строка разворачивается по байтам и ломает не-ASCII текст
	b := []byte(s)
	for i, j := 0, len(b)-1; i < j; i, j = i+1, j-1 {
		b[i], b[j] = b[j], b[i]
	}
	return string(b)
}

func main() {
	sc := bufio.NewScanner(os.Stdin)
	line := ""
	if sc.Scan() {
		line = sc.Text()
	}
	fmt.Printf("bytes: %d\n", len(line))
	// TODO: len считает байты; здесь нужно число символов (рун)
	fmt.Printf("runes: %d\n", len(line))
	fmt.Printf("reversed: %s\n", reverse(line))
	if line == "" {
		fmt.Println("first: none")
		return
	}
	// TODO: line[0] - это первый БАЙТ; нужна первая руна
	fmt.Printf("first: U+%04X\n", line[0])
}
