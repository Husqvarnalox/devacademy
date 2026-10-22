package main

import "fmt"

func main() {
	var day int
	fmt.Scan(&day)
	switch day {
	case 1:
		fmt.Println("понедельник")
	case 2:
		fmt.Println("вторник")
	case 3:
		fmt.Println("среда")
	case 4:
		fmt.Println("четверг")
	case 5:
		fmt.Println("пятница")
	case 6:
		fmt.Println("суббота")
	case 7:
		fmt.Println("воскресенье")
	default:
		fmt.Println("Нет такого дня")
	}
}
