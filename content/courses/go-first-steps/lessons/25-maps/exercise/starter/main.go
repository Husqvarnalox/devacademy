package main

import "fmt"

func main() {
	counts := map[string]int{}
	for {
		var word string
		_, err := fmt.Scan(&word)
		if err != nil {
			break
		}
		// Увеличьте счётчик слова word в карте counts
	}
	// Выведите слова в алфавитном порядке: слово, пробел, сколько раз встретилось
	fmt.Println(len(counts))
}
