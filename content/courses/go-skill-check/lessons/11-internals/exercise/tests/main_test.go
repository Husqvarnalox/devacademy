package main

import "testing"

func equal(a, b []int) bool {
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

func TestWithAppended(t *testing.T) {
	t.Run("values", func(t *testing.T) {
		got := withAppended([]int{1, 2}, 3)
		if !equal(got, []int{1, 2, 3}) {
			t.Fatalf("withAppended = %v, ожидалось [1 2 3]", got)
		}
	})
	t.Run("nil_input", func(t *testing.T) {
		got := withAppended(nil, 7)
		if !equal(got, []int{7}) {
			t.Fatalf("withAppended(nil, 7) = %v, ожидалось [7]", got)
		}
	})
	t.Run("spare_capacity_untouched", func(t *testing.T) {
		s := make([]int, 2, 10)
		s[0], s[1] = 1, 2
		withAppended(s, 3)
		if s[:3][2] != 0 {
			t.Fatalf("withAppended записала в запас ёмкости исходного среза: s[2] = %d", s[:3][2])
		}
	})
	t.Run("independent_result", func(t *testing.T) {
		s := make([]int, 2, 10)
		s[0], s[1] = 1, 2
		r := withAppended(s, 3)
		r[0] = 99
		if s[0] != 1 {
			t.Fatalf("результат делит память с исходным срезом: s[0] = %d", s[0])
		}
	})
	t.Run("two_results_independent", func(t *testing.T) {
		s := make([]int, 1, 8)
		a := withAppended(s, 1)
		b := withAppended(s, 2)
		if a[1] != 1 || b[1] != 2 {
			t.Fatalf("результаты влияют друг на друга: %v %v", a, b)
		}
	})
}

func TestFirstN(t *testing.T) {
	t.Run("values", func(t *testing.T) {
		got := firstN([]int{1, 2, 3, 4}, 2)
		if !equal(got, []int{1, 2}) {
			t.Fatalf("firstN = %v, ожидалось [1 2]", got)
		}
	})
	t.Run("clamp_high", func(t *testing.T) {
		got := firstN([]int{1, 2}, 10)
		if !equal(got, []int{1, 2}) {
			t.Fatalf("firstN(.., 10) = %v, ожидалось [1 2]", got)
		}
	})
	t.Run("clamp_low", func(t *testing.T) {
		if got := firstN([]int{1, 2}, -3); len(got) != 0 {
			t.Fatalf("firstN(.., -3) = %v, ожидался пустой срез", got)
		}
	})
	t.Run("shares_memory", func(t *testing.T) {
		s := []int{1, 2, 3}
		firstN(s, 2)[0] = 100
		if s[0] != 100 {
			t.Fatalf("firstN должна делить память с исходным срезом: s[0] = %d", s[0])
		}
	})
	t.Run("capacity_limited", func(t *testing.T) {
		s := []int{1, 2, 3, 4}
		got := firstN(s, 2)
		if cap(got) != 2 {
			t.Fatalf("cap(firstN(s, 2)) = %d, ожидалось 2", cap(got))
		}
		_ = append(got, 99)
		if s[2] != 3 {
			t.Fatalf("append к результату затронул исходный срез: s[2] = %d", s[2])
		}
	})
}
