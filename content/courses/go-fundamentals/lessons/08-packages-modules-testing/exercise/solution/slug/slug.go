// Package slug строит URL-совместимые идентификаторы из заголовков.
package slug

import (
	"strconv"
	"strings"

	"example.com/textkit/internal/norm"
)

// Slugify превращает заголовок в slug: слова в нижнем регистре через "-".
func Slugify(s string) string {
	return strings.Join(norm.Words(s), "-")
}

// Unique возвращает base, если его нет в taken, иначе base-2, base-3 и так далее
// (первый свободный вариант). Пустой base заменяется на "item".
// Найденный slug в taken НЕ добавляется.
func Unique(base string, taken map[string]bool) string {
	if base == "" {
		base = "item"
	}
	if !taken[base] {
		return base
	}
	for n := 2; ; n++ {
		cand := base + "-" + strconv.Itoa(n)
		if !taken[cand] {
			return cand
		}
	}
}
