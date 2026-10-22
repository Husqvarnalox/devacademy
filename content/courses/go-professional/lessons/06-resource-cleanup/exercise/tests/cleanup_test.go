package cleanup

import (
	"errors"
	"reflect"
	"strings"
	"sync"
	"testing"
)

type fake struct {
	mu        sync.Mutex
	log       []string
	failOpen  map[string]error
	failClose map[string]error
	closes    map[string]int
}

func newFake() *fake {
	return &fake{failOpen: map[string]error{}, failClose: map[string]error{}, closes: map[string]int{}}
}

func (f *fake) record(s string) {
	f.mu.Lock()
	f.log = append(f.log, s)
	f.mu.Unlock()
}

func (f *fake) Open(name string) (Resource, error) {
	f.record("open " + name)
	if err := f.failOpen[name]; err != nil {
		return nil, err
	}
	return &fres{f: f, name: name}, nil
}

type fres struct {
	f    *fake
	name string
}

func (r *fres) Close() error {
	r.f.record("close " + r.name)
	r.f.mu.Lock()
	r.f.closes[r.name]++
	r.f.mu.Unlock()
	return r.f.failClose[r.name]
}

func eq(t *testing.T, got, want []string) {
	t.Helper()
	if !reflect.DeepEqual(got, want) {
		t.Fatalf("event log:\n got  %v\n want %v", got, want)
	}
}

func TestProcessHappyPath(t *testing.T) {
	f := newFake()
	var seen int
	err := Process(f, []string{"a", "b", "c"}, func(rs []Resource) error {
		seen = len(rs)
		f.record("work")
		return nil
	})
	if err != nil || seen != 3 {
		t.Fatalf("err=%v seen=%d", err, seen)
	}
	eq(t, f.log, []string{"open a", "open b", "open c", "work", "close c", "close b", "close a"})
}

func TestProcessNoNames(t *testing.T) {
	f := newFake()
	called := false
	if err := Process(f, nil, func(rs []Resource) error { called = len(rs) == 0; return nil }); err != nil || !called {
		t.Fatalf("err=%v called=%v", err, called)
	}
}

func TestOpenFailureClosesPrevious(t *testing.T) {
	f := newFake()
	boom := errors.New("boom")
	f.failOpen["c"] = boom
	worked := false
	err := Process(f, []string{"a", "b", "c", "d"}, func([]Resource) error { worked = true; return nil })
	if worked {
		t.Fatal("work must not run when opening failed")
	}
	if !errors.Is(err, boom) || !strings.Contains(err.Error(), `"c"`) {
		t.Fatalf("err = %v; want it to wrap boom and name resource c", err)
	}
	eq(t, f.log, []string{"open a", "open b", "open c", "close b", "close a"})
}

func TestOpenFailureAndCloseFailureBothReported(t *testing.T) {
	f := newFake()
	openErr, closeErr := errors.New("cannot open"), errors.New("cannot close")
	f.failOpen["b"] = openErr
	f.failClose["a"] = closeErr
	g, err := OpenAll(f, []string{"a", "b"})
	if g != nil {
		t.Fatal("no Group must be returned on failure")
	}
	if !errors.Is(err, openErr) || !errors.Is(err, closeErr) {
		t.Fatalf("err = %v; want both the open and the close failure", err)
	}
}

func TestWorkErrorStillCloses(t *testing.T) {
	f := newFake()
	bad := errors.New("work failed")
	err := Process(f, []string{"a", "b"}, func([]Resource) error { return bad })
	if !errors.Is(err, bad) {
		t.Fatalf("err = %v, want work failed", err)
	}
	eq(t, f.log, []string{"open a", "open b", "close b", "close a"})
}

func TestWorkPanicStillCloses(t *testing.T) {
	f := newFake()
	func() {
		defer func() {
			if r := recover(); r != "oops" {
				t.Fatalf("recovered %v, want the original panic to continue", r)
			}
		}()
		_ = Process(f, []string{"a", "b"}, func([]Resource) error { panic("oops") })
		t.Fatal("Process returned after a panic in work")
	}()
	eq(t, f.log, []string{"open a", "open b", "close b", "close a"})
}

func TestCloseErrorsAreJoined(t *testing.T) {
	f := newFake()
	eb, ea, ew := errors.New("close-b"), errors.New("close-a"), errors.New("work")
	f.failClose["b"], f.failClose["a"] = eb, ea
	err := Process(f, []string{"a", "b", "c"}, func([]Resource) error { return ew })
	for _, want := range []error{eb, ea, ew} {
		if !errors.Is(err, want) {
			t.Fatalf("err = %v; missing %v", err, want)
		}
	}
	// every Close was attempted although b's failed
	eq(t, f.log, []string{"open a", "open b", "open c", "close c", "close b", "close a"})
	if !strings.Contains(err.Error(), `"b"`) {
		t.Fatalf("close errors must name the resource: %v", err)
	}
}

func TestCloseErrorAloneIsReturned(t *testing.T) {
	f := newFake()
	ec := errors.New("flush failed")
	f.failClose["a"] = ec
	if err := Process(f, []string{"a"}, func([]Resource) error { return nil }); !errors.Is(err, ec) {
		t.Fatalf("err = %v; a failed Close after a successful work must be reported", err)
	}
}

func TestGroupCloseIsIdempotent(t *testing.T) {
	f := newFake()
	g, err := OpenAll(f, []string{"x", "y"})
	if err != nil {
		t.Fatal(err)
	}
	if err := g.Close(); err != nil {
		t.Fatal(err)
	}
	if err := g.Close(); err != nil {
		t.Fatalf("second Close = %v, want nil", err)
	}
	if f.closes["x"] != 1 || f.closes["y"] != 1 {
		t.Fatalf("each resource must be closed exactly once, got %v", f.closes)
	}
	eq(t, f.log, []string{"open x", "open y", "close y", "close x"})
}
