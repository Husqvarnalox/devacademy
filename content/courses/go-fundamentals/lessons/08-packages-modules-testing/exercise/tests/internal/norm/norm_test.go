package norm

import (
	"reflect"
	"testing"
)

func TestWords(t *testing.T) {
	tests := []struct {
		name string
		in   string
		want []string
	}{
		{"simple", "Hello World", []string{"hello", "world"}},
		{"punctuation", "Go, это - язык!", []string{"go", "это", "язык"}},
		{"digits", "Go 1.26 released", []string{"go", "1", "26", "released"}},
		{"cyrillic-upper", "ПРИВЕТ Мир", []string{"привет", "мир"}},
		{"only-separators", " ,.!- ", []string{}},
		{"empty", "", []string{}},
	}
	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			got := Words(tc.in)
			if len(got) == 0 && len(tc.want) == 0 {
				return
			}
			if !reflect.DeepEqual(got, tc.want) {
				t.Errorf("Words(%q) = %q, want %q", tc.in, got, tc.want)
			}
		})
	}
}
