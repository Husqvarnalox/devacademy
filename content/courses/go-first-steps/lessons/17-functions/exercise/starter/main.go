package main

import "fmt"

// greet печатает приветствие: Здравствуйте, <имя>!
func greet(name string) {
	// Напечатайте приветствие с помощью fmt.Println: текст "Здравствуйте," и значение name.
}

// repeatLine печатает text на отдельной строке ровно times раз.
func repeatLine(text string, times int) {
	// Подсказка: цикл for i := 0; i < times; i++ { ... }
}

func main() {
	fmt.Println("Пример вызова:")
	greet("Анна")
	repeatLine("Go", 3)
}
