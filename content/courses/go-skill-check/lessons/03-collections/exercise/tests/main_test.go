package main

import "testing"

func TestGroupByRuneLen(t *testing.T) {
	words := []string{"go", "я", "да", "hello", "мир", "ok"}
	got := groupByRuneLen(words)
	want := map[int][]string{1: {"я"}, 2: {"go", "да", "ok"}, 3: {"мир"}, 5: {"hello"}}
	t.Run("groups", func(t *testing.T) {
		if len(got) != len(want) {
			t.Fatalf("групп %d, ожидалось %d: %v", len(got), len(want), got)
		}
		for n, ws := range want {
			g := got[n]
			if len(g) != len(ws) {
				t.Fatalf("группа %d: %v, ожидалось %v", n, g, ws)
			}
			for i := range ws {
				if g[i] != ws[i] {
					t.Fatalf("группа %d: %v, ожидалось %v (порядок слов сохраняется)", n, g, ws)
				}
			}
		}
	})
	t.Run("empty", func(t *testing.T) {
		if got := groupByRuneLen(nil); len(got) != 0 {
			t.Fatalf("для пустого ввода ожидалась пустая карта, получено %v", got)
		}
	})
	t.Run("runes_not_bytes", func(t *testing.T) {
		got := groupByRuneLen([]string{"привет"})
		if len(got[6]) != 1 {
			t.Fatalf("«привет» должно попасть в группу 6 (символы), получено %v", got)
		}
	})
}

func TestChunk(t *testing.T) {
	cases := []struct {
		name  string
		items []int
		size  int
		want  [][]int
	}{
		{"even", []int{1, 2, 3, 4}, 2, [][]int{{1, 2}, {3, 4}}},
		{"remainder", []int{1, 2, 3, 4, 5}, 2, [][]int{{1, 2}, {3, 4}, {5}}},
		{"size_larger", []int{1, 2, 3}, 10, [][]int{{1, 2, 3}}},
		{"size_one", []int{7, 8}, 1, [][]int{{7}, {8}}},
		{"empty_items", []int{}, 3, [][]int{}},
		{"zero_size", []int{1, 2}, 0, [][]int{}},
		{"negative_size", []int{1, 2}, -2, [][]int{}},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			got := chunk(c.items, c.size)
			if len(got) != len(c.want) {
				t.Fatalf("chunk(%v, %d) = %v, ожидалось %v", c.items, c.size, got, c.want)
			}
			for i := range c.want {
				if len(got[i]) != len(c.want[i]) {
					t.Fatalf("chunk(%v, %d) = %v, ожидалось %v", c.items, c.size, got, c.want)
				}
				for j := range c.want[i] {
					if got[i][j] != c.want[i][j] {
						t.Fatalf("chunk(%v, %d) = %v, ожидалось %v", c.items, c.size, got, c.want)
					}
				}
			}
		})
	}
}
