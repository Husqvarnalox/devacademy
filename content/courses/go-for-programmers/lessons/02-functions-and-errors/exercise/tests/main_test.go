package main

import (
	"errors"
	"testing"
)

func TestDivmod(t *testing.T) {
	cases := []struct {
		name    string
		a, b    int
		q, r    int
		wantErr bool
	}{
		{"basic", 17, 5, 3, 2, false},
		{"exact", 10, 5, 2, 0, false},
		{"negative", -7, 2, -3, -1, false},
		{"zero_dividend", 0, 3, 0, 0, false},
		{"zero_divisor", 5, 0, 0, 0, true},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			q, r, err := divmod(c.a, c.b)
			if c.wantErr {
				if !errors.Is(err, ErrDivByZero) {
					t.Fatalf("divmod(%d, %d): ошибка %v, ожидалась ErrDivByZero", c.a, c.b, err)
				}
				if q != 0 || r != 0 {
					t.Fatalf("divmod(%d, %d) при ошибке вернула %d, %d, ожидались нули", c.a, c.b, q, r)
				}
				return
			}
			if err != nil {
				t.Fatalf("divmod(%d, %d): неожиданная ошибка %v", c.a, c.b, err)
			}
			if q != c.q || r != c.r {
				t.Fatalf("divmod(%d, %d) = %d, %d, ожидалось %d, %d", c.a, c.b, q, r, c.q, c.r)
			}
		})
	}
}

func TestParseRange(t *testing.T) {
	cases := []struct {
		name   string
		in     string
		lo, hi int
	}{
		{"simple", "3-7", 3, 7},
		{"spaces", " 2 - 5 ", 2, 5},
		{"single", "5-5", 5, 5},
		{"zero", "0-0", 0, 0},
		{"wide", "10-1000", 10, 1000},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			lo, hi, err := parseRange(c.in)
			if err != nil {
				t.Fatalf("parseRange(%q): неожиданная ошибка %v", c.in, err)
			}
			if lo != c.lo || hi != c.hi {
				t.Fatalf("parseRange(%q) = %d, %d, ожидалось %d, %d", c.in, lo, hi, c.lo, c.hi)
			}
		})
	}
}

func TestParseRangeInvalid(t *testing.T) {
	inputs := map[string]string{
		"empty":      "",
		"one_part":   "7",
		"three":      "1-2-3",
		"letters":    "a-5",
		"missing_hi": "1-",
		"missing_lo": "-1",
		"reversed":   "7-3",
		"decimal":    "1.5-3",
	}
	for name, in := range inputs {
		t.Run(name, func(t *testing.T) {
			lo, hi, err := parseRange(in)
			if err == nil {
				t.Fatalf("parseRange(%q) вернула nil, ожидалась ошибка", in)
			}
			if lo != 0 || hi != 0 {
				t.Fatalf("parseRange(%q) при ошибке вернула %d, %d, ожидались нули", in, lo, hi)
			}
		})
	}
}

func TestSumRange(t *testing.T) {
	good := []struct {
		name string
		in   string
		want int
	}{
		{"one_to_four", "1-4", 10},
		{"single", "5-5", 5},
		{"with_zero", "0-3", 6},
		{"spaces", " 1 - 100 ", 5050},
	}
	for _, c := range good {
		t.Run(c.name, func(t *testing.T) {
			got, err := sumRange(c.in)
			if err != nil {
				t.Fatalf("sumRange(%q): неожиданная ошибка %v", c.in, err)
			}
			if got != c.want {
				t.Fatalf("sumRange(%q) = %d, ожидалось %d", c.in, got, c.want)
			}
		})
	}
	for _, in := range []string{"4-1", "x", "", "1-b"} {
		t.Run("error_"+in, func(t *testing.T) {
			got, err := sumRange(in)
			if err == nil || got != 0 {
				t.Fatalf("sumRange(%q) = %d, %v; ожидались 0 и ошибка", in, got, err)
			}
		})
	}
}
