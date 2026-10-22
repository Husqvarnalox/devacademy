package main

import "fmt"

// groupByRuneLen группирует слова по числу символов (рун), сохраняя порядок слов в группе.
// Подсказка: unicode/utf8.RuneCountInString.
func groupByRuneLen(words []string) map[int][]string {
	return nil
}

// chunk режет items на куски по size элементов; последний кусок может быть короче.
// При size <= 0 или пустом items верните пустой срез срезов.
func chunk(items []int, size int) [][]int {
	return nil
}

func main() {
	fmt.Println(groupByRuneLen([]string{"go", "я", "да", "hello"}))
	fmt.Println(chunk([]int{1, 2, 3, 4, 5}, 2))
}
