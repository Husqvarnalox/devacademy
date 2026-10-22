package main

import "fmt"

func main() {
	var age, balance, price int
	fmt.Scan(&age, &balance, &price)
	adult := age >= 18
	enough := balance >= price
	canBuy := adult && enough

	fmt.Println("Совершеннолетний:", adult)
	fmt.Println("Хватает денег:", enough)
	fmt.Println("Можно купить:", canBuy)
}
