package main

import "fmt"

func main() {
	var a, b, c int
	fmt.Scan(&a, &b, &c)
	avg := float64(a+b+c) / 3
	fmt.Println(avg)
	fmt.Println(int(avg))
}
