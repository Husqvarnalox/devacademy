package report

import (
	"fmt"
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
//
// This version is correct but allocates on every row. It runs on the export
// hot path, so the allocation count has to come down.
func FormatCSV(recs []Record) string {
	out := ""
	for _, r := range recs {
		name := r.Name
		if strings.ContainsAny(name, ",\"\n") {
			name = "\"" + strings.ReplaceAll(name, "\"", "\"\"") + "\""
		}
		line := fmt.Sprintf("%d,%s,%d.%02d\n", r.ID, name, r.Amount/100, r.Amount%100)
		out += line
	}
	return out
}

// ActiveNames returns the names of all active records in input order.
// It must return a non-nil empty slice when nothing matches.
func ActiveNames(recs []Record) []string {
	var names []string
	for _, r := range recs {
		if r.Active {
			names = append(names, r.Name)
		}
	}
	if names == nil {
		names = []string{}
	}
	return names
}
