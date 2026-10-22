package main

import (
	"fmt"
	"math"
)

// Shape - фигура с площадью и названием.
type Shape interface {
	Area() float64
	Name() string
}

// Rect - прямоугольник.
type Rect struct{ W, H float64 }

// Circle - круг.
type Circle struct{ R float64 }

// Area возвращает площадь прямоугольника.
func (r Rect) Area() float64 { return r.W * r.H }

// Name возвращает название фигуры.
func (r Rect) Name() string { return "rect" }

// Area возвращает площадь круга.
func (c Circle) Area() float64 { return math.Pi * c.R * c.R }

// Name возвращает название фигуры.
func (c Circle) Name() string { return "circle" }

// totalArea суммирует площади фигур.
func totalArea(shapes []Shape) float64 {
	total := 0.0
	for _, s := range shapes {
		total += s.Area()
	}
	return total
}

// describe описывает значение произвольного типа.
func describe(v any) string {
	switch x := v.(type) {
	case nil:
		return "nil"
	case Shape:
		return "shape " + x.Name()
	case int:
		return fmt.Sprintf("int %d", x)
	case string:
		return fmt.Sprintf("string %q", x)
	case error:
		return "error: " + x.Error()
	default:
		return "unknown"
	}
}

func main() {
	shapes := []Shape{Rect{2, 3}, Circle{1}}
	fmt.Printf("%.2f\n", totalArea(shapes))
	fmt.Println(describe(shapes[0]), describe(42), describe(nil))
}
