package main

import (
	"strings"
	"testing"
)

func TestSumNumbers(t *testing.T) {
	good := []struct {
		name  string
		in    string
		sum   int64
		count int
	}{
		{"basic", "1 2 3\n4", 10, 4},
		{"empty", "", 0, 0},
		{"only_spaces", "  \n \n", 0, 0},
		{"negative", "-5 10 -2", 3, 3},
		{"beyond_32_bits", "3000000000 4000000000\n9000000000000000000", 9000000007000000000, 3},
	}
	for _, c := range good {
		t.Run(c.name, func(t *testing.T) {
			sum, count, err := sumNumbers(strings.NewReader(c.in))
			if err != nil {
				t.Fatalf("sumNumbers(%q): неожиданная ошибка %v", c.in, err)
			}
			if sum != c.sum || count != c.count {
				t.Fatalf("sumNumbers(%q) = %d, %d, ожидалось %d, %d", c.in, sum, count, c.sum, c.count)
			}
		})
	}
	bad := map[string]string{
		"word":     "1 x 3",
		"decimal":  "1 2.5",
		"overflow": "99999999999999999999",
	}
	for name, in := range bad {
		t.Run("error_"+name, func(t *testing.T) {
			if _, _, err := sumNumbers(strings.NewReader(in)); err == nil {
				t.Fatalf("sumNumbers(%q): ожидалась ошибка", in)
			}
		})
	}
}

func TestLineStats(t *testing.T) {
	cases := []struct {
		name                  string
		in                    string
		lines, words, longest int
	}{
		{"example", "hello world\nпривет\n\nlast line here", 4, 6, 14},
		{"empty", "", 0, 0, 0},
		{"crlf", "a b\r\nc\r\n", 2, 3, 3},
		{"blank_lines", "\n\n\n", 3, 0, 0},
		{"trailing_newline", "one two\n", 1, 2, 7},
		{"cyrillic_longest", "мир\nпривет", 2, 2, 6},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			lines, words, longest, err := lineStats(strings.NewReader(c.in))
			if err != nil {
				t.Fatalf("lineStats(%q): неожиданная ошибка %v", c.in, err)
			}
			if lines != c.lines || words != c.words || longest != c.longest {
				t.Fatalf("lineStats(%q) = %d, %d, %d; ожидалось %d, %d, %d", c.in, lines, words, longest, c.lines, c.words, c.longest)
			}
		})
	}
}

func TestLineStatsLongLine(t *testing.T) {
	long := strings.Repeat("я", 100000)
	lines, words, longest, err := lineStats(strings.NewReader(long + "\nshort"))
	if err != nil {
		t.Fatalf("длинная строка: неожиданная ошибка %v", err)
	}
	if lines != 2 || words != 2 || longest != 100000 {
		t.Fatalf("длинная строка: %d, %d, %d; ожидалось 2, 2, 100000", lines, words, longest)
	}
}
