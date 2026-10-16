package main

import (
    "bufio"
    "fmt"
    "os"
    "strings"
)

func without(s []int, i int) []int {
    // TODO: верните новый срез без элемента i, не изменяя s
    return append(s[:i], s[i+1:]...)
}

func join(s []int) string {
    parts := make([]string, len(s))
    for i, v := range s {
        parts[i] = fmt.Sprint(v)
    }
    return strings.Join(parts, " ")
}

func main() {
    in := bufio.NewReader(os.Stdin)
    var n int
    fmt.Fscan(in, &n)
    s := make([]int, n)
    for i := range s {
        fmt.Fscan(in, &s[i])
    }
    var idx int
    fmt.Fscan(in, &idx)
    r := without(s, idx)
    fmt.Println("result: " + join(r))
    fmt.Println("original: " + join(s))
}
