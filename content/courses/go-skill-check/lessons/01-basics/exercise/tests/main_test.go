package main

import "testing"

func TestDaysInMonth(t *testing.T) {
	cases := []struct {
		name        string
		year, month int
		want        int
	}{
		{"leap_feb", 2024, 2, 29},
		{"common_feb", 2023, 2, 28},
		{"century_feb", 1900, 2, 28},
		{"four_hundred_feb", 2000, 2, 29},
		{"april", 2023, 4, 30},
		{"december", 2023, 12, 31},
		{"invalid_high", 2023, 13, 0},
		{"invalid_zero", 2023, 0, 0},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			if got := daysInMonth(c.year, c.month); got != c.want {
				t.Fatalf("daysInMonth(%d, %d) = %d, ожидалось %d", c.year, c.month, got, c.want)
			}
		})
	}
}

func TestIsPrime(t *testing.T) {
	cases := []struct {
		name string
		n    int
		want bool
	}{
		{"zero", 0, false},
		{"one", 1, false},
		{"two", 2, true},
		{"nine", 9, false},
		{"ninety_seven", 97, true},
		{"negative", -7, false},
		{"large_prime", 7919, true},
		{"square_of_prime", 7921, false},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			if got := isPrime(c.n); got != c.want {
				t.Fatalf("isPrime(%d) = %v, ожидалось %v", c.n, got, c.want)
			}
		})
	}
}
