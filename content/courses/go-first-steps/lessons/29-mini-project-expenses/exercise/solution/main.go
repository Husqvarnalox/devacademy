package main

import (
	"bufio"
	"fmt"
	"os"
	"slices"
	"strconv"
	"strings"
)

func main() {
	sc := bufio.NewScanner(os.Stdin)
	sums := map[string]int{}
	total := 0
	skipped := 0
	for sc.Scan() {
		fields := strings.Fields(sc.Text())
		if len(fields) == 0 {
			continue
		}
		if len(fields) != 2 {
			skipped++
			continue
		}
		amount, err := strconv.Atoi(fields[1])
		if err != nil || amount <= 0 {
			skipped++
			continue
		}
		sums[fields[0]] += amount
		total += amount
	}
	categories := []string{}
	for c := range sums {
		categories = append(categories, c)
	}
	slices.Sort(categories)
	for _, c := range categories {
		fmt.Printf("%s: %d\n", c, sums[c])
	}
	fmt.Println("Итого:", total)
	fmt.Println("Пропущено:", skipped)
}
