package main

import (
	"errors"
	"testing"
)

func TestSafeDivide(t *testing.T) {
	cases := []struct {
		name    string
		a, b    int
		want    int
		wantErr bool
	}{
		{"basic", 7, 2, 3, false},
		{"negative", -9, 3, -3, false},
		{"zero_dividend", 0, 5, 0, false},
		{"zero_divisor", 5, 0, 0, true},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			got, err := safeDivide(c.a, c.b)
			if c.wantErr {
				if !errors.Is(err, ErrZero) || got != 0 {
					t.Fatalf("safeDivide(%d, %d) = %d, %v; ожидались 0 и ErrZero", c.a, c.b, got, err)
				}
				return
			}
			if err != nil || got != c.want {
				t.Fatalf("safeDivide(%d, %d) = %d, %v; ожидалось %d, nil", c.a, c.b, got, err, c.want)
			}
		})
	}
}

func TestParseKV(t *testing.T) {
	good := []struct {
		name string
		in   string
		key  string
		val  int
	}{
		{"simple", "age=30", "age", 30},
		{"spaces", " width = 5 ", "width", 5},
		{"negative", "delta=-4", "delta", -4},
		{"cyrillic_key", "возраст=7", "возраст", 7},
		{"zero", "n=0", "n", 0},
	}
	for _, c := range good {
		t.Run(c.name, func(t *testing.T) {
			k, v, err := parseKV(c.in)
			if err != nil || k != c.key || v != c.val {
				t.Fatalf("parseKV(%q) = %q, %d, %v; ожидалось %q, %d, nil", c.in, k, v, err, c.key, c.val)
			}
		})
	}
}

func TestParseKVInvalid(t *testing.T) {
	for name, in := range map[string]string{
		"no_equals":   "age",
		"empty_key":   "=5",
		"blank_key":   "  =5",
		"not_number":  "a=b",
		"empty_value": "a=",
		"empty":       "",
	} {
		t.Run(name, func(t *testing.T) {
			k, v, err := parseKV(in)
			if err == nil {
				t.Fatalf("parseKV(%q): ожидалась ошибка", in)
			}
			if k != "" || v != 0 {
				t.Fatalf("parseKV(%q) при ошибке вернула %q, %d, ожидались \"\" и 0", in, k, v)
			}
		})
	}
}
