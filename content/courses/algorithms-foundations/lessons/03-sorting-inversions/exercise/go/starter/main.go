package main

import (
	"bufio"
	"fmt"
	"os"
	"strconv"
)

// sortCount сортирует a на месте (tmp - буфер той же длины) и возвращает число пар i < j с a[i] > a[j].
func sortCount(a, tmp []int64) int64 {
	// TODO: сортировка слиянием, которая заодно считает инверсии (O(n log n))
	_ = tmp
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
	a := make([]int64, n)
	for i := range a {
		a[i] = next()
	}
	fmt.Println(sortCount(a, make([]int64, n)))
}
