package main

import "fmt"

// daysInMonth возвращает число дней в месяце (1-12) указанного года.
// Високосный год: делится на 4, кроме годов, кратных 100 (но кратные 400 високосные).
// Для месяца вне диапазона 1-12 верните 0.
func daysInMonth(year, month int) int {
	return 0
}

// isPrime сообщает, является ли n простым числом (простые начинаются с 2).
func isPrime(n int) bool {
	return false
}

func main() {
	var year, month, n int
	fmt.Scan(&year, &month, &n)
	fmt.Println(daysInMonth(year, month), isPrime(n))
}
