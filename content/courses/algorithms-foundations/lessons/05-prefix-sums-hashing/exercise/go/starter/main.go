package main

import (
	"bufio"
	"fmt"
	"os"
	"strconv"
)

// countSegments возвращает число непустых непрерывных отрезков a с суммой ровно k (числа могут быть отрицательными).
func countSegments(a []int64, k int64) int64 {
	// TODO: префиксные суммы + map "сумма -> сколько раз встречалась", O(n)
	_, _ = a, k
	return 0
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
	k := next()
	a := make([]int64, n)
	for i := range a {
		a[i] = next()
	}
	fmt.Println(countSegments(a, k))
}
