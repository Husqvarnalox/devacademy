package main

import (
	"errors"
	"fmt"
	"io"
	"os"
	"strings"
)

// sumNumbers читает целые числа (через пробелы и переводы строк) до конца данных
// и возвращает их сумму и количество. Нечисловой токен - ошибка.
// Подсказка: bufio.NewReader, fmt.Fscan, io.EOF; сумма - int64.
func sumNumbers(r io.Reader) (sum int64, count int, err error) {
	return 0, 0, errors.New("sumNumbers не написана")
}

// lineStats возвращает число строк, число слов и длину самой длинной строки в символах.
// Подсказка: bufio.Scanner, strings.Fields, utf8.RuneCountInString; строки до 1 МБ
// потребуют sc.Buffer.
func lineStats(r io.Reader) (lines, words, longest int, err error) {
	return 0, 0, 0, errors.New("lineStats не написана")
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
