package main

import (
	"bufio"
	"fmt"
	"os"
	"strconv"
)

// countSegments возвращает число непустых непрерывных отрезков a с суммой ровно k (числа могут быть отрицательными).
func countSegments(a []int64, k int64) int64 {
	seen := make(map[int64]int64, len(a)+1) // префиксная сумма -> сколько раз встречалась
	seen[0] = 1                              // пустой префикс
	var cur, ans int64
	for _, x := range a {
		cur += x
		ans += seen[cur-k] // отсутствующий ключ даёт 0
		seen[cur]++
	}
	return ans
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
