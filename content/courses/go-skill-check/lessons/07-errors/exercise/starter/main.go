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

// lookup ищет key в m. Если ключа нет, верните 0 и ошибку, которая оборачивает
// ErrNotFound (fmt.Errorf с %w) и добавляет контекст.
func lookup(m map[string]int, key string) (int, error) {
	return 0, errors.New("lookup не написана")
}

// validateAge проверяет возраст 0..150. Для неверного значения верните
// &ValidationError{Field: "age"}, для верного - литерал nil.
func validateAge(age int) error {
	return errors.New("validateAge не написана")
}

// classify называет причину ошибки: nil -> "ok"; обёрнутая ErrNotFound -> "not found";
// *ValidationError в цепочке -> "invalid " + поле; иначе "other".
// Подсказка: errors.Is и errors.As.
func classify(err error) string {
	return ""
}

func main() {
	_, err := lookup(map[string]int{"a": 1}, "b")
	fmt.Println(err, classify(err))
	fmt.Println(classify(validateAge(200)), classify(validateAge(30)))
}
