package main

import (
	"errors"
	"fmt"
)

// parseVersion разбирает версию "1.2.3" или "v1.2.3": ровно три неотрицательных
// целых числа через точку. Иначе верните 0, 0, 0 и ошибку.
func parseVersion(s string) (major, minor, patch int, err error) {
	return 0, 0, 0, errors.New("parseVersion не написана")
}

// compareVersions возвращает -1, 0 или 1 для a < b, a == b, a > b.
// Сравнивайте числа, а не строки: 1.10.0 больше 1.2.0.
// Если любая из версий некорректна, верните 0 и ошибку.
func compareVersions(a, b string) (int, error) {
	return 0, errors.New("compareVersions не написана")
}

func main() {
	fmt.Println(compareVersions("1.2.3", "v1.10.0"))
}
