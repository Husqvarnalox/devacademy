package main

import "testing"

func TestParseVersion(t *testing.T) {
	good := []struct {
		name                string
		in                  string
		major, minor, patch int
	}{
		{"plain", "1.2.3", 1, 2, 3},
		{"with_v", "v10.0.7", 10, 0, 7},
		{"zeros", "0.0.0", 0, 0, 0},
		{"multi_digit", "12.345.6789", 12, 345, 6789},
	}
	for _, c := range good {
		t.Run(c.name, func(t *testing.T) {
			a, b, p, err := parseVersion(c.in)
			if err != nil || a != c.major || b != c.minor || p != c.patch {
				t.Fatalf("parseVersion(%q) = %d, %d, %d, %v; ожидалось %d, %d, %d, nil", c.in, a, b, p, err, c.major, c.minor, c.patch)
			}
		})
	}
}

func TestParseVersionInvalid(t *testing.T) {
	for name, in := range map[string]string{
		"empty":      "",
		"two_parts":  "1.2",
		"four":       "1.2.3.4",
		"letters":    "1.x.3",
		"negative":   "1.-2.3",
		"empty_part": "1..3",
		"double_v":   "vv1.2.3",
	} {
		t.Run(name, func(t *testing.T) {
			a, b, p, err := parseVersion(in)
			if err == nil {
				t.Fatalf("parseVersion(%q): ожидалась ошибка", in)
			}
			if a != 0 || b != 0 || p != 0 {
				t.Fatalf("parseVersion(%q) при ошибке вернула %d, %d, %d, ожидались нули", in, a, b, p)
			}
		})
	}
}

func TestCompareVersions(t *testing.T) {
	cases := []struct {
		name string
		a, b string
		want int
	}{
		{"equal", "1.2.3", "1.2.3", 0},
		{"v_prefix_equal", "v1.2.3", "1.2.3", 0},
		{"patch_less", "1.2.3", "1.2.4", -1},
		{"minor_greater", "1.3.0", "1.2.9", 1},
		{"major_decides", "2.0.0", "1.99.99", 1},
		{"numeric_not_lexical", "1.2.0", "1.10.0", -1},
		{"numeric_not_lexical_reverse", "1.10.0", "1.9.0", 1},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			got, err := compareVersions(c.a, c.b)
			if err != nil || got != c.want {
				t.Fatalf("compareVersions(%q, %q) = %d, %v; ожидалось %d, nil", c.a, c.b, got, err, c.want)
			}
		})
	}
	t.Run("invalid_left", func(t *testing.T) {
		if _, err := compareVersions("1.x.0", "1.0.0"); err == nil {
			t.Fatal("ожидалась ошибка для некорректной левой версии")
		}
	})
	t.Run("invalid_right", func(t *testing.T) {
		if _, err := compareVersions("1.0.0", "bad"); err == nil {
			t.Fatal("ожидалась ошибка для некорректной правой версии")
		}
	})
}
