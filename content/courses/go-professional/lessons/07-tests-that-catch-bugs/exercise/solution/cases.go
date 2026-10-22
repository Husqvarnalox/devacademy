// Package durcase holds the test table for ParseSeconds.
//
// ParseSeconds(s string) (int, error) converts a compact duration into a
// number of seconds. The implementation is owned by another team and is not
// in this repository; your job is the test table that guards it.
//
// Specification
//
//		duration  = component { component }
//		component = digits unit
//		digits    = one or more ASCII digits "0".."9", at most 6 of them
//		unit      = "h" (3600 s) | "m" (60 s) | "s" (1 s)
//
//	  - components must appear in the order h, m, s, each unit at most once;
//	  - leading zeros are allowed ("05s" is 5 seconds);
//	  - m and s values may exceed 59 ("90m" is 5400, "90s" is 90);
//	  - anything else is an error and ParseSeconds returns (0, non-nil error):
//	    the empty string, a number without a unit ("30"), a unit without a
//	    number ("h"), upper-case or unknown units, spaces, signs, fractions,
//	    more than 6 digits, wrong order, repeated units, trailing characters.
//
// Hidden tests run every case against the correct implementation and against
// several deliberately broken ones. A good table makes each broken one fail
// at least one case.
package durcase

// Case is one row of the table.
type Case struct {
	Name    string // unique, non-empty, describes the idea of the case
	Input   string
	Want    int  // expected seconds; ignored when WantErr is true
	WantErr bool // the input must be rejected
}

// Cases is the table. At most 40 cases, all with distinct inputs.
var Cases = []Case{
	// valid inputs: each unit alone, then combined
	{Name: "seconds only", Input: "45s", Want: 45},
	{Name: "minutes only", Input: "2m", Want: 120},
	{Name: "hours only", Input: "1h", Want: 3600},
	{Name: "all three units", Input: "1h2m3s", Want: 3723},
	{Name: "hours and seconds, no minutes", Input: "1h5s", Want: 3605},
	{Name: "zero", Input: "0s", Want: 0},

	// values outside the "clock" range are legal
	{Name: "minutes above 59", Input: "90m", Want: 5400},
	{Name: "seconds above 59", Input: "90s", Want: 90},
	{Name: "leading zeros", Input: "05s", Want: 5},

	// boundary of the digit limit: 6 digits fit, 7 do not
	{Name: "six digits", Input: "999999s", Want: 999999},
	{Name: "seven digits", Input: "1000000s", WantErr: true},

	// structure errors
	{Name: "empty", Input: "", WantErr: true},
	{Name: "number without unit", Input: "30", WantErr: true},
	{Name: "unit without number", Input: "h", WantErr: true},
	{Name: "wrong order", Input: "30s1h", WantErr: true},
	{Name: "repeated unit", Input: "1h1h", WantErr: true},
	{Name: "trailing characters", Input: "5sx", WantErr: true},

	// character errors
	{Name: "upper-case unit", Input: "1H", WantErr: true},
	{Name: "unknown unit", Input: "5d", WantErr: true},
	{Name: "space inside", Input: "1h 30m", WantErr: true},
	{Name: "sign", Input: "-5s", WantErr: true},
	{Name: "fraction", Input: "1.5h", WantErr: true},
}
