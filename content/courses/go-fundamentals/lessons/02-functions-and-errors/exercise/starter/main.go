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

// SyntaxError - типизированная ошибка (пока не используется).
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
		// TODO: верните *SyntaxError (с Line и Reason), а не обычную строку
		return 0, fmt.Errorf("syntax error in %q: want: a op b", line)
	}
	a, err := strconv.Atoi(f[0])
	if err != nil {
		// TODO: %v обрывает цепочку ошибок; нужен %w
		return 0, fmt.Errorf("operand %q: %v", f[0], err)
	}
	b, err := strconv.Atoi(f[2])
	if err != nil {
		return 0, fmt.Errorf("operand %q: %v", f[2], err)
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
			// TODO: оберните сентинел ErrDivZero, а не создавайте новую ошибку
			return 0, fmt.Errorf("%d %s %d: division by zero", a, op, b)
		}
		if op == "/" {
			return a / b, nil
		}
		return a % b, nil
	default:
		return 0, fmt.Errorf("operator %q: unknown operator", op)
	}
}

// classify возвращает короткое имя вида ошибки:
// "div0", "op", "syntax", "number" или "other".
func classify(err error) string {
	// TODO: используйте errors.Is для сентинелов и errors.As для
	// *SyntaxError и *strconv.NumError (проверяйте в таком порядке)
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
