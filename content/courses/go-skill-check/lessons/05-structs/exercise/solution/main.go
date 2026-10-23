package main

import (
	"errors"
	"fmt"
)

// Ошибки операций со счётом.
var (
	ErrInvalidAmount = errors.New("amount must be positive")
	ErrInsufficient  = errors.New("insufficient funds")
)

// Account - банковский счёт.
type Account struct {
	Owner   string
	balance int
}

// NewAccount создаёт счёт с начальным остатком.
func NewAccount(owner string, initial int) *Account {
	return &Account{Owner: owner, balance: initial}
}

// Balance возвращает текущий остаток.
func (a *Account) Balance() int { return a.balance }

// Deposit зачисляет n; n должно быть положительным.
func (a *Account) Deposit(n int) error {
	if n <= 0 {
		return ErrInvalidAmount
	}
	a.balance += n
	return nil
}

// Withdraw списывает n; нельзя списать больше остатка.
func (a *Account) Withdraw(n int) error {
	if n <= 0 {
		return ErrInvalidAmount
	}
	if n > a.balance {
		return ErrInsufficient
	}
	a.balance -= n
	return nil
}

// transfer переводит n со счёта from на счёт to; при ошибке остатки не меняются.
func transfer(from, to *Account, n int) error {
	if err := from.Withdraw(n); err != nil {
		return err
	}
	return to.Deposit(n)
}

func main() {
	a := NewAccount("anna", 100)
	b := NewAccount("boris", 0)
	fmt.Println(transfer(a, b, 30), a.Balance(), b.Balance())
}
