package textstat

import (
	"reflect"
	"slices"
	"testing"
)

func TestWordFreq(t *testing.T) {
	t.Run("basic", func(t *testing.T) {
		got := WordFreq("Go go GO, gopher! go.")
		want := map[string]int{"go": 4, "gopher": 1}
		if !reflect.DeepEqual(got, want) {
			t.Fatalf("got %v, want %v", got, want)
		}
	})
	t.Run("empty", func(t *testing.T) {
		got := WordFreq("  ... !!  ")
		if got == nil || len(got) != 0 {
			t.Fatalf("got %#v, want пустая не-nil карта", got)
		}
	})
	t.Run("cyrillic", func(t *testing.T) {
		got := WordFreq("Мир, мир; ПРИВЕТ мир")
		want := map[string]int{"мир": 3, "привет": 1}
		if !reflect.DeepEqual(got, want) {
			t.Fatalf("got %v, want %v", got, want)
		}
	})
}

func TestTopWords(t *testing.T) {
	freq := map[string]int{"b": 2, "a": 2, "c": 5, "d": 1, "e": 1}
	t.Run("order", func(t *testing.T) {
		got := TopWords(freq, 3)
		if !slices.Equal(got, []string{"c", "a", "b"}) {
			t.Fatalf("got %v", got)
		}
	})
	t.Run("limit-larger", func(t *testing.T) {
		got := TopWords(freq, 100)
		if !slices.Equal(got, []string{"c", "a", "b", "d", "e"}) {
			t.Fatalf("got %v", got)
		}
	})
	t.Run("zero-and-negative", func(t *testing.T) {
		if got := TopWords(freq, 0); len(got) != 0 {
			t.Fatalf("n=0: got %v", got)
		}
		if got := TopWords(freq, -2); len(got) != 0 {
			t.Fatalf("n<0: got %v", got)
		}
	})
	t.Run("stable-across-runs", func(t *testing.T) {
		for i := 0; i < 50; i++ {
			if got := TopWords(freq, 5); !slices.Equal(got, []string{"c", "a", "b", "d", "e"}) {
				t.Fatalf("run %d: got %v", i, got)
			}
		}
	})
}

func TestChunk(t *testing.T) {
	t.Run("even", func(t *testing.T) {
		got := Chunk([]int{1, 2, 3, 4}, 2)
		if !reflect.DeepEqual(got, [][]int{{1, 2}, {3, 4}}) {
			t.Fatalf("got %v", got)
		}
	})
	t.Run("uneven", func(t *testing.T) {
		got := Chunk([]int{1, 2, 3, 4, 5}, 2)
		if !reflect.DeepEqual(got, [][]int{{1, 2}, {3, 4}, {5}}) {
			t.Fatalf("got %v", got)
		}
	})
	t.Run("append-isolated", func(t *testing.T) {
		src := []int{1, 2, 3, 4}
		got := Chunk(src, 2)
		got[0] = append(got[0], 99)
		if got[1][0] != 3 || src[2] != 3 {
			t.Fatalf("append к первой части затёр соседнюю: %v, src=%v", got, src)
		}
		if cap(got[1]) != len(got[1]) {
			t.Fatalf("cap=%d, ожидалась ёмкость, равная длине", cap(got[1]))
		}
	})
	t.Run("bad-size", func(t *testing.T) {
		if Chunk([]int{1}, 0) != nil || Chunk([]int{1}, -1) != nil {
			t.Fatal("size <= 0 должен давать nil")
		}
	})
	t.Run("empty", func(t *testing.T) {
		if got := Chunk(nil, 3); len(got) != 0 {
			t.Fatalf("got %v", got)
		}
	})
}

func TestInvert(t *testing.T) {
	got := Invert(map[string]int{"b": 1, "a": 1, "z": 2, "y": 1})
	want := map[int][]string{1: {"a", "b", "y"}, 2: {"z"}}
	for i := 0; i < 20; i++ {
		got = Invert(map[string]int{"b": 1, "a": 1, "z": 2, "y": 1})
		if !reflect.DeepEqual(got, want) {
			t.Fatalf("run %d: got %v, want %v", i, got, want)
		}
	}
}

func TestDedup(t *testing.T) {
	t.Run("order", func(t *testing.T) {
		got := Dedup([]string{"b", "a", "b", "c", "a"})
		if !slices.Equal(got, []string{"b", "a", "c"}) {
			t.Fatalf("got %v", got)
		}
	})
	t.Run("no-alias", func(t *testing.T) {
		in := []string{"x", "y", "x"}
		got := Dedup(in)
		got[0] = "changed"
		if in[0] != "x" {
			t.Fatalf("результат делит массив с входом: %v", in)
		}
	})
	t.Run("empty", func(t *testing.T) {
		if got := Dedup(nil); len(got) != 0 {
			t.Fatalf("got %v", got)
		}
	})
}
