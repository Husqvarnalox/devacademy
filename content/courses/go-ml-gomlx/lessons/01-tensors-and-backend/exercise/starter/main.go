package main

import (
	"bufio"
	"fmt"
	"os"

	"github.com/gomlx/compute"
	_ "github.com/gomlx/compute/gobackend"
	"github.com/gomlx/gomlx/core/graph"
	"github.com/gomlx/gomlx/core/tensors"
)

// poly builds the graph of y = 3x^2 - 2x + 1 for a whole tensor at once.
func poly(x *graph.Node) *graph.Node {
	// TODO: return 3*x^2 - 2*x + 1 using graph.Square, graph.MulScalar, graph.Sub and graph.AddScalar.
	return x
}

// evaluate returns y = poly(x) and sum(y), both computed by the graph.
func evaluate(backend compute.Backend, values []float32) (y []float32, sum float32) {
	exec := graph.MustNewExec(backend, func(x *graph.Node) (*graph.Node, *graph.Node) {
		y := poly(x)
		return y, graph.ReduceAllSum(y)
	})
	out := exec.MustExec(tensors.FromValue(values))
	return out[0].Value().([]float32), out[1].Value().(float32)
}

func main() {
	var values []float32
	in := bufio.NewReader(os.Stdin)
	for {
		var v float32
		if _, err := fmt.Fscan(in, &v); err != nil {
			break
		}
		values = append(values, v)
	}
	backend, err := compute.NewWithConfig("go")
	if err != nil {
		fmt.Println("backend:", err)
		os.Exit(1)
	}
	y, sum := evaluate(backend, values)
	fmt.Println("shape:", tensors.FromValue(values).Shape())
	for i, v := range y {
		fmt.Printf("y[%d] = %.3f\n", i, v)
	}
	fmt.Printf("sum = %.3f\n", sum)
}
