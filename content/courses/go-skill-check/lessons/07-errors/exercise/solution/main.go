package main

import (
	"errors"
	"fmt"
)

// ErrNotFound возвращается, когда ключа нет в карте.
var ErrNotFound = errors.New("not found")

// ValidationError описывает неверное значение поля.
type ValidationError struct {
	Field string
}

func (e *ValidationError) Error() string { return "invalid " + e.Field }

// lookup ищет key в m; если ключа нет, оборачивает ErrNotFound с контекстом.
func lookup(m map[string]int, key string) (int, error) {
	v, ok := m[key]
	if !ok {
		return 0, fmt.Errorf("lookup %q: %w", key, ErrNotFound)
	}
	return v, nil
}

// validateAge проверяет возраст от 0 до 150.
func validateAge(age int) error {
	if age < 0 || age > 150 {
		return &ValidationError{Field: "age"}
	}
	return nil
}

// classify называет причину ошибки.
func classify(err error) string {
	var ve *ValidationError
	switch {
	case err == nil:
		return "ok"
	case errors.Is(err, ErrNotFound):
		return "not found"
	case errors.As(err, &ve):
		return "invalid " + ve.Field
	default:
		return "other"
	}
}

func main() {
	_, err := lookup(map[string]int{"a": 1}, "b")
	fmt.Println(err, classify(err))
	fmt.Println(classify(validateAge(200)), classify(validateAge(30)))
}
