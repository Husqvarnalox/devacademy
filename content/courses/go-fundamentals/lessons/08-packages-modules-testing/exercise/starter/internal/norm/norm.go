// Package norm содержит внутренние функции нормализации текста.
// Из-за каталога internal импортировать пакет могут только пакеты модуля
// example.com/textkit.
package norm

import "strings"

// Words разбивает s на слова: последовательности букв и цифр в нижнем регистре.
// Любой другой символ служит разделителем.
func Words(s string) []string {
	// TODO: сейчас делит только по пробелам и не приводит регистр.
	// Используйте strings.ToLower и strings.FieldsFunc с unicode.IsLetter / unicode.IsDigit.
	return strings.Fields(s)
}
