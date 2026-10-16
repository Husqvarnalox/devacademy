package main

import (
	"bufio"
	"fmt"
	"os"
	"strconv"
)

// countSegments возвращает число непустых непрерывных отрезков a с суммой не больше limit (все a[i] > 0).
func countSegments(a []int64, limit int64) int64 {
	var total, cur int64
	left := 0 // окно a[left..right]; инвариант: его сумма cur <= limit (или окно пусто)
	for right, x := range a {
		cur += x
		for cur > limit {
			cur -= a[left]
			left++
		}
		total += int64(right - left + 1) // подходящие отрезки, оканчивающиеся в right
	}
	return total
}

func main() {
	sc := bufio.NewScanner(os.Stdin)
	sc.Buffer(make([]byte, 1<<20), 1<<26)
	sc.Split(bufio.ScanWords)
	next := func() int64 {
		sc.Scan()
		v, _ := strconv.ParseInt(sc.Text(), 10, 64)
		return v
	}
	n := int(next())
	limit := next()
	a := make([]int64, n)
	for i := range a {
		a[i] = next()
	}
	fmt.Println(countSegments(a, limit))
}
