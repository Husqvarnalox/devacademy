package durcase

import (
	"errors"
	"fmt"
	"strings"
	"testing"
)

// opts switches single behaviors of the parser. The zero value except
// maxDigits is not the reference: see ref and the mutants below.
type opts struct {
	maxDigits  int
	skipOrder  bool // m1: order not enforced (repeats still rejected)
	allowDup   bool // m2: repeated units accepted (order still enforced)
	capMinutes bool // m3: minutes above 59 rejected
	allowBare  bool // m4: "30" accepted as 30 seconds
	noLeadZero bool // m5: leading zeros rejected
}

var ref = opts{maxDigits: 6}

func parse(s string, o opts) (int, error) {
	if s == "" {
		return 0, errors.New("empty")
	}
	total, last := 0, -1
	seen := map[int]bool{}
	i := 0
	for i < len(s) {
		j := i
		for j < len(s) && s[j] >= '0' && s[j] <= '9' {
			j++
		}
		digits := s[i:j]
		if digits == "" {
			return 0, fmt.Errorf("expected digits at %d", i)
		}
		if len(digits) > o.maxDigits {
			return 0, errors.New("too many digits")
		}
		if o.noLeadZero && len(digits) > 1 && digits[0] == '0' {
			return 0, errors.New("leading zero")
		}
		n := 0
		for _, c := range digits {
			n = n*10 + int(c-'0')
		}
		if j == len(s) {
			if o.allowBare && i == 0 {
				return n, nil
			}
			return 0, errors.New("missing unit")
		}
		var rank, mult int
		switch s[j] {
		case 'h':
			rank, mult = 0, 3600
		case 'm':
			rank, mult = 1, 60
			if o.capMinutes && n > 59 {
				return 0, errors.New("minutes too large")
			}
		case 's':
			rank, mult = 2, 1
		default:
			return 0, fmt.Errorf("unknown unit %q", s[j])
		}
		switch {
		case o.skipOrder:
			if seen[rank] {
				return 0, errors.New("repeated unit")
			}
			seen[rank] = true
		case o.allowDup:
			if rank < last {
				return 0, errors.New("wrong order")
			}
		default:
			if rank <= last {
				return 0, errors.New("wrong order or repeat")
			}
		}
		last = rank
		total += n * mult
		i = j + 1
	}
	return total, nil
}

func TestCasesAreWellFormed(t *testing.T) {
	if len(Cases) == 0 || len(Cases) > 40 {
		t.Fatalf("the table must have 1..40 cases, has %d", len(Cases))
	}
	names, inputs := map[string]bool{}, map[string]bool{}
	for i, c := range Cases {
		if strings.TrimSpace(c.Name) == "" {
			t.Errorf("case %d has an empty name", i)
		}
		if names[c.Name] {
			t.Errorf("duplicate case name %q", c.Name)
		}
		if inputs[c.Input] {
			t.Errorf("duplicate input %q (case %q)", c.Input, c.Name)
		}
		names[c.Name], inputs[c.Input] = true, true
	}
}

func TestCasesAgreeWithReference(t *testing.T) {
	for _, c := range Cases {
		t.Run(c.Name, func(t *testing.T) {
			got, err := parse(c.Input, ref)
			if c.WantErr {
				if err == nil {
					t.Fatalf("input %q is valid according to the specification (= %d seconds), but the case expects an error", c.Input, got)
				}
				return
			}
			if err != nil {
				t.Fatalf("input %q must be accepted by the specification, reference says: %v", c.Input, err)
			}
			if got != c.Want {
				t.Fatalf("input %q: the specification gives %d seconds, the case expects %d", c.Input, got, c.Want)
			}
		})
	}
}

// killed reports whether at least one case fails when run against the mutant.
func killed(o opts) (string, bool) {
	for _, c := range Cases {
		got, err := parse(c.Input, o)
		if c.WantErr {
			if err == nil {
				return c.Name, true
			}
			continue
		}
		if err != nil || got != c.Want {
			return c.Name, true
		}
	}
	return "", false
}

func TestMutantsAreDetected(t *testing.T) {
	// sanity: the reference itself is never "killed" by a valid table
	if name, bad := killed(ref); bad {
		t.Skipf("table contradicts the specification at %q; see TestCasesAgreeWithReference", name)
	}
	mutants := []struct {
		id string
		o  opts
	}{
		{"m1", opts{maxDigits: 6, skipOrder: true}},
		{"m2", opts{maxDigits: 6, allowDup: true}},
		{"m3", opts{maxDigits: 6, capMinutes: true}},
		{"m4", opts{maxDigits: 6, allowBare: true}},
		{"m5", opts{maxDigits: 6, noLeadZero: true}},
		{"m6", opts{maxDigits: 7}},
	}
	for _, m := range mutants {
		t.Run(m.id, func(t *testing.T) {
			if _, ok := killed(m.o); !ok {
				t.Fatalf("mutant %s survived: no case fails on an implementation with this defect; add a case that separates it from the specification", m.id)
			}
		})
	}
}
