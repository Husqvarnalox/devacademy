package main

import (
	"bufio"
	"fmt"
	"os"
)

func main() {
	sc := bufio.NewScanner(os.Stdin)
	for sc.Scan() {
		line := sc.Text()
		// Разберите строку на слова и посчитайте средний балл ученика
		fmt.Println(line)
	}
}
