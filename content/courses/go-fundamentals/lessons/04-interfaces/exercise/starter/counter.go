package iface

import "io"

// CountingWriter пересылает данные в w и считает реально записанные байты.
type CountingWriter struct {
	w io.Writer
	N int
}

func NewCountingWriter(w io.Writer) *CountingWriter {
	return &CountingWriter{w: w}
}

func (c *CountingWriter) Write(p []byte) (int, error) {
	// TODO: пересылайте данные в c.w и прибавляйте к c.N число байтов,
	// которое вернул c.w.Write (оно может быть меньше len(p))
	return c.w.Write(p)
}
