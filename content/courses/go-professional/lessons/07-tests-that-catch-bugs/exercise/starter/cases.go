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
	{Name: "seconds", Input: "45s", Want: 45},
	{Name: "minutes", Input: "2m", Want: 120},
	{Name: "hours", Input: "1h", Want: 3600},
}
