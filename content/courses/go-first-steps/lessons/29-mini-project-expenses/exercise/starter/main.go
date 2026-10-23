package main

import (
	"bufio"
	"os"
)

func main() {
	sc := bufio.NewScanner(os.Stdin)
	for sc.Scan() {
		_ = sc.Text()
	}
}
