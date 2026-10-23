package main

import "fmt"

func main() {
	var score int
	fmt.Scan(&score)
	if score < 0 || score > 100 {
		fmt.Println("Неверный балл")
	} else if score >= 90 {
		fmt.Println("Оценка: 5")
	} else if score >= 75 {
		fmt.Println("Оценка: 4")
	} else if score >= 60 {
		fmt.Println("Оценка: 3")
	} else {
		fmt.Println("Оценка: 2")
	}
}
