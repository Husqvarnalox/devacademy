package main

import "fmt"

func main() {
	var productName string = "Чай"
	var price float64 = 12.5
	var quantity int = 4
	var inStock bool = true
	var discount float64

	fmt.Println("Товар:", productName)
	fmt.Println("Цена:", price)
	fmt.Println("Количество:", quantity)
	fmt.Println("В наличии:", inStock)
	fmt.Println("Скидка:", discount)
}
