// Package norm содержит внутренние функции нормализации текста.
// Из-за каталога internal импортировать пакет могут только пакеты модуля
// example.com/textkit.
package norm

import (
	"strings"
	"unicode"
)

// Words разбивает s на слова: последовательности букв и цифр в нижнем регистре.
// Любой другой символ служит разделителем.
func Words(s string) []string {
	return strings.FieldsFunc(strings.ToLower(s), func(r rune) bool {
		return !unicode.IsLetter(r) && !unicode.IsDigit(r)
	})
}
