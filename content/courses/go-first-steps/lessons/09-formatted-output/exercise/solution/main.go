package main

import "fmt"

func main() {
	var name string
	var quantity int
	var price float64
	fmt.Scan(&name, &quantity, &price)
	total := float64(quantity) * price
	fmt.Printf("%s: %d x %.2f = %.2f\n", name, quantity, price, total)
}
