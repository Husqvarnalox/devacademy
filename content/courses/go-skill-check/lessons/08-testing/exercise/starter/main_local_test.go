package main

import "testing"

// TestCompareVersionsLocal - ваш собственный табличный тест. Он запускается вместе с
// проверкой, но не оценивается: добавьте случаи, которые считаете важными.
func TestCompareVersionsLocal(t *testing.T) {
	cases := []struct {
		name string
		a, b string
		want int
	}{
		{"equal", "1.0.0", "1.0.0", 0},
		{"minor_numeric", "1.2.0", "1.10.0", -1},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			got, err := compareVersions(c.a, c.b)
			if err != nil || got != c.want {
				t.Errorf("compareVersions(%q, %q) = %d, %v; ожидалось %d, nil", c.a, c.b, got, err, c.want)
			}
		})
	}
}
