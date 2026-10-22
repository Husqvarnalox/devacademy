package main

import (
    "bufio"
    "fmt"
    "os"
)

func main() {
    in := bufio.NewReader(os.Stdin)
    var n int
    fmt.Fscan(in, &n)
    var sum, min, max int64
    // TODO: прочитайте n чисел типа int64 и найдите сумму, минимум и максимум
    fmt.Printf("sum=%d\nmin=%d\nmax=%d\navg=%.2f\n", sum, min, max, 0.0)
}
