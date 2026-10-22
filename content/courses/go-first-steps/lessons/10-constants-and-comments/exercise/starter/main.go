package main

import "fmt"

func main() {
	// Объявите здесь константу vatRate со значением 0.2

	var price float64
	fmt.Scan(&price)
	withVAT := price * (1 + vatRate)
	fmt.Printf("Цена с НДС: %.2f\n", withVAT)
}
