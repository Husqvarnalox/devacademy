package iface

import "io"

type upperReader struct {
	r io.Reader
}

// NewUpperReader оборачивает r и должен переводить ASCII-буквы a-z в верхний
// регистр. Остальные байты (в том числе многобайтные символы UTF-8) не меняются.
func NewUpperReader(r io.Reader) io.Reader {
	return &upperReader{r: r}
}

func (u *upperReader) Read(p []byte) (int, error) {
	// TODO: прочитайте из u.r в p, переведите в верхний регистр ТОЛЬКО p[:n]
	// и верните (n, err) без изменений
	return u.r.Read(p)
}
