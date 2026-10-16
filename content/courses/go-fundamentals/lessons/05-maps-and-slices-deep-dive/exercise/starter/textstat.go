package textstat

import (
	"maps"
	"slices"
	"strings"
)

// WordFreq считает слова: приводит к нижнему регистру и убирает знаки . , ! ? ; : по краям.
func WordFreq(text string) map[string]int {
	// TODO: карта не создана, запись в nil-карту вызовет панику
	var freq map[string]int
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
	// TODO: порядок обхода карты случаен; отсортируйте words (slices.SortFunc)
	// и обрежьте до n элементов (n может быть больше длины или отрицательным)
	return words
}

// Chunk режет s на части по size элементов (size <= 0 даёт nil). Ёмкость каждой
// части должна быть равна её длине, чтобы append к одной части не затирал соседнюю.
func Chunk(s []int, size int) [][]int {
	if size <= 0 {
		return nil
	}
	var out [][]int
	for len(s) > 0 {
		n := min(size, len(s))
		// TODO: s[:n] делит массив с остальными частями; ограничьте ёмкость (s[:n:n])
		out = append(out, s[:n])
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
	// TODO: порядок ключей в группах случаен; отсортируйте каждую группу
	return out
}

// Dedup убирает повторы, сохраняя порядок первых вхождений. Результат - новый срез.
func Dedup(s []string) []string {
	// TODO: реализуйте через map[string]struct{}
	return s
}
