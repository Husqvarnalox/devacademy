package main

import "fmt"

// withAppended возвращает НОВЫЙ срез: элементы s плюс v.
// Нельзя писать в спарную ёмкость s: после вызова s[:cap(s)] не должен измениться.
func withAppended(s []int, v int) []int {
	return nil
}

// firstN возвращает первые n элементов s (n ограничивается диапазоном 0..len(s)).
// Результат должен делить память с s (запись по индексу видна в s),
// но append к результату не должен перезаписывать элементы s.
func firstN(s []int, n int) []int {
	return nil
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
