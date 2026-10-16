package iface

import (
	"bytes"
	"errors"
	"io"
	"strings"
	"testing"
	"testing/iotest"
)

type celsius int

func (c celsius) String() string { return strings.Repeat("C", int(c)) }

type myInt int

type errAndStringer struct{}

func (errAndStringer) Error() string  { return "boom" }
func (errAndStringer) String() string { return "str" }

func readAll(t *testing.T, r io.Reader) string {
	t.Helper()
	b, err := io.ReadAll(r)
	if err != nil {
		t.Fatalf("ReadAll: %v", err)
	}
	return string(b)
}

func TestUpperReader(t *testing.T) {
	t.Run("simple", func(t *testing.T) {
		got := readAll(t, NewUpperReader(strings.NewReader("hello, World 123")))
		if got != "HELLO, WORLD 123" {
			t.Fatalf("got %q", got)
		}
	})
	t.Run("empty", func(t *testing.T) {
		if got := readAll(t, NewUpperReader(strings.NewReader(""))); got != "" {
			t.Fatalf("got %q", got)
		}
	})
	t.Run("onebyte", func(t *testing.T) {
		got := readAll(t, NewUpperReader(iotest.OneByteReader(strings.NewReader("abc xyz"))))
		if got != "ABC XYZ" {
			t.Fatalf("got %q", got)
		}
	})
	t.Run("unicode", func(t *testing.T) {
		got := readAll(t, NewUpperReader(strings.NewReader("привет abc")))
		if got != "привет ABC" {
			t.Fatalf("got %q: байты не-ASCII менять нельзя", got)
		}
	})
	t.Run("data-with-eof", func(t *testing.T) {
		// DataErrReader отдаёт последние данные вместе с io.EOF в одном вызове Read.
		got := readAll(t, NewUpperReader(iotest.DataErrReader(strings.NewReader("tail"))))
		if got != "TAIL" {
			t.Fatalf("got %q", got)
		}
	})
	t.Run("error-passthrough", func(t *testing.T) {
		boom := errors.New("boom")
		r := NewUpperReader(io.MultiReader(strings.NewReader("ab"), iotest.ErrReader(boom)))
		b, err := io.ReadAll(r)
		if !errors.Is(err, boom) {
			t.Fatalf("err = %v, want boom", err)
		}
		if string(b) != "AB" {
			t.Fatalf("data = %q", b)
		}
	})
	t.Run("not-modify-rest-of-buffer", func(t *testing.T) {
		// Read не должен трогать байты буфера за пределами прочитанного.
		buf := []byte("zzzzzzzz")
		n, _ := NewUpperReader(strings.NewReader("ab")).Read(buf)
		if n != 2 || string(buf) != "ABzzzzzz" {
			t.Fatalf("n=%d buf=%q", n, buf)
		}
	})
}

type shortWriter struct {
	max int
	buf bytes.Buffer
}

func (s *shortWriter) Write(p []byte) (int, error) {
	if len(p) > s.max {
		s.buf.Write(p[:s.max])
		return s.max, io.ErrShortWrite
	}
	return s.buf.Write(p)
}

func TestCountingWriter(t *testing.T) {
	t.Run("basic", func(t *testing.T) {
		var buf bytes.Buffer
		cw := NewCountingWriter(&buf)
		io.WriteString(cw, "hello")
		io.WriteString(cw, ", мир")
		if cw.N != 13 || buf.String() != "hello, мир" {
			t.Fatalf("N=%d buf=%q", cw.N, buf.String())
		}
	})
	t.Run("short-write", func(t *testing.T) {
		sw := &shortWriter{max: 3}
		cw := NewCountingWriter(sw)
		n, err := cw.Write([]byte("abcdef"))
		if n != 3 || !errors.Is(err, io.ErrShortWrite) || cw.N != 3 {
			t.Fatalf("n=%d err=%v N=%d", n, err, cw.N)
		}
	})
	t.Run("as-writer", func(t *testing.T) {
		var buf bytes.Buffer
		cw := NewCountingWriter(&buf)
		var w io.Writer = cw
		w.Write([]byte("xy"))
		if cw.N != 2 {
			t.Fatalf("N=%d", cw.N)
		}
	})
}

func TestDescribe(t *testing.T) {
	var nilErr error
	cases := []struct {
		name string
		in   any
		want string
	}{
		{"nil", nil, "nil"},
		{"int", 5, "int 5"},
		{"string", "hi", `string "hi"`},
		{"bool", true, "bool true"},
		{"slice", []int{1, 2, 3}, "[]int len=3"},
		{"error", errors.New("bad"), "error: bad"},
		{"stringer", celsius(3), "stringer: CCC"},
		{"named-int-is-not-int", myInt(7), "other iface.myInt"},
		{"float", 1.5, "other float64"},
		{"error-before-stringer", errAndStringer{}, "error: boom"},
		{"nil-interface-value", nilErr, "nil"},
		{"pointer", new(int), "other *int"},
	}
	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			if got := Describe(tc.in); got != tc.want {
				t.Fatalf("Describe(%v) = %q, want %q", tc.in, got, tc.want)
			}
		})
	}
}

func TestValidate(t *testing.T) {
	t.Run("known-is-nil", func(t *testing.T) {
		if err := Validate(1); err != nil {
			t.Fatalf("Validate(1) = %#v, want nil interface", err)
		}
	})
	t.Run("unknown", func(t *testing.T) {
		err := Validate(42)
		var nf *NotFoundError
		if !errors.As(err, &nf) || nf.ID != 42 {
			t.Fatalf("err = %v", err)
		}
	})
}

func TestFind(t *testing.T) {
	t.Run("found", func(t *testing.T) {
		v, err := Find(2)
		if err != nil || v != "bob" {
			t.Fatalf("Find(2) = %q, %v", v, err)
		}
	})
	t.Run("missing", func(t *testing.T) {
		_, err := Find(9)
		var nf *NotFoundError
		if !errors.As(err, &nf) || nf.ID != 9 {
			t.Fatalf("err = %v", err)
		}
		if !strings.Contains(err.Error(), "id 9 not found") {
			t.Fatalf("message = %q", err.Error())
		}
	})
}
