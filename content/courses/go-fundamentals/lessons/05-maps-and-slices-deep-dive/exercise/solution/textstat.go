package textstat

import (
	"maps"
	"slices"
	"strings"
)

// WordFreq считает слова: приводит к нижнему регистру и убирает знаки . , ! ? ; : по краям.
func WordFreq(text string) map[string]int {
	freq := make(map[string]int)
	for _, w := range strings.Fields(strings.ToLower(text)) {
		w = strings.Trim(w, ".,!?;:")
		if w == "" {
			continue
		}
		freq[w]++
	}
	return freq
}

// TopWords возвращает не более n слов: по убыванию частоты, при равенстве по алфавиту.
func TopWords(freq map[string]int, n int) []string {
	words := slices.Collect(maps.Keys(freq))
	slices.SortFunc(words, func(a, b string) int {
		if freq[a] != freq[b] {
			return freq[b] - freq[a]
		}
		return strings.Compare(a, b)
	})
	if n < len(words) {
		words = words[:max(n, 0)]
	}
	return words
}

// Chunk режет s на части по size элементов. Ёмкость каждой части ограничена
// её длиной, поэтому append к одной части не затирает соседнюю.
func Chunk(s []int, size int) [][]int {
	if size <= 0 {
		return nil
	}
	var out [][]int
	for len(s) > 0 {
		n := min(size, len(s))
		out = append(out, s[:n:n])
		s = s[n:]
	}
	return out
}

// Invert группирует ключи по значению; слова в каждой группе отсортированы.
func Invert(m map[string]int) map[int][]string {
	out := make(map[int][]string)
	for k, v := range m {
		out[v] = append(out[v], k)
	}
	for _, ks := range out {
		slices.Sort(ks)
	}
	return out
}

// Dedup убирает повторы, сохраняя порядок первых вхождений. Результат - новый срез.
func Dedup(s []string) []string {
	seen := make(map[string]struct{}, len(s))
	out := make([]string, 0, len(s))
	for _, v := range s {
		if _, dup := seen[v]; dup {
			continue
		}
		seen[v] = struct{}{}
		out = append(out, v)
	}
	return out
}
