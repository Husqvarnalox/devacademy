package report

import (
	"fmt"
	"strings"
	"testing"
)

func TestFormatCSV(t *testing.T) {
	cases := []struct {
		name string
		in   []Record
		want string
	}{
		{"empty", nil, ""},
		{"one", []Record{{ID: 1, Name: "Ann", Amount: 1234}}, "1,Ann,12.34\n"},
		{"cents-padding", []Record{{ID: 2, Name: "Bob", Amount: 705}, {ID: 3, Name: "Eve", Amount: 5}}, "2,Bob,7.05\n3,Eve,0.05\n"},
		{"zero", []Record{{ID: 0, Name: "", Amount: 0}}, "0,,0.00\n"},
		{"large", []Record{{ID: 123456789, Name: "Big", Amount: 99999999900}}, "123456789,Big,999999999.00\n"},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			if got := FormatCSV(c.in); got != c.want {
				t.Fatalf("FormatCSV = %q, want %q", got, c.want)
			}
		})
	}
}

func TestFormatCSVQuoting(t *testing.T) {
	cases := []struct {
		name string
		in   string
		want string
	}{
		{"comma", "Doe, John", "1,\"Doe, John\",1.00\n"},
		{"quote", `say "hi"`, "1,\"say \"\"hi\"\"\",1.00\n"},
		{"newline", "a\nb", "1,\"a\nb\",1.00\n"},
		{"plain-unicode", "Иван", "1,Иван,1.00\n"},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			got := FormatCSV([]Record{{ID: 1, Name: c.in, Amount: 100}})
			if got != c.want {
				t.Fatalf("FormatCSV = %q, want %q", got, c.want)
			}
		})
	}
}

func sample(n int) []Record {
	recs := make([]Record, n)
	for i := range recs {
		recs[i] = Record{ID: i + 1, Name: fmt.Sprintf("customer-%d", i), Amount: int64(i*137 + 5), Active: i%3 != 0}
	}
	return recs
}

func TestFormatCSVMatchesReference(t *testing.T) {
	recs := sample(50)
	recs[7].Name = "a,b"
	recs[9].Name = `q"q`
	var want strings.Builder
	for _, r := range recs {
		name := r.Name
		if strings.ContainsAny(name, ",\"\n") {
			name = `"` + strings.ReplaceAll(name, `"`, `""`) + `"`
		}
		fmt.Fprintf(&want, "%d,%s,%d.%02d\n", r.ID, name, r.Amount/100, r.Amount%100)
	}
	if got := FormatCSV(recs); got != want.String() {
		t.Fatalf("output differs from reference for 50 records")
	}
}

func TestFormatCSVAllocs(t *testing.T) {
	recs := sample(100)
	var sink string
	allocs := testing.AllocsPerRun(50, func() { sink = FormatCSV(recs) })
	_ = sink
	const limit = 4
	if allocs > limit {
		t.Fatalf("FormatCSV makes %.0f allocations for 100 records, limit is %d (allocation must not grow with the number of rows)", allocs, limit)
	}
}

func TestActiveNames(t *testing.T) {
	recs := sample(10)
	got := ActiveNames(recs)
	want := []string{}
	for _, r := range recs {
		if r.Active {
			want = append(want, r.Name)
		}
	}
	if len(got) != len(want) {
		t.Fatalf("len = %d, want %d", len(got), len(want))
	}
	for i := range want {
		if got[i] != want[i] {
			t.Fatalf("got[%d] = %q, want %q", i, got[i], want[i])
		}
	}
	none := ActiveNames([]Record{{Name: "x"}})
	if none == nil || len(none) != 0 {
		t.Fatalf("no active records must give a non-nil empty slice, got %#v", none)
	}
}

func TestActiveNamesAllocs(t *testing.T) {
	recs := sample(300)
	var sink []string
	allocs := testing.AllocsPerRun(50, func() { sink = ActiveNames(recs) })
	_ = sink
	if allocs > 1 {
		t.Fatalf("ActiveNames makes %.0f allocations for 300 records, limit is 1 (the result slice only)", allocs)
	}
}
