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
    for i := 0; i < n; i++ {
        var x int64
        fmt.Fscan(in, &x)
        sum += x
        if i == 0 || x < min {
            min = x
        }
        if i == 0 || x > max {
            max = x
        }
    }
    fmt.Printf("sum=%d\nmin=%d\nmax=%d\navg=%.2f\n", sum, min, max, float64(sum)/float64(n))
}
