package main

import (
	"fmt"
	"io"
	"os"
	"slices"
	"unicode"
)

type entry struct {
	r rune
	n int
}

func main() {
	data, _ := io.ReadAll(os.Stdin)
	counts := map[rune]int{}
	for _, r := range string(data) {
		if unicode.IsSpace(r) {
			continue
		}
		counts[r]++
	}
	list := make([]entry, 0, len(counts))
	for r, n := range counts {
		list = append(list, entry{r, n})
	}
	slices.SortFunc(list, func(a, b entry) int {
		if a.n != b.n {
			return b.n - a.n
		}
		return int(a.r - b.r)
	})
	for _, e := range list {
		fmt.Printf("%c %d\n", e.r, e.n)
	}
}
