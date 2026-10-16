package main

import (
	"bufio"
	"fmt"
	"os"
	"strconv"
)

// pieces возвращает, сколько кусков длины length получится из всех досок.
func pieces(a []int64, length int64) int64 {
	var total int64
	for _, x := range a {
		total += x / length
	}
	return total
}

// longestPiece возвращает наибольшую целую длину L >= 1, при которой выходит не менее k кусков; 0, если такой L нет.
func longestPiece(a []int64, k int64) int64 {
	var mx int64
	for _, x := range a {
		if x > mx {
			mx = x
		}
	}
	lo, hi := int64(0), mx+1 // инвариант: lo подходит (0 - условно), hi не подходит
	for hi-lo > 1 {
		mid := lo + (hi-lo)/2
		if pieces(a, mid) >= k {
			lo = mid
		} else {
			hi = mid
		}
	}
	return lo
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
	fmt.Println(longestPiece(a, k))
}
