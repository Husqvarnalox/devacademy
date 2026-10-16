package main

import (
	"bufio"
	"errors"
	"fmt"
	"os"
	"strconv"
	"strings"
)

var (
	ErrDivZero   = errors.New("division by zero")
	ErrUnknownOp = errors.New("unknown operator")
)

// SyntaxError - типизированная ошибка: несёт данные о том, что именно не так.
type SyntaxError struct {
	Line   string
	Reason string
}

func (e *SyntaxError) Error() string {
	return fmt.Sprintf("syntax error in %q: %s", e.Line, e.Reason)
}

// Eval вычисляет выражение вида "a op b".
func Eval(line string) (int, error) {
	f := strings.Fields(line)
	if len(f) != 3 {
		return 0, &SyntaxError{Line: line, Reason: "want: a op b"}
	}
	a, err := strconv.Atoi(f[0])
	if err != nil {
		return 0, fmt.Errorf("operand %q: %w", f[0], err)
	}
	b, err := strconv.Atoi(f[2])
	if err != nil {
		return 0, fmt.Errorf("operand %q: %w", f[2], err)
	}
	switch op := f[1]; op {
	case "+":
		return a + b, nil
	case "-":
		return a - b, nil
	case "*":
		return a * b, nil
	case "/", "%":
		if b == 0 {
			return 0, fmt.Errorf("%d %s %d: %w", a, op, b, ErrDivZero)
		}
		if op == "/" {
			return a / b, nil
		}
		return a % b, nil
	default:
		return 0, fmt.Errorf("operator %q: %w", op, ErrUnknownOp)
	}
}

// classify возвращает короткое имя вида ошибки.
func classify(err error) string {
	var se *SyntaxError
	var ne *strconv.NumError
	switch {
	case errors.Is(err, ErrDivZero):
		return "div0"
	case errors.Is(err, ErrUnknownOp):
		return "op"
	case errors.As(err, &se):
		return "syntax"
	case errors.As(err, &ne):
		return "number"
	}
	return "other"
}

func main() {
	ok, failed := 0, 0
	defer func() {
		fmt.Printf("done: %d ok, %d failed\n", ok, failed)
	}()

	sc := bufio.NewScanner(os.Stdin)
	for i := 1; sc.Scan(); i++ {
		v, err := Eval(sc.Text())
		if err != nil {
			failed++
			fmt.Printf("%d: %s: %v\n", i, classify(err), err)
			continue
		}
		ok++
		fmt.Printf("%d: %d\n", i, v)
	}
}
