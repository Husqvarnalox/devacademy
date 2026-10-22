package report

import (
	"strconv"
	"strings"
)

// Record is one row of the export. Amount is in cents.
type Record struct {
	ID     int
	Name   string
	Amount int64
	Active bool
}

// FormatCSV renders records as CSV lines "id,name,amount\n", where amount is
// written as dollars.cents ("1234" cents -> "12.34"). A name containing a comma,
// a double quote or a newline is wrapped in double quotes and inner quotes are
// doubled. The result ends with a newline after the last record; no records
// give an empty string.
func FormatCSV(recs []Record) string {
	if len(recs) == 0 {
		return ""
	}
	// Estimate the final size once: a row is at least id + name + amount and
	// separators. Over-estimating wastes a little memory; under-estimating
	// still works, it only costs one more growth.
	size := 0
	for _, r := range recs {
		size += len(r.Name) + 32
	}
	var b strings.Builder
	b.Grow(size)

	var tmp [24]byte // scratch space for numbers; stays on the stack
	for _, r := range recs {
		b.Write(strconv.AppendInt(tmp[:0], int64(r.ID), 10))
		b.WriteByte(',')
		writeField(&b, r.Name)
		b.WriteByte(',')
		b.Write(strconv.AppendInt(tmp[:0], r.Amount/100, 10))
		b.WriteByte('.')
		cents := r.Amount % 100
		b.WriteByte(byte('0' + cents/10))
		b.WriteByte(byte('0' + cents%10))
		b.WriteByte('\n')
	}
	return b.String()
}

func writeField(b *strings.Builder, s string) {
	if !strings.ContainsAny(s, ",\"\n") {
		b.WriteString(s)
		return
	}
	b.WriteByte('"')
	for i := 0; i < len(s); i++ {
		if s[i] == '"' {
			b.WriteByte('"')
		}
		b.WriteByte(s[i])
	}
	b.WriteByte('"')
}

// ActiveNames returns the names of all active records in input order.
// It must return a non-nil empty slice when nothing matches.
func ActiveNames(recs []Record) []string {
	n := 0
	for _, r := range recs {
		if r.Active {
			n++
		}
	}
	names := make([]string, 0, n)
	for _, r := range recs {
		if r.Active {
			names = append(names, r.Name)
		}
	}
	return names
}
