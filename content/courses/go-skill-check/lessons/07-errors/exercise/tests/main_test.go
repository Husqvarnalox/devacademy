package main

import (
	"errors"
	"fmt"
	"testing"
)

func TestLookup(t *testing.T) {
	m := map[string]int{"a": 1, "zero": 0}
	t.Run("found", func(t *testing.T) {
		v, err := lookup(m, "a")
		if err != nil || v != 1 {
			t.Fatalf("lookup(a) = %d, %v", v, err)
		}
	})
	t.Run("found_zero_value", func(t *testing.T) {
		v, err := lookup(m, "zero")
		if err != nil || v != 0 {
			t.Fatalf("lookup(zero) = %d, %v; ключ существует, ошибки быть не должно", v, err)
		}
	})
	t.Run("missing_wrapped", func(t *testing.T) {
		v, err := lookup(m, "b")
		if v != 0 || !errors.Is(err, ErrNotFound) {
			t.Fatalf("lookup(b) = %d, %v; ожидались 0 и обёрнутая ErrNotFound", v, err)
		}
		if err == ErrNotFound {
			t.Fatalf("ошибка должна быть обёрнута с контекстом, а не равна ErrNotFound")
		}
	})
	t.Run("nil_map", func(t *testing.T) {
		if _, err := lookup(nil, "x"); !errors.Is(err, ErrNotFound) {
			t.Fatalf("lookup(nil, x): %v", err)
		}
	})
}

func TestValidateAge(t *testing.T) {
	for _, age := range []int{0, 1, 150} {
		t.Run(fmt.Sprintf("valid_%d", age), func(t *testing.T) {
			if err := validateAge(age); err != nil {
				t.Fatalf("validateAge(%d) = %v, ожидалось nil", age, err)
			}
		})
	}
	for _, age := range []int{-1, 151, 1000} {
		t.Run(fmt.Sprintf("invalid_%d", age), func(t *testing.T) {
			var ve *ValidationError
			err := validateAge(age)
			if !errors.As(err, &ve) || ve.Field != "age" {
				t.Fatalf("validateAge(%d) = %v, ожидалась *ValidationError с полем age", age, err)
			}
		})
	}
}

func TestClassify(t *testing.T) {
	wrapped := fmt.Errorf("outer: %w", fmt.Errorf("inner: %w", ErrNotFound))
	validation := fmt.Errorf("form: %w", &ValidationError{Field: "email"})
	cases := []struct {
		name string
		err  error
		want string
	}{
		{"nil", nil, "ok"},
		{"not_found", ErrNotFound, "not found"},
		{"wrapped_twice", wrapped, "not found"},
		{"validation", validation, "invalid email"},
		{"other", errors.New("boom"), "other"},
		{"joined", errors.Join(errors.New("x"), ErrNotFound), "not found"},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			if got := classify(c.err); got != c.want {
				t.Fatalf("classify(%v) = %q, ожидалось %q", c.err, got, c.want)
			}
		})
	}
}
