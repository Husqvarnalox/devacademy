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

// Account - банковский счёт. Поле balance скрыто: менять его можно только методами.
type Account struct {
	Owner   string
	balance int
}

// NewAccount создаёт счёт с начальным остатком и возвращает указатель на него.
func NewAccount(owner string, initial int) *Account {
	return nil
}

// Balance возвращает текущий остаток.
func (a *Account) Balance() int {
	return 0
}

// Deposit зачисляет n (n > 0, иначе ErrInvalidAmount).
func (a *Account) Deposit(n int) error {
	return errors.New("Deposit не написан")
}

// Withdraw списывает n (n > 0, иначе ErrInvalidAmount; n больше остатка - ErrInsufficient).
func (a *Account) Withdraw(n int) error {
	return errors.New("Withdraw не написан")
}

// transfer переводит n со счёта from на счёт to; при ошибке остатки не меняются.
func transfer(from, to *Account, n int) error {
	return errors.New("transfer не написана")
}

func main() {
	a := NewAccount("anna", 100)
	b := NewAccount("boris", 0)
	if a != nil && b != nil {
		fmt.Println(transfer(a, b, 30), a.Balance(), b.Balance())
	}
}
