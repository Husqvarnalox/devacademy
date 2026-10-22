package main

import "fmt"

func main() {
	var n int
	fmt.Scan(&n)
	var scores []int
	for i := 0; i < n; i++ {
		var x int
		fmt.Scan(&x)
		scores = append(scores, x)
	}
	// Отберите баллы не ниже 60 в новый срез, отсортируйте его (пакет slices) и напечатайте
	fmt.Println(scores)
}
