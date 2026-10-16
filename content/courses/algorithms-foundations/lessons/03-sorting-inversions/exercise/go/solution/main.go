package main

import (
	"bufio"
	"fmt"
	"os"
	"strconv"
)

// sortCount сортирует a на месте (tmp - буфер той же длины) и возвращает число пар i < j с a[i] > a[j].
func sortCount(a, tmp []int64) int64 {
	n := len(a)
	if n <= 1 {
		return 0
	}
	mid := n / 2
	inv := sortCount(a[:mid], tmp[:mid]) + sortCount(a[mid:], tmp[mid:])
	i, j, k := 0, mid, 0
	for i < mid && j < n {
		if a[i] <= a[j] {
			tmp[k] = a[i]
			i++
		} else {
			tmp[k] = a[j]
			j++
			inv += int64(mid - i) // a[j] меньше всех оставшихся слева
		}
		k++
	}
	for i < mid {
		tmp[k] = a[i]
		i++
		k++
	}
	for j < n {
		tmp[k] = a[j]
		j++
		k++
	}
	copy(a, tmp[:n])
	return inv
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
