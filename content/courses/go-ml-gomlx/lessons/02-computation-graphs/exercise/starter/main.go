package main

import (
	"bufio"
	"fmt"
	"os"
	"strings"

	"github.com/gomlx/compute"
	_ "github.com/gomlx/compute/gobackend"
	"github.com/gomlx/gomlx/core/graph"
	"github.com/gomlx/gomlx/core/tensors"
)

// standardize works on a [rows, cols] matrix and returns the per-column mean, the per-column
// population standard deviation (both with shape [1, cols]) and the standardized matrix.
func standardize(x *graph.Node) []*graph.Node {
	mean := graph.ReduceAndKeep(x, graph.ReduceMean, 0)
	// TODO: centered = x - mean; variance = mean(centered^2) per column; std = sqrt(variance);
	// z = centered / (std + 1e-6). Keep the [1, cols] shape of mean so that Sub and Div broadcast.
	centered := x
	std := graph.OnesLike(mean)
	z := centered
	return []*graph.Node{mean, std, z}
}

func readMatrix(in *bufio.Reader) [][]float32 {
	var rows, cols int
	fmt.Fscan(in, &rows, &cols)
	m := make([][]float32, rows)
	for i := range m {
		m[i] = make([]float32, cols)
		for j := range m[i] {
			fmt.Fscan(in, &m[i][j])
		}
	}
	return m
}

func format(values []float32) string {
	parts := make([]string, len(values))
	for i, v := range values {
		parts[i] = fmt.Sprintf("%.3f", v)
	}
	return strings.Join(parts, " ")
}

func main() {
	matrix := readMatrix(bufio.NewReader(os.Stdin))
	backend, err := compute.NewWithConfig("go")
	if err != nil {
		fmt.Println("backend:", err)
		os.Exit(1)
	}
	exec := graph.MustNewExec(backend, standardize)
	out := exec.MustExec(tensors.FromValue(matrix))
	fmt.Println("mean:", format(out[0].Value().([][]float32)[0]))
	fmt.Println("std:", format(out[1].Value().([][]float32)[0]))
	fmt.Println("z:")
	for _, row := range out[2].Value().([][]float32) {
		fmt.Println(" ", format(row))
	}
}
