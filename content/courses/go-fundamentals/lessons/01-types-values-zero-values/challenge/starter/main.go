package main

import (
	"fmt"
	"io"
	"os"
)

func main() {
	data, _ := io.ReadAll(os.Stdin)
	text := string(data)
	counts := map[string]int{}
	// TODO: цикл идёт по байтам, поэтому многобайтные символы разваливаются,
	// а пробельные символы тоже попадают в счётчик
	for i := 0; i < len(text); i++ {
		counts[string(text[i])]++
	}
	// TODO: порядок обхода карты случаен; нужна сортировка:
	// сначала по убыванию количества, при равенстве - по возрастанию руны
	for k, n := range counts {
		fmt.Printf("%s %d\n", k, n)
	}
}
