package main

import "testing"

func TestMinMax(t *testing.T) {
	cases := []struct {
		name           string
		a, b, c        int
		wantLo, wantHi int
	}{
		{"ascending", 1, 2, 3, 1, 3},
		{"descending", 9, 5, 1, 1, 9},
		{"max_in_middle", 2, 8, 4, 2, 8},
		{"min_in_middle", 7, 1, 5, 1, 7},
		{"all_equal", 4, 4, 4, 4, 4},
		{"negatives", -3, -10, -1, -10, -1},
		{"mixed_signs", 5, -2, 0, -2, 5},
		{"two_equal_max", 6, 6, 1, 1, 6},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			lo, hi := minMax(c.a, c.b, c.c)
			if lo != c.wantLo || hi != c.wantHi {
				t.Fatalf("minMax(%d, %d, %d) = (%d, %d), ожидалось (%d, %d)", c.a, c.b, c.c, lo, hi, c.wantLo, c.wantHi)
			}
		})
	}
}

func TestDivMod(t *testing.T) {
	cases := []struct {
		name  string
		a, b  int
		wantQ int
		wantR int
	}{
		{"with_remainder", 17, 5, 3, 2},
		{"exact", 10, 5, 2, 0},
		{"dividend_smaller", 3, 7, 0, 3},
		{"zero_dividend", 0, 4, 0, 0},
		{"divide_by_one", 9, 1, 9, 0},
		{"negative_dividend", -7, 2, -3, -1},
		{"large", 1000003, 1000, 1000, 3},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			q, r := divMod(c.a, c.b)
			if q != c.wantQ || r != c.wantR {
				t.Fatalf("divMod(%d, %d) = (%d, %d), ожидалось (%d, %d)", c.a, c.b, q, r, c.wantQ, c.wantR)
			}
		})
	}
}
