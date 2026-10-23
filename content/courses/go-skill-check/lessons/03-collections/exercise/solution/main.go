package main

import (
	"fmt"
	"unicode/utf8"
)

// groupByRuneLen группирует слова по числу символов, сохраняя порядок слов.
func groupByRuneLen(words []string) map[int][]string {
	groups := make(map[int][]string)
	for _, w := range words {
		n := utf8.RuneCountInString(w)
		groups[n] = append(groups[n], w)
	}
	return groups
}

// chunk режет items на куски по size элементов; последний кусок может быть короче.
func chunk(items []int, size int) [][]int {
	if size <= 0 {
		return [][]int{}
	}
	result := [][]int{}
	for start := 0; start < len(items); start += size {
		end := start + size
		if end > len(items) {
			end = len(items)
		}
		result = append(result, items[start:end])
	}
	return result
}

func main() {
	fmt.Println(groupByRuneLen([]string{"go", "я", "да", "hello"}))
	fmt.Println(chunk([]int{1, 2, 3, 4, 5}, 2))
}
