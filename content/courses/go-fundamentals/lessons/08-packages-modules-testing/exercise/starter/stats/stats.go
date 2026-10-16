// Package stats считает частоты слов.
package stats

// Entry - слово и число его вхождений.
type Entry struct {
	Word  string
	Count int
}

// WordCount возвращает частоты слов текста (см. norm.Words).
func WordCount(text string) map[string]int {
	// TODO: пройдите по norm.Words(text) и увеличивайте счётчики.
	return nil
}

// Top возвращает до n самых частых слов: по убыванию Count, при равенстве - по алфавиту.
// При n <= 0 возвращается пустой срез.
func Top(counts map[string]int, n int) []Entry {
	// TODO: соберите []Entry, отсортируйте sort.Slice и обрежьте до n.
	return nil
}
