package main

import "fmt"

// withAppended возвращает новый срез: элементы s плюс v.
// Запас ёмкости s не должен меняться.
func withAppended(s []int, v int) []int {
	out := make([]int, len(s), len(s)+1)
	copy(out, s)
	return append(out, v)
}

// firstN возвращает первые n элементов s (n ограничивается 0..len(s)).
// Результат делит память с s, но append к нему не затрагивает s.
func firstN(s []int, n int) []int {
	if n < 0 {
		n = 0
	}
	if n > len(s) {
		n = len(s)
	}
	return s[:n:n]
}

func main() {
	base := make([]int, 2, 10)
	base[0], base[1] = 1, 2
	r := withAppended(base, 3)
	fmt.Println(r, base[:3])

	head := firstN(base, 1)
	head = append(head, 99)
	fmt.Println(head, base)
}
