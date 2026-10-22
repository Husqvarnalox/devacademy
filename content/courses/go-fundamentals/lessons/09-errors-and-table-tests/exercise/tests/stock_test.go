package shop

import (
	"errors"
	"testing"
)

func TestReserve(t *testing.T) {
	tests := []struct {
		name      string
		stock     map[string]int
		item      string
		qty       int
		wantLeft  int
		wantErr   error
		wantStock int // остаток товара на складе после вызова
	}{
		{"ok", map[string]int{"tea": 5}, "tea", 2, 3, nil, 3},
		{"all", map[string]int{"tea": 5}, "tea", 5, 0, nil, 0},
		{"unknown", map[string]int{"tea": 5}, "milk", 1, 0, ErrUnknownItem, -1},
		{"insufficient", map[string]int{"tea": 5}, "tea", 6, 5, ErrInsufficient, 5},
		{"zero", map[string]int{"tea": 5}, "tea", 0, 0, ErrBadQuantity, 5},
		{"negative", map[string]int{"tea": 5}, "tea", -3, 0, ErrBadQuantity, 5},
		{"bad-before-unknown", map[string]int{}, "x", 0, 0, ErrBadQuantity, -1},
	}
	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			got, err := Reserve(tc.stock, tc.item, tc.qty)
			if tc.wantErr == nil {
				if err != nil {
					t.Fatalf("неожиданная ошибка: %v", err)
				}
				if got != tc.wantLeft {
					t.Fatalf("остаток = %d, want %d", got, tc.wantLeft)
				}
			} else {
				if !errors.Is(err, tc.wantErr) {
					t.Fatalf("err = %v, want errors.Is(%v)", err, tc.wantErr)
				}
			}
			if tc.wantStock >= 0 {
				if s := tc.stock[tc.item]; s != tc.wantStock {
					t.Fatalf("на складе %d, want %d", s, tc.wantStock)
				}
			}
		})
	}
}

func TestReserveErrorNames(t *testing.T) {
	_, err := Reserve(map[string]int{"tea": 1}, "coffee", 1)
	if err == nil || !errors.Is(err, ErrUnknownItem) {
		t.Fatalf("ожидалась ErrUnknownItem, получено %v", err)
	}
	if msg := err.Error(); len(msg) <= len(ErrUnknownItem.Error()) {
		t.Fatalf("в сообщении нет имени товара: %q", msg)
	}
}

func TestParseOrder(t *testing.T) {
	tests := []struct {
		name     string
		line     string
		wantItem string
		wantQty  int
		wantErr  bool
	}{
		{"simple", "apple x3", "apple", 3, false},
		{"spaces", "  green tea x12  ", "green tea", 12, false},
		{"x-in-name", "box x-ray x2", "box x-ray", 2, false},
		{"zero-allowed", "apple x0", "apple", 0, false},
		{"no-qty", "apple", "", 0, true},
		{"not-number", "apple xN", "", 0, true},
		{"empty", "", "", 0, true},
		{"only-qty", " x5", "", 0, true},
	}
	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			item, qty, err := ParseOrder(tc.line)
			if (err != nil) != tc.wantErr {
				t.Fatalf("err = %v, wantErr = %v", err, tc.wantErr)
			}
			if err == nil && (item != tc.wantItem || qty != tc.wantQty) {
				t.Fatalf("got (%q, %d), want (%q, %d)", item, qty, tc.wantItem, tc.wantQty)
			}
		})
	}
}
