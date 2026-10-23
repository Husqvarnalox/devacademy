package main

import "fmt"

func main() {
	var age, balance, price int
	fmt.Scan(&age, &balance, &price)
	// adult - возраст не меньше 18
	// enough - денег хватает на товар
	// canBuy - совершеннолетний и денег хватает

	fmt.Println("Совершеннолетний:", adult)
	fmt.Println("Хватает денег:", enough)
	fmt.Println("Можно купить:", canBuy)
}
