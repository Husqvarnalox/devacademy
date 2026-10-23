package main

import (
	"errors"
	"testing"
)

func TestAccountBasics(t *testing.T) {
	t.Run("new", func(t *testing.T) {
		a := NewAccount("anna", 50)
		if a == nil || a.Owner != "anna" || a.Balance() != 50 {
			t.Fatalf("NewAccount(\"anna\", 50) = %+v", a)
		}
	})
	t.Run("deposit", func(t *testing.T) {
		a := NewAccount("anna", 10)
		if err := a.Deposit(5); err != nil || a.Balance() != 15 {
			t.Fatalf("после Deposit(5): баланс %d, ошибка %v", a.Balance(), err)
		}
	})
	t.Run("withdraw", func(t *testing.T) {
		a := NewAccount("anna", 10)
		if err := a.Withdraw(4); err != nil || a.Balance() != 6 {
			t.Fatalf("после Withdraw(4): баланс %d, ошибка %v", a.Balance(), err)
		}
	})
	t.Run("withdraw_exact", func(t *testing.T) {
		a := NewAccount("anna", 10)
		if err := a.Withdraw(10); err != nil || a.Balance() != 0 {
			t.Fatalf("Withdraw всего остатка: баланс %d, ошибка %v", a.Balance(), err)
		}
	})
}

func TestAccountErrors(t *testing.T) {
	t.Run("deposit_invalid", func(t *testing.T) {
		a := NewAccount("a", 10)
		for _, n := range []int{0, -5} {
			if err := a.Deposit(n); !errors.Is(err, ErrInvalidAmount) {
				t.Fatalf("Deposit(%d): ошибка %v, ожидалась ErrInvalidAmount", n, err)
			}
		}
		if a.Balance() != 10 {
			t.Fatalf("баланс изменился после неудачных зачислений: %d", a.Balance())
		}
	})
	t.Run("withdraw_invalid", func(t *testing.T) {
		a := NewAccount("a", 10)
		if err := a.Withdraw(0); !errors.Is(err, ErrInvalidAmount) {
			t.Fatalf("Withdraw(0): ошибка %v, ожидалась ErrInvalidAmount", err)
		}
	})
	t.Run("withdraw_insufficient", func(t *testing.T) {
		a := NewAccount("a", 10)
		if err := a.Withdraw(11); !errors.Is(err, ErrInsufficient) {
			t.Fatalf("Withdraw(11): ошибка %v, ожидалась ErrInsufficient", err)
		}
		if a.Balance() != 10 {
			t.Fatalf("баланс изменился после неудачного списания: %d", a.Balance())
		}
	})
}

func TestTransfer(t *testing.T) {
	t.Run("success", func(t *testing.T) {
		a, b := NewAccount("a", 100), NewAccount("b", 5)
		if err := transfer(a, b, 30); err != nil {
			t.Fatalf("transfer: неожиданная ошибка %v", err)
		}
		if a.Balance() != 70 || b.Balance() != 35 {
			t.Fatalf("остатки %d и %d, ожидались 70 и 35 (счета передаются по указателю)", a.Balance(), b.Balance())
		}
	})
	t.Run("insufficient_leaves_both_unchanged", func(t *testing.T) {
		a, b := NewAccount("a", 10), NewAccount("b", 5)
		if err := transfer(a, b, 11); !errors.Is(err, ErrInsufficient) {
			t.Fatalf("transfer: ошибка %v, ожидалась ErrInsufficient", err)
		}
		if a.Balance() != 10 || b.Balance() != 5 {
			t.Fatalf("остатки изменились: %d и %d", a.Balance(), b.Balance())
		}
	})
	t.Run("invalid_amount", func(t *testing.T) {
		a, b := NewAccount("a", 10), NewAccount("b", 5)
		if err := transfer(a, b, -3); !errors.Is(err, ErrInvalidAmount) {
			t.Fatalf("transfer(-3): ошибка %v, ожидалась ErrInvalidAmount", err)
		}
		if a.Balance() != 10 || b.Balance() != 5 {
			t.Fatalf("остатки изменились: %d и %d", a.Balance(), b.Balance())
		}
	})
	t.Run("to_self", func(t *testing.T) {
		a := NewAccount("a", 10)
		if err := transfer(a, a, 4); err != nil || a.Balance() != 10 {
			t.Fatalf("перевод самому себе: баланс %d, ошибка %v", a.Balance(), err)
		}
	})
}
