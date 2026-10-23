package main

import "fmt"

// Shape - фигура с площадью и названием.
type Shape interface {
	Area() float64
	Name() string
}

// Rect - прямоугольник.
type Rect struct{ W, H float64 }

// Circle - круг.
type Circle struct{ R float64 }

// Area и Name для Rect: площадь W*H, название "rect".
func (r Rect) Area() float64 { return 0 }
func (r Rect) Name() string  { return "" }

// Area и Name для Circle: площадь π*R², название "circle" (math.Pi из пакета math).
func (c Circle) Area() float64 { return 0 }
func (c Circle) Name() string  { return "" }

// totalArea суммирует площади фигур.
func totalArea(shapes []Shape) float64 {
	return 0
}

// describe описывает значение произвольного типа через type switch:
//
//	nil -> "nil"; Shape -> "shape " + Name(); int -> "int 42";
//	string -> `string "hi"` (через %q); error -> "error: " + текст; иначе "unknown".
func describe(v any) string {
	return ""
}

func main() {
	shapes := []Shape{Rect{2, 3}, Circle{1}}
	fmt.Printf("%.2f\n", totalArea(shapes))
	fmt.Println(describe(shapes[0]), describe(42), describe(nil))
}
