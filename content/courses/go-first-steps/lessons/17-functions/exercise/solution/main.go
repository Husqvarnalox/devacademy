package main

import "fmt"

// greet печатает приветствие: Здравствуйте, <имя>!
func greet(name string) {
	fmt.Println("Здравствуйте,", name+"!")
}

// repeatLine печатает text на отдельной строке ровно times раз.
func repeatLine(text string, times int) {
	for i := 0; i < times; i++ {
		fmt.Println(text)
	}
}

func main() {
	fmt.Println("Пример вызова:")
	greet("Анна")
	repeatLine("Go", 3)
}
