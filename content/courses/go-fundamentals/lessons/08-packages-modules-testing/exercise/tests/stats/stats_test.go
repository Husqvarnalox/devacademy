package stats

import (
	"reflect"
	"testing"
)

func TestWordCount(t *testing.T) {
	t.Run("basic", func(t *testing.T) {
		got := WordCount("a b a. A!")
		want := map[string]int{"a": 3, "b": 1}
		if !reflect.DeepEqual(got, want) {
			t.Errorf("got %v, want %v", got, want)
		}
	})
	t.Run("empty-not-nil", func(t *testing.T) {
		got := WordCount("")
		if got == nil || len(got) != 0 {
			t.Errorf("got %#v, want пустую не-nil карту", got)
		}
	})
	t.Run("cyrillic", func(t *testing.T) {
		got := WordCount("Мир мир МИР")
		if got["мир"] != 3 {
			t.Errorf("got %v", got)
		}
	})
}

func TestTop(t *testing.T) {
	counts := map[string]int{"b": 2, "a": 2, "c": 5, "d": 1}
	t.Run("order", func(t *testing.T) {
		got := Top(counts, 3)
		want := []Entry{{"c", 5}, {"a", 2}, {"b", 2}}
		if !reflect.DeepEqual(got, want) {
			t.Errorf("got %v, want %v", got, want)
		}
	})
	t.Run("n-larger-than-len", func(t *testing.T) {
		if got := Top(counts, 10); len(got) != 4 {
			t.Errorf("len = %d, want 4", len(got))
		}
	})
	t.Run("n-nonpositive", func(t *testing.T) {
		for _, n := range []int{0, -3} {
			if got := Top(counts, n); len(got) != 0 {
				t.Errorf("Top(n=%d) = %v", n, got)
			}
		}
	})
	t.Run("empty-map", func(t *testing.T) {
		if got := Top(nil, 3); len(got) != 0 {
			t.Errorf("got %v", got)
		}
	})
	t.Run("does-not-modify", func(t *testing.T) {
		m := map[string]int{"x": 1}
		Top(m, 1)
		if len(m) != 1 || m["x"] != 1 {
			t.Errorf("карта изменена: %v", m)
		}
	})
}
