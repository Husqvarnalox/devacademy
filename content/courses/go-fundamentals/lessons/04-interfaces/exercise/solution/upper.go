package iface

import "io"

type upperReader struct {
	r io.Reader
}

// Проверка на этапе компиляции: *upperReader реализует io.Reader.
var _ io.Reader = (*upperReader)(nil)

// NewUpperReader оборачивает r и переводит ASCII-буквы a-z в верхний регистр.
// Остальные байты (в том числе многобайтные символы UTF-8) не меняются.
func NewUpperReader(r io.Reader) io.Reader {
	return &upperReader{r: r}
}

func (u *upperReader) Read(p []byte) (int, error) {
	n, err := u.r.Read(p)
	// Обрабатываем только прочитанную часть p[:n]; err возвращаем как есть,
	// даже если n > 0 (io.Reader вправе вернуть данные и ошибку вместе).
	for i := 0; i < n; i++ {
		if p[i] >= 'a' && p[i] <= 'z' {
			p[i] -= 'a' - 'A'
		}
	}
	return n, err
}
