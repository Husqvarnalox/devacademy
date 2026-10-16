// Package stats считает частоты слов.
package stats

import (
	"sort"

	"example.com/textkit/internal/norm"
)

// Entry - слово и число его вхождений.
type Entry struct {
	Word  string
	Count int
}

// WordCount возвращает частоты слов текста (см. norm.Words).
func WordCount(text string) map[string]int {
	m := make(map[string]int)
	for _, w := range norm.Words(text) {
		m[w]++
	}
	return m
}

// Top возвращает до n самых частых слов: по убыванию Count, при равенстве - по алфавиту.
// При n <= 0 возвращается пустой срез.
func Top(counts map[string]int, n int) []Entry {
	out := make([]Entry, 0, len(counts))
	for w, c := range counts {
		out = append(out, Entry{w, c})
	}
	sort.Slice(out, func(i, j int) bool {
		if out[i].Count != out[j].Count {
			return out[i].Count > out[j].Count
		}
		return out[i].Word < out[j].Word
	})
	if n < 0 {
		n = 0
	}
	if n < len(out) {
		out = out[:n]
	}
	return out
}
