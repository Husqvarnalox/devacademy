package main

import (
	"bufio"
	"fmt"
	"os"
	"strings"
)

// wordFreq считает слова без учёта регистра; знаки . , ! ? ; : " ( ) по краям слова отбрасываются.
// Подсказка: strings.Fields, strings.ToLower, strings.Trim.
func wordFreq(text string) map[string]int {
	return nil
}

// topWords возвращает до n самых частых слов: по убыванию частоты, при равенстве - по алфавиту.
// Подсказка: соберите ключи в срез и отсортируйте его через sort.Slice (пакет sort).
func topWords(freq map[string]int, n int) []string {
	return nil
}

// reverseRunes разворачивает строку по символам (рунам), а не по байтам.
func reverseRunes(s string) string {
	return s
}

func main() {
	var sb strings.Builder
	sc := bufio.NewScanner(os.Stdin)
	for sc.Scan() {
		sb.WriteString(sc.Text())
		sb.WriteString(" ")
	}
	for _, w := range topWords(wordFreq(sb.String()), 3) {
		fmt.Println(w, reverseRunes(w))
	}
}
