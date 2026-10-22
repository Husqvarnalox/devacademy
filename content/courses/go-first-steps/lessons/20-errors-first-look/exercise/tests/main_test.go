package main

import "testing"

func TestParseAgeValid(t *testing.T) {
	cases := []struct {
		name  string
		input string
		want  int
	}{
		{"typical", "25", 25},
		{"zero", "0", 0},
		{"upper_bound", "150", 150},
		{"one_digit", "7", 7},
		{"leading_zeros", "007", 7},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			got, err := parseAge(c.input)
			if err != nil {
				t.Fatalf("parseAge(%q): неожиданная ошибка: %v", c.input, err)
			}
			if got != c.want {
				t.Fatalf("parseAge(%q) = %d, ожидалось %d", c.input, got, c.want)
			}
		})
	}
}

func TestParseAgeInvalid(t *testing.T) {
	cases := []struct {
		name  string
		input string
	}{
		{"letters", "abc"},
		{"empty", ""},
		{"negative", "-1"},
		{"too_old", "151"},
		{"far_too_old", "1000"},
		{"decimal", "12.5"},
		{"with_space", " 7"},
		{"overflow", "99999999999999999999"},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			got, err := parseAge(c.input)
			if err == nil {
				t.Fatalf("parseAge(%q) вернула ошибку nil, ожидалась ошибка", c.input)
			}
			if got != 0 {
				t.Fatalf("parseAge(%q) при ошибке вернула %d, ожидался 0", c.input, got)
			}
			if err.Error() == "" {
				t.Fatalf("parseAge(%q): текст ошибки пуст", c.input)
			}
		})
	}
}
