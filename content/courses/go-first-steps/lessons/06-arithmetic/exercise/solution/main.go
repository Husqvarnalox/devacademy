package main

import "fmt"

func main() {
	total := 135
	hours := total / 60
	minutes := total % 60

	fmt.Println(hours, "ч", minutes, "мин")
}
