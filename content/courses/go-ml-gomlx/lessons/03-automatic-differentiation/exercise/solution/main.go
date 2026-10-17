package main

import (
	"bufio"
	"fmt"
	"math"
	"os"

	"github.com/gomlx/compute"
	_ "github.com/gomlx/compute/gobackend"
	"github.com/gomlx/gomlx/core/graph"
)

// f(x, y) = sigmoid(x*y) + x^2
func f(x, y *graph.Node) *graph.Node {
	return graph.Add(graph.Sigmoid(graph.Mul(x, y)), graph.Square(x))
}

// valueAndGradient returns f and its partial derivatives with respect to x and y, computed by reverse-mode autodiff.
func valueAndGradient(x, y *graph.Node) []*graph.Node {
	out := f(x, y)
	grads := graph.Gradient(out, x, y)
	return []*graph.Node{out, grads[0], grads[1]}
}

func main() {
	backend, err := compute.NewWithConfig("go")
	if err != nil {
		fmt.Println("backend:", err)
		os.Exit(1)
	}
	auto := graph.MustNewExec(backend, valueAndGradient)
	forward := graph.MustNewExec(backend, f)
	eval := func(x, y float64) float64 { return forward.MustExec(x, y)[0].Value().(float64) }

	const h = 1e-5
	in := bufio.NewReader(os.Stdin)
	for {
		var x, y float64
		if _, err := fmt.Fscan(in, &x, &y); err != nil {
			break
		}
		out := auto.MustExec(x, y)
		value, gx, gy := out[0].Value().(float64), out[1].Value().(float64), out[2].Value().(float64)
		nx := (eval(x+h, y) - eval(x-h, y)) / (2 * h)
		ny := (eval(x, y+h) - eval(x, y-h)) / (2 * h)
		ok := math.Abs(gx-nx) < 1e-4 && math.Abs(gy-ny) < 1e-4
		fmt.Printf("f = %.4f grad = %.4f %.4f numeric = %.4f %.4f ok = %t\n", value, gx, gy, nx, ny, ok)
	}
}
