package main

import "testing"

func TestByteAndRuneCount(t *testing.T) {
	cases := []struct {
		name      string
		in        string
		wantBytes int
		wantRunes int
	}{
		{"latin", "hello", 5, 5},
		{"cyrillic", "привет", 12, 6},
		{"empty", "", 0, 0},
		{"mixed", "Go и Я", 8, 6},
		{"emoji", "a😀", 5, 2},
		{"yo", "ёж", 4, 2},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			b, r := byteAndRuneCount(c.in)
			if b != c.wantBytes || r != c.wantRunes {
				t.Fatalf("byteAndRuneCount(%q) = (%d, %d), ожидалось (%d, %d)", c.in, b, r, c.wantBytes, c.wantRunes)
			}
		})
	}
}

func TestReverse(t *testing.T) {
	cases := []struct {
		name string
		in   string
		want string
	}{
		{"latin", "abc", "cba"},
		{"cyrillic", "привет", "тевирп"},
		{"empty", "", ""},
		{"single", "я", "я"},
		{"palindrome", "шалаш", "шалаш"},
		{"mixed", "Go-го", "ог-oG"},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			got := reverse(c.in)
			if got != c.want {
				t.Fatalf("reverse(%q) = %q, ожидалось %q", c.in, got, c.want)
			}
		})
	}
}

func TestCapitalize(t *testing.T) {
	cases := []struct {
		name string
		in   string
		want string
	}{
		{"latin", "gOLANG", "Golang"},
		{"cyrillic", "пРИВЕТ", "Привет"},
		{"already_ok", "Москва", "Москва"},
		{"empty", "", ""},
		{"single_letter", "я", "Я"},
		{"yo", "ёЛКА", "Ёлка"},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			got := capitalize(c.in)
			if got != c.want {
				t.Fatalf("capitalize(%q) = %q, ожидалось %q", c.in, got, c.want)
			}
		})
	}
}
