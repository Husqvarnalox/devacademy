package main

import (
	"fmt"
	"os"
)

// Greet возвращает приветствие.
func Greet(name string) string {
	return fmt.Sprintf("Hello, %s!", name)
}

func main() {
	name := "world"
	if len(os.Args) > 1 {
		name = os.Args[1]
	}
	fmt.Println(Greet(name))
}
