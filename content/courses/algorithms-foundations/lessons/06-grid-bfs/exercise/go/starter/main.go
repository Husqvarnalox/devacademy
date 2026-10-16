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
	// TODO: поиск в ширину по состояниям (клетка, использована ли уже пробивка стены), O(n * m)
	_, _, _ = grid, n, m
	return -1
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
