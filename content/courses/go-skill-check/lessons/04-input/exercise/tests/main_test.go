package main

import (
	"strings"
	"testing"
)

func TestTotalByName(t *testing.T) {
	good := []struct {
		name string
		in   string
		want map[string]int64
	}{
		{"basic", "anna 10\nboris 5\nanna 7\n", map[string]int64{"anna": 17, "boris": 5}},
		{"blank_lines", "\n\nx 1\n   \nx 2", map[string]int64{"x": 3}},
		{"empty", "", map[string]int64{}},
		{"cyrillic", "Аня 3\nАня -1", map[string]int64{"Аня": 2}},
		{"large", "a 5000000000\na 5000000000", map[string]int64{"a": 10000000000}},
		{"crlf", "a 1\r\nb 2\r\n", map[string]int64{"a": 1, "b": 2}},
	}
	for _, c := range good {
		t.Run(c.name, func(t *testing.T) {
			got, err := totalByName(strings.NewReader(c.in))
			if err != nil {
				t.Fatalf("totalByName(%q): неожиданная ошибка %v", c.in, err)
			}
			if len(got) != len(c.want) {
				t.Fatalf("totalByName(%q) = %v, ожидалось %v", c.in, got, c.want)
			}
			for k, v := range c.want {
				if got[k] != v {
					t.Fatalf("totalByName(%q) = %v, ожидалось %v", c.in, got, c.want)
				}
			}
		})
	}
}

func TestTotalByNameInvalid(t *testing.T) {
	for name, in := range map[string]string{
		"one_field":    "anna\n",
		"three_fields": "anna 1 2\n",
		"not_number":   "anna x\n",
		"decimal":      "anna 1.5\n",
		"late_error":   "a 1\nb 2\nc\n",
	} {
		t.Run(name, func(t *testing.T) {
			got, err := totalByName(strings.NewReader(in))
			if err == nil {
				t.Fatalf("totalByName(%q): ожидалась ошибка, получено %v", in, got)
			}
		})
	}
}
