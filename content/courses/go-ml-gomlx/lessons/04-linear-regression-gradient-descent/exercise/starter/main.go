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

// step performs ONE gradient-descent step of y ~ w*x + b on the mean squared error.
// It returns the new w, the new b and the loss measured before the update.
func step(w, b, xs, ys, lr *graph.Node) []*graph.Node {
	pred := graph.Add(graph.Mul(xs, w), b)
	diff := graph.Sub(pred, ys)
	loss := graph.ReduceAllMean(graph.Square(diff))
	// TODO: grads := graph.Gradient(loss, w, b), then newW = w - lr*grads[0] and newB = b - lr*grads[1].
	return []*graph.Node{w, b, loss}
}

// closedForm solves the same problem with the normal equations (plain Go, float64) as a reference.
func closedForm(xs, ys []float32) (w, b float64) {
	n := float64(len(xs))
	var sx, sy, sxx, sxy float64
	for i := range xs {
		x, y := float64(xs[i]), float64(ys[i])
		sx, sy, sxx, sxy = sx+x, sy+y, sxx+x*x, sxy+x*y
	}
	w = (n*sxy - sx*sy) / (n*sxx - sx*sx)
	b = (sy - w*sx) / n
	return w, b
}

func main() {
	in := bufio.NewReader(os.Stdin)
	var lr float32
	var epochs int
	fmt.Fscan(in, &lr, &epochs)
	var xs, ys []float32
	for {
		var x, y float32
		if _, err := fmt.Fscan(in, &x, &y); err != nil {
			break
		}
		xs, ys = append(xs, x), append(ys, y)
	}

	backend, err := compute.NewWithConfig("go")
	if err != nil {
		fmt.Println("backend:", err)
		os.Exit(1)
	}
	train := graph.MustNewExec(backend, step)
	w, b := tensors.FromValue(float32(0)), tensors.FromValue(float32(0))
	x, y, rate := tensors.FromValue(xs), tensors.FromValue(ys), tensors.FromValue(lr)

	var loss *tensors.Tensor
	for epoch := 0; epoch < epochs; epoch++ {
		out := train.MustExec(w, b, x, y, rate)
		w, b, loss = out[0], out[1], out[2]
		if epoch == 0 {
			fmt.Printf("initial loss = %.3f\n", loss.Value())
		}
	}
	cw, cb := closedForm(xs, ys)
	fmt.Printf("closed form: w = %.3f b = %.3f\n", cw, cb)
	fmt.Printf("gradient descent: w = %.3f b = %.3f last loss = %.4f\n", w.Value(), b.Value(), loss.Value())
}
