package main

import (
	"bufio"
	"fmt"
	"os"
	"strconv"
)

// bestSegment возвращает максимальную сумму непустого непрерывного отрезка a.
func bestSegment(a []int64) int64 {
	best, cur := a[0], a[0]
	for _, x := range a[1:] {
		if cur+x > x {
			cur = cur + x
		} else {
			cur = x
		}
		if cur > best {
			best = cur
		}
	}
	return best
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
	fmt.Println(bestSegment(a))
}
