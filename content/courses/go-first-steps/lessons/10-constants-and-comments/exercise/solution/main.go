package main

import "fmt"

func main() {
	// Ставка НДС: 20 процентов
	const vatRate = 0.2

	var price float64
	fmt.Scan(&price)
	withVAT := price * (1 + vatRate)
	fmt.Printf("Цена с НДС: %.2f\n", withVAT)
}
