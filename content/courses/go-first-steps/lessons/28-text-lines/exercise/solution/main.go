package main

import (
	"bufio"
	"fmt"
	"os"
	"strconv"
	"strings"
)

func main() {
	sc := bufio.NewScanner(os.Stdin)
	bestName := ""
	bestAvg := -1.0
	for sc.Scan() {
		fields := strings.Fields(sc.Text())
		if len(fields) == 0 {
			continue
		}
		sum := 0
		for i, f := range fields {
			if i == 0 {
				continue
			}
			grade, _ := strconv.Atoi(f)
			sum += grade
		}
		avg := float64(sum) / float64(len(fields)-1)
		fmt.Printf("%s: %.2f\n", fields[0], avg)
		if avg > bestAvg {
			bestAvg = avg
			bestName = fields[0]
		}
	}
	if bestName != "" {
		fmt.Println("best:", bestName)
	}
}
