package main

import "testing"

func TestSumMultiples(t *testing.T) {
	cases := []struct {
		name string
		n, k int
		want int
	}{
		{"ten_by_three", 10, 3, 18},
		{"no_multiples", 5, 7, 0},
		{"every_number", 6, 1, 21},
		{"zero_limit", 0, 3, 0},
		{"n_is_multiple", 12, 4, 24},
		{"single", 1, 1, 1},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			got := sumMultiples(c.n, c.k)
			if got != c.want {
				t.Fatalf("sumMultiples(%d, %d) = %d, ожидалось %d", c.n, c.k, got, c.want)
			}
		})
	}
}

func TestSign(t *testing.T) {
	cases := []struct {
		name string
		n    int
		want string
	}{
		{"negative", -5, "отрицательное"},
		{"zero", 0, "ноль"},
		{"positive", 7, "положительное"},
		{"minus_one", -1, "отрицательное"},
		{"one", 1, "положительное"},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			got := sign(c.n)
			if got != c.want {
				t.Fatalf("sign(%d) = %q, ожидалось %q", c.n, got, c.want)
			}
		})
	}
}
