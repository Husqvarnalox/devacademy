// Package slug строит URL-совместимые идентификаторы из заголовков.
package slug

import "strings"

// Slugify превращает заголовок в slug: слова в нижнем регистре через "-".
func Slugify(s string) string {
	// TODO: используйте norm.Words из example.com/textkit/internal/norm
	// и strings.Join(words, "-").
	return strings.ToLower(s)
}

// Unique возвращает base, если его нет в taken, иначе base-2, base-3 и так далее
// (первый свободный вариант). Пустой base заменяется на "item".
// Найденный slug в taken НЕ добавляется.
func Unique(base string, taken map[string]bool) string {
	// TODO
	return base
}
