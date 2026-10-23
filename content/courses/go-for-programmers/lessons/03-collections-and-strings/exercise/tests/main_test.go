package main

import "testing"

func sameMap(a, b map[string]int) bool {
	if len(a) != len(b) {
		return false
	}
	for k, v := range b {
		if got, ok := a[k]; !ok || got != v {
			return false
		}
	}
	return true
}

func sameStrings(a, b []string) bool {
	if len(a) != len(b) {
		return false
	}
	for i := range a {
		if a[i] != b[i] {
			return false
		}
	}
	return true
}

func TestWordFreq(t *testing.T) {
	cases := []struct {
		name string
		in   string
		want map[string]int
	}{
		{"mixed_case", "Go go GO, go!", map[string]int{"go": 4}},
		{"cyrillic", "Привет, мир! привет", map[string]int{"привет": 2, "мир": 1}},
		{"empty", "", map[string]int{}},
		{"only_punctuation", "... !!! ,", map[string]int{}},
		{"quotes", "\"a\" (b) a;", map[string]int{"a": 2, "b": 1}},
		{"newlines", "x\ny\n\nx", map[string]int{"x": 2, "y": 1}},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			if got := wordFreq(c.in); !sameMap(got, c.want) {
				t.Fatalf("wordFreq(%q) = %v, ожидалось %v", c.in, got, c.want)
			}
		})
	}
}

func TestTopWords(t *testing.T) {
	freq := map[string]int{"b": 2, "a": 2, "c": 5, "d": 1}
	cases := []struct {
		name string
		freq map[string]int
		n    int
		want []string
	}{
		{"top_three", freq, 3, []string{"c", "a", "b"}},
		{"ties_alphabetical", freq, 2, []string{"c", "a"}},
		{"n_larger", freq, 10, []string{"c", "a", "b", "d"}},
		{"n_zero", freq, 0, []string{}},
		{"n_negative", freq, -1, []string{}},
		{"empty_map", map[string]int{}, 3, []string{}},
		{"nil_map", nil, 3, []string{}},
		{"cyrillic", map[string]int{"яблоко": 1, "арбуз": 1}, 2, []string{"арбуз", "яблоко"}},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			if got := topWords(c.freq, c.n); !sameStrings(got, c.want) {
				t.Fatalf("topWords(%v, %d) = %v, ожидалось %v", c.freq, c.n, got, c.want)
			}
		})
	}
}

func TestReverseRunes(t *testing.T) {
	cases := []struct {
		name, in, want string
	}{
		{"ascii", "abc", "cba"},
		{"empty", "", ""},
		{"cyrillic", "привет", "тевирп"},
		{"mixed", "Go, мир", "рим ,oG"},
		{"single", "я", "я"},
		{"accent", "héllo", "olléh"},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			if got := reverseRunes(c.in); got != c.want {
				t.Fatalf("reverseRunes(%q) = %q, ожидалось %q", c.in, got, c.want)
			}
		})
	}
}
