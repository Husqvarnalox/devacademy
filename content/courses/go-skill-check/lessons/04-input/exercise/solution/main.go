package main

import (
	"bufio"
	"fmt"
	"io"
	"strconv"
	"strings"
)

// totalByName суммирует суммы по именам из строк "имя сумма".
func totalByName(r io.Reader) (map[string]int64, error) {
	totals := make(map[string]int64)
	sc := bufio.NewScanner(r)
	lineNo := 0
	for sc.Scan() {
		lineNo++
		line := strings.TrimSpace(sc.Text())
		if line == "" {
			continue
		}
		fields := strings.Fields(line)
		if len(fields) != 2 {
			return nil, fmt.Errorf("строка %d: ожидалось два поля, получено %d", lineNo, len(fields))
		}
		amount, err := strconv.ParseInt(fields[1], 10, 64)
		if err != nil {
			return nil, fmt.Errorf("строка %d: %w", lineNo, err)
		}
		totals[fields[0]] += amount
	}
	if err := sc.Err(); err != nil {
		return nil, err
	}
	return totals, nil
}

func main() {
	input := "anna 10\nboris 5\n\nanna 7\n"
	totals, err := totalByName(strings.NewReader(input))
	fmt.Println(totals, err)
}
