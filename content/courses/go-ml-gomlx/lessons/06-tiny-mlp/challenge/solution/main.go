package main

import (
	"fmt"
	"math"
	"math/rand"
	"os"

	"github.com/gomlx/compute"
	_ "github.com/gomlx/compute/gobackend"
	"github.com/gomlx/gomlx/core/graph"
	"github.com/gomlx/gomlx/core/tensors"
)

const (
	features     = 16
	hidden       = 64
	learningRate = 0.3
	epochs       = 1000
)

// dataset builds a deterministic synthetic problem. Only the first two features matter: the label is 1 when the
// point (x0, x1) lies outside the circle of radius 1.2, so no straight line separates the classes. The other
// 14 features are weak noise, and 10% of the labels are flipped so that overfitting becomes visible.
func dataset(rng *rand.Rand, n int) (xs [][]float32, ys [][]float32) {
	for i := 0; i < n; i++ {
		row := make([]float32, features)
		for j := range row {
			scale := 0.2
			if j < 2 {
				scale = 1
			}
			row[j] = float32(rng.NormFloat64() * scale)
		}
		label := float32(0)
		if row[0]*row[0]+row[1]*row[1] > 1.44 {
			label = 1
		}
		if rng.Float64() < 0.10 {
			label = 1 - label
		}
		xs = append(xs, row)
		ys = append(ys, []float32{label})
	}
	return xs, ys
}

// initWeights draws a [rows, cols] weight matrix with standard deviation 1/sqrt(rows).
func initWeights(rng *rand.Rand, rows, cols int) [][]float32 {
	w := make([][]float32, rows)
	for i := range w {
		w[i] = make([]float32, cols)
		for j := range w[i] {
			w[i][j] = float32(rng.NormFloat64() / math.Sqrt(float64(rows)))
		}
	}
	return w
}

// forward is the network 16 -> 64 -> 1: a tanh hidden layer followed by a linear output (the logits).
func forward(x, w1, b1, w2, b2 *graph.Node) *graph.Node {
	h := graph.Tanh(graph.Add(graph.MatMul(x, w1), b1))
	return graph.Add(graph.MatMul(h, w2), b2)
}

// bceLoss is the mean binary cross-entropy from logits: mean(softplus(z) - y*z).
func bceLoss(z, y *graph.Node) *graph.Node {
	return graph.ReduceAllMean(graph.Sub(graph.Softplus(z), graph.Mul(y, z)))
}

// lambda is the L2 strength read from stdin in main.
var lambda float64

func trainStep(x, y, w1, b1, w2, b2 *graph.Node) []*graph.Node {
	loss := bceLoss(forward(x, w1, b1, w2, b2), y)
	if lambda > 0 {
		penalty := graph.Add(graph.ReduceAllSum(graph.Square(w1)), graph.ReduceAllSum(graph.Square(w2)))
		loss = graph.Add(loss, graph.MulScalar(penalty, float32(lambda/2)))
	}
	g := graph.Gradient(loss, w1, b1, w2, b2)
	update := func(p, grad *graph.Node) *graph.Node {
		return graph.Sub(p, graph.MulScalar(grad, float32(learningRate)))
	}
	return []*graph.Node{update(w1, g[0]), update(b1, g[1]), update(w2, g[2]), update(b2, g[3])}
}

func evaluate(x, y, w1, b1, w2, b2 *graph.Node) []*graph.Node {
	z := forward(x, w1, b1, w2, b2)
	predicted := graph.ConvertDType(graph.GreaterThan(z, graph.ZerosLike(z)), y.DType())
	accuracy := graph.ReduceAllMean(graph.ConvertDType(graph.Equal(predicted, y), y.DType()))
	return []*graph.Node{bceLoss(z, y), accuracy}
}

func main() {
	fmt.Scan(&lambda)

	backend, err := compute.NewWithConfig("go")
	if err != nil {
		fmt.Println("backend:", err)
		os.Exit(1)
	}
	rng := rand.New(rand.NewSource(7))
	trainX, trainY := dataset(rng, 100)
	testX, testY := dataset(rng, 400)
	w1 := tensors.FromValue(initWeights(rng, features, hidden))
	b1 := tensors.FromValue([][]float32{make([]float32, hidden)})
	w2 := tensors.FromValue(initWeights(rng, hidden, 1))
	b2 := tensors.FromValue([][]float32{{0}})
	fmt.Printf("lambda = %g\n", lambda)

	step := graph.MustNewExec(backend, trainStep)
	tx, ty := tensors.FromValue(trainX), tensors.FromValue(trainY)
	for epoch := 0; epoch < epochs; epoch++ {
		out := step.MustExec(tx, ty, w1, b1, w2, b2)
		w1, b1, w2, b2 = out[0], out[1], out[2], out[3]
	}

	eval := graph.MustNewExec(backend, evaluate)
	train := eval.MustExec(tx, ty, w1, b1, w2, b2)
	test := eval.MustExec(tensors.FromValue(testX), tensors.FromValue(testY), w1, b1, w2, b2)
	fmt.Printf("train: loss = %.3f accuracy = %.3f\n", train[0].Value(), train[1].Value())
	fmt.Printf("test: loss = %.3f accuracy = %.3f\n", test[0].Value(), test[1].Value())
	norm := graph.MustNewExec(backend, func(a, b *graph.Node) *graph.Node {
		return graph.Sqrt(graph.Add(graph.ReduceAllSum(graph.Square(a)), graph.ReduceAllSum(graph.Square(b))))
	})
	fmt.Printf("weight norm = %.3f\n", norm.MustExec(w1, w2)[0].Value())
}
