package main

import (
	"fmt"
	"slices"
)

func main() {
	counts := map[string]int{}
	for {
		var word string
		_, err := fmt.Scan(&word)
		if err != nil {
			break
		}
		counts[word]++
	}
	var keys []string
	for word := range counts {
		keys = append(keys, word)
	}
	slices.Sort(keys)
	for _, word := range keys {
		fmt.Println(word, counts[word])
	}
}
