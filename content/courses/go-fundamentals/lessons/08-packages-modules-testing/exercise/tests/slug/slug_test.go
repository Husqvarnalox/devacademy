package slug

import "testing"

func TestSlugify(t *testing.T) {
	tests := []struct {
		name, in, want string
	}{
		{"simple", "Hello World", "hello-world"},
		{"punctuation", "Go: язык и идиомы!", "go-язык-и-идиомы"},
		{"extra-spaces", "  many   spaces  ", "many-spaces"},
		{"digits", "Release 1.26", "release-1-26"},
		{"empty", "", ""},
		{"only-symbols", "***", ""},
	}
	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			if got := Slugify(tc.in); got != tc.want {
				t.Errorf("Slugify(%q) = %q, want %q", tc.in, got, tc.want)
			}
		})
	}
}

func TestUnique(t *testing.T) {
	t.Run("free", func(t *testing.T) {
		if got := Unique("post", map[string]bool{}); got != "post" {
			t.Errorf("got %q", got)
		}
	})
	t.Run("taken", func(t *testing.T) {
		if got := Unique("post", map[string]bool{"post": true}); got != "post-2" {
			t.Errorf("got %q", got)
		}
	})
	t.Run("skips-taken-suffixes", func(t *testing.T) {
		taken := map[string]bool{"post": true, "post-2": true, "post-3": true}
		if got := Unique("post", taken); got != "post-4" {
			t.Errorf("got %q", got)
		}
	})
	t.Run("empty-base", func(t *testing.T) {
		if got := Unique("", map[string]bool{}); got != "item" {
			t.Errorf("got %q", got)
		}
		if got := Unique("", map[string]bool{"item": true}); got != "item-2" {
			t.Errorf("got %q", got)
		}
	})
	t.Run("nil-map", func(t *testing.T) {
		if got := Unique("x", nil); got != "x" {
			t.Errorf("got %q", got)
		}
	})
	t.Run("does-not-modify", func(t *testing.T) {
		taken := map[string]bool{"a": true}
		_ = Unique("a", taken)
		if len(taken) != 1 {
			t.Errorf("Unique изменила карту: %v", taken)
		}
	})
}

func BenchmarkSlugify(b *testing.B) {
	for b.Loop() {
		Slugify("Go: язык и идиомы 1.26")
	}
}
