package main

import (
	"bufio"
	"fmt"
	"os"
	"sort"
	"strings"
)

// wordFreq считает слова без учёта регистра и знаков препинания по краям.
func wordFreq(text string) map[string]int {
	freq := make(map[string]int)
	for _, w := range strings.Fields(strings.ToLower(text)) {
		w = strings.Trim(w, ".,!?;:\"()")
		if w == "" {
			continue
		}
		freq[w]++
	}
	return freq
}

// topWords возвращает до n самых частых слов: по убыванию частоты, затем по алфавиту.
func topWords(freq map[string]int, n int) []string {
	words := make([]string, 0, len(freq))
	for w := range freq {
		words = append(words, w)
	}
	sort.Slice(words, func(i, j int) bool {
		if freq[words[i]] != freq[words[j]] {
			return freq[words[i]] > freq[words[j]]
		}
		return words[i] < words[j]
	})
	if n < 0 {
		n = 0
	}
	if n < len(words) {
		words = words[:n]
	}
	return words
}

// reverseRunes разворачивает строку по символам.
func reverseRunes(s string) string {
	r := []rune(s)
	for i, j := 0, len(r)-1; i < j; i, j = i+1, j-1 {
		r[i], r[j] = r[j], r[i]
	}
	return string(r)
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
