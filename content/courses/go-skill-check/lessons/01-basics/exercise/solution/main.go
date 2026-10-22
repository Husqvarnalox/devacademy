package main

import "fmt"

// daysInMonth возвращает число дней в месяце года; для неверного месяца - 0.
func daysInMonth(year, month int) int {
	switch month {
	case 1, 3, 5, 7, 8, 10, 12:
		return 31
	case 4, 6, 9, 11:
		return 30
	case 2:
		if year%400 == 0 || (year%4 == 0 && year%100 != 0) {
			return 29
		}
		return 28
	default:
		return 0
	}
}

// isPrime сообщает, является ли n простым числом.
func isPrime(n int) bool {
	if n < 2 {
		return false
	}
	for d := 2; d*d <= n; d++ {
		if n%d == 0 {
			return false
		}
	}
	return true
}

func main() {
	var year, month, n int
	fmt.Scan(&year, &month, &n)
	fmt.Println(daysInMonth(year, month), isPrime(n))
}
