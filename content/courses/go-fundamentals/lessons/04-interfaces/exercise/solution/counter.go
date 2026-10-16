package iface

import "io"

// CountingWriter пересылает данные в w и считает реально записанные байты.
type CountingWriter struct {
	w io.Writer
	N int
}

var _ io.Writer = (*CountingWriter)(nil)

func NewCountingWriter(w io.Writer) *CountingWriter {
	return &CountingWriter{w: w}
}

func (c *CountingWriter) Write(p []byte) (int, error) {
	n, err := c.w.Write(p)
	c.N += n
	return n, err
}
