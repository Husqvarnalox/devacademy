package main

import (
	"math"
	"testing"
)

func TestDigitSum(t *testing.T) {
	cases := []struct {
		name string
		n    int
		want int
	}{
		{"zero", 0, 0},
		{"one_digit", 7, 7},
		{"four_digits", 1234, 10},
		{"trailing_zeros", 1000, 1},
		{"nines", 999999, 54},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			if got := digitSum(c.n); got != c.want {
				t.Fatalf("digitSum(%d) = %d, ожидалось %d", c.n, got, c.want)
			}
		})
	}
}

func TestCollatzSteps(t *testing.T) {
	cases := []struct {
		name string
		n    int
		want int
	}{
		{"one", 1, 0},
		{"two", 2, 1},
		{"six", 6, 8},
		{"seven", 7, 16},
		{"twenty_seven", 27, 111},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			if got := collatzSteps(c.n); got != c.want {
				t.Fatalf("collatzSteps(%d) = %d, ожидалось %d", c.n, got, c.want)
			}
		})
	}
}

func TestClassify(t *testing.T) {
	cases := []struct {
		name string
		n    int
		want string
	}{
		{"negative_even", -4, "negative"},
		{"negative_odd", -3, "negative"},
		{"zero", 0, "zero"},
		{"even", 10, "even"},
		{"odd", 7, "odd"},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			if got := classify(c.n); got != c.want {
				t.Fatalf("classify(%d) = %q, ожидалось %q", c.n, got, c.want)
			}
		})
	}
}

func TestRatio(t *testing.T) {
	cases := []struct {
		name string
		a, b int
		want float64
	}{
		{"half", 1, 2, 0.5},
		{"mixed", 7, 2, 3.5},
		{"third", 1, 3, 1.0 / 3.0},
		{"zero_divisor", 5, 0, 0},
		{"negative", -1, 4, -0.25},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			if got := ratio(c.a, c.b); math.Abs(got-c.want) > 1e-9 {
				t.Fatalf("ratio(%d, %d) = %v, ожидалось %v", c.a, c.b, got, c.want)
			}
		})
	}
}
