package main

import (
	"bufio"
	"fmt"
	"io"
	"os"
	"strings"
	"unicode/utf8"
)

// sumNumbers читает целые числа до конца данных и возвращает сумму и количество.
func sumNumbers(r io.Reader) (sum int64, count int, err error) {
	in := bufio.NewReader(r)
	for {
		var v int64
		_, err := fmt.Fscan(in, &v)
		if err == io.EOF {
			return sum, count, nil
		}
		if err != nil {
			return sum, count, err
		}
		sum += v
		count++
	}
}

// lineStats возвращает число строк, слов и длину самой длинной строки в символах.
func lineStats(r io.Reader) (lines, words, longest int, err error) {
	sc := bufio.NewScanner(r)
	sc.Buffer(make([]byte, 0, 64*1024), 1<<20)
	for sc.Scan() {
		text := sc.Text()
		lines++
		words += len(strings.Fields(text))
		if n := utf8.RuneCountInString(text); n > longest {
			longest = n
		}
	}
	return lines, words, longest, sc.Err()
}

func main() {
	data, err := io.ReadAll(os.Stdin)
	if err != nil {
		fmt.Println("ошибка:", err)
		return
	}
	text := string(data)
	sum, count, err := sumNumbers(strings.NewReader(text))
	if err != nil {
		fmt.Println("числа:", err)
	} else {
		fmt.Println("сумма:", sum, "количество:", count)
	}
	lines, words, longest, err := lineStats(strings.NewReader(text))
	if err != nil {
		fmt.Println("ошибка:", err)
		return
	}
	fmt.Println("строк:", lines, "слов:", words, "длиннейшая:", longest)
}
