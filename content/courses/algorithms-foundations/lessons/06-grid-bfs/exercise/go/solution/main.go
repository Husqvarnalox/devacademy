package main

import (
	"bufio"
	"fmt"
	"os"
	"strconv"
	"strings"
)

// shortestExit возвращает наименьшее число шагов от 'S' до 'E' (4 направления), если можно войти не более чем в одну клетку '#'. Иначе -1.
func shortestExit(grid []string, n, m int) int {
	flat := strings.Join(grid, "")
	start := strings.IndexByte(flat, 'S')
	end := strings.IndexByte(flat, 'E')
	// состояние = (клетка, сколько стен уже пробито: 0 или 1), кодируем числом cell*2 + used
	dist := make([]int32, 2*n*m)
	for i := range dist {
		dist[i] = -1
	}
	queue := make([]int32, 0, 2*n*m)
	dist[start*2] = 0
	queue = append(queue, int32(start*2))
	dr := [4]int{1, -1, 0, 0}
	dc := [4]int{0, 0, 1, -1}
	for head := 0; head < len(queue); head++ {
		state := int(queue[head])
		cell, used := state>>1, state&1
		d := dist[state] + 1
		r, c := cell/m, cell%m
		for k := 0; k < 4; k++ {
			nr, nc := r+dr[k], c+dc[k]
			if nr < 0 || nr >= n || nc < 0 || nc >= m {
				continue
			}
			ncell := nr*m + nc
			nused := used
			if flat[ncell] == '#' {
				nused++
			}
			if nused > 1 {
				continue
			}
			nstate := ncell*2 + nused
			if dist[nstate] < 0 {
				dist[nstate] = d
				queue = append(queue, int32(nstate))
			}
		}
	}
	a, b := dist[end*2], dist[end*2+1]
	switch {
	case a < 0 && b < 0:
		return -1
	case a < 0:
		return int(b)
	case b < 0:
		return int(a)
	case a < b:
		return int(a)
	}
	return int(b)
}

func main() {
	sc := bufio.NewScanner(os.Stdin)
	sc.Buffer(make([]byte, 1<<20), 1<<26)
	sc.Split(bufio.ScanWords)
	sc.Scan()
	n, _ := strconv.Atoi(sc.Text())
	sc.Scan()
	m, _ := strconv.Atoi(sc.Text())
	grid := make([]string, n)
	for i := range grid {
		sc.Scan()
		grid[i] = sc.Text()
	}
	fmt.Println(shortestExit(grid, n, m))
}
