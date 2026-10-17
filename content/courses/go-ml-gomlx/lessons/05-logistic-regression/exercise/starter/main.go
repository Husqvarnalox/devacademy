package main

import (
	"fmt"
	"math/rand"
	"os"

	"github.com/gomlx/compute"
	_ "github.com/gomlx/compute/gobackend"
	"github.com/gomlx/gomlx/core/graph"
	"github.com/gomlx/gomlx/core/tensors"
)

// dataset returns two overlapping Gaussian clouds: class 0 around (-1, -1) and class 1 around (1, 1).
// The generator is seeded, so every run sees exactly the same points.
func dataset() (xs [][]float32, ys []float32) {
	rng := rand.New(rand.NewSource(42))
	for i := 0; i < 100; i++ {
		class := float32(i % 2)
		center := float32(2*class - 1)
		xs = append(xs, []float32{center + float32(rng.NormFloat64()), center + float32(rng.NormFloat64())})
		ys = append(ys, class)
	}
	return xs, ys
}

// logits computes z = X w + b for X of shape [n, 2], w of shape [2] and a scalar b.
func logits(x, w, b *graph.Node) *graph.Node {
	return graph.Add(graph.Dot(x, w).Product(), b)
}

// bceLoss is the loss of the model. The starter uses the squared error of the probability sigmoid(z):
// replace it with the binary cross-entropy (see the lesson), preferably from logits in the stable form
// mean(softplus(z) - y*z).
func bceLoss(z, y *graph.Node) *graph.Node {
	return graph.ReduceAllMean(graph.Square(graph.Sub(graph.Sigmoid(z), y)))
}

func step(x, y, w, b, lr *graph.Node) []*graph.Node {
	loss := bceLoss(logits(x, w, b), y)
	grads := graph.Gradient(loss, w, b)
	return []*graph.Node{graph.Sub(w, graph.Mul(lr, grads[0])), graph.Sub(b, graph.Mul(lr, grads[1])), loss}
}

// evaluate returns the loss and the accuracy (fraction of points with z > 0 equal to the label).
func evaluate(x, y, w, b *graph.Node) []*graph.Node {
	z := logits(x, w, b)
	predicted := graph.ConvertDType(graph.GreaterThan(z, graph.ZerosLike(z)), y.DType())
	accuracy := graph.ReduceAllMean(graph.ConvertDType(graph.Equal(predicted, y), y.DType()))
	return []*graph.Node{bceLoss(z, y), accuracy}
}

func main() {
	var lr float32
	var epochs int
	fmt.Scan(&lr, &epochs)

	backend, err := compute.NewWithConfig("go")
	if err != nil {
		fmt.Println("backend:", err)
		os.Exit(1)
	}
	xs, ys := dataset()
	x, y, rate := tensors.FromValue(xs), tensors.FromValue(ys), tensors.FromValue(lr)
	w, b := tensors.FromValue([]float32{0, 0}), tensors.FromValue(float32(0))

	train := graph.MustNewExec(backend, step)
	for epoch := 0; epoch < epochs; epoch++ {
		out := train.MustExec(x, y, w, b, rate)
		w, b = out[0], out[1]
		if epoch == 0 {
			fmt.Printf("initial loss = %.3f\n", out[2].Value())
		}
	}
	result := graph.MustNewExec(backend, evaluate).MustExec(x, y, w, b)
	weights := w.Value().([]float32)
	fmt.Printf("loss = %.3f accuracy = %.3f\n", result[0].Value(), result[1].Value())
	fmt.Printf("w = %.3f %.3f b = %.3f\n", weights[0], weights[1], b.Value())
}
