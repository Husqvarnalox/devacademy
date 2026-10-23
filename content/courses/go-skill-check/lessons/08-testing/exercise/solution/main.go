package main

import (
	"fmt"
	"strconv"
	"strings"
)

// parseVersion разбирает версию "1.2.3" или "v1.2.3".
func parseVersion(s string) (major, minor, patch int, err error) {
	s = strings.TrimPrefix(s, "v")
	parts := strings.Split(s, ".")
	if len(parts) != 3 {
		return 0, 0, 0, fmt.Errorf("parseVersion %q: ожидалось три части", s)
	}
	nums := make([]int, 3)
	for i, p := range parts {
		n, err := strconv.Atoi(p)
		if err != nil || n < 0 || strings.HasPrefix(p, "+") {
			return 0, 0, 0, fmt.Errorf("parseVersion %q: неверная часть %q", s, p)
		}
		nums[i] = n
	}
	return nums[0], nums[1], nums[2], nil
}

// compareVersions возвращает -1, 0 или 1 для a < b, a == b, a > b.
func compareVersions(a, b string) (int, error) {
	a1, a2, a3, err := parseVersion(a)
	if err != nil {
		return 0, err
	}
	b1, b2, b3, err := parseVersion(b)
	if err != nil {
		return 0, err
	}
	left, right := [3]int{a1, a2, a3}, [3]int{b1, b2, b3}
	for i := range left {
		switch {
		case left[i] < right[i]:
			return -1, nil
		case left[i] > right[i]:
			return 1, nil
		}
	}
	return 0, nil
}

func main() {
	fmt.Println(compareVersions("1.2.3", "v1.10.0"))
}
