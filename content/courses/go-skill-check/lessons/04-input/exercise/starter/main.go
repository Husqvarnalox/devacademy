package main

import (
	"errors"
	"fmt"
	"io"
	"strings"
)

// totalByName читает строки вида "имя сумма" и суммирует суммы по именам.
// Пустые строки пропускаются. Строка не из двух полей или с нечисловой суммой - ошибка
// (тогда верните nil и ошибку). Суммы - int64.
// Подсказка: bufio.Scanner, strings.Fields, strconv.ParseInt.
func totalByName(r io.Reader) (map[string]int64, error) {
	return nil, errors.New("totalByName не написана")
}

func main() {
	input := "anna 10\nboris 5\n\nanna 7\n"
	totals, err := totalByName(strings.NewReader(input))
	fmt.Println(totals, err)
}
