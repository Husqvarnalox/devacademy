package main

import (
	"errors"
	"math"
	"testing"
)

func near(a, b float64) bool { return math.Abs(a-b) < 1e-9 }

func TestShapes(t *testing.T) {
	t.Run("rect", func(t *testing.T) {
		var s Shape = Rect{W: 2, H: 3}
		if !near(s.Area(), 6) || s.Name() != "rect" {
			t.Fatalf("Rect{2,3}: Area=%v Name=%q, ожидалось 6 и rect", s.Area(), s.Name())
		}
	})
	t.Run("circle", func(t *testing.T) {
		var s Shape = Circle{R: 2}
		if !near(s.Area(), 4*math.Pi) || s.Name() != "circle" {
			t.Fatalf("Circle{2}: Area=%v Name=%q, ожидалось %v и circle", s.Area(), s.Name(), 4*math.Pi)
		}
	})
}

func TestTotalArea(t *testing.T) {
	t.Run("mixed", func(t *testing.T) {
		got := totalArea([]Shape{Rect{2, 3}, Circle{1}, Rect{1, 1}})
		if want := 7 + math.Pi; !near(got, want) {
			t.Fatalf("totalArea = %v, ожидалось %v", got, want)
		}
	})
	t.Run("empty", func(t *testing.T) {
		if got := totalArea(nil); got != 0 {
			t.Fatalf("totalArea(nil) = %v, ожидалось 0", got)
		}
	})
}

func TestDescribe(t *testing.T) {
	cases := []struct {
		name string
		in   any
		want string
	}{
		{"nil", nil, "nil"},
		{"rect", Rect{1, 2}, "shape rect"},
		{"circle", Circle{3}, "shape circle"},
		{"int", 42, "int 42"},
		{"string", "hi", "string \"hi\""},
		{"error", errors.New("boom"), "error: boom"},
		{"float", 1.5, "unknown"},
		{"slice", []int{1}, "unknown"},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			if got := describe(c.in); got != c.want {
				t.Fatalf("describe(%#v) = %q, ожидалось %q", c.in, got, c.want)
			}
		})
	}
}
