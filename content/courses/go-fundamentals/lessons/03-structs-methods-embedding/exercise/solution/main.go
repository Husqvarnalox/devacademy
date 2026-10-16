package main

import (
	"bufio"
	"fmt"
	"os"
	"strconv"
	"strings"
)

// Logger - встраиваемый журнал операций. Нулевое значение готово к работе.
type Logger struct {
	lines []string
}

func (l *Logger) Logf(format string, args ...any) {
	l.lines = append(l.lines, fmt.Sprintf(format, args...))
}

func (l Logger) Count() int { return len(l.lines) }

func (l Logger) History() string { return strings.Join(l.lines, "; ") }

// Account встраивает Logger: методы Logf, Count и History "поднимаются" в Account.
type Account struct {
	Logger
	ID      string
	Balance int
}

// NewAccount - конструктор по соглашению: возвращает указатель.
func NewAccount(id string) *Account {
	return &Account{ID: id}
}

func (a *Account) Deposit(n int) {
	a.Balance += n
	a.Logf("deposit %d", n)
}

func (a *Account) Withdraw(n int) error {
	if n > a.Balance {
		return fmt.Errorf("insufficient funds: balance %d, need %d", a.Balance, n)
	}
	a.Balance -= n
	a.Logf("withdraw %d", n)
	return nil
}

func (a Account) String() string {
	return fmt.Sprintf("%s: balance=%d ops=%d [%s]", a.ID, a.Balance, a.Count(), a.History())
}

func main() {
	accounts := map[string]*Account{}
	sc := bufio.NewScanner(os.Stdin)
	for sc.Scan() {
		f := strings.Fields(sc.Text())
		if len(f) < 2 {
			continue
		}
		cmd, id := f[0], f[1]
		if cmd == "open" {
			if _, exists := accounts[id]; exists {
				fmt.Printf("%s already exists\n", id)
				continue
			}
			accounts[id] = NewAccount(id)
			fmt.Printf("opened %s\n", id)
			continue
		}
		acc, ok := accounts[id]
		if !ok {
			fmt.Printf("unknown account %s\n", id)
			continue
		}
		switch cmd {
		case "deposit":
			n, _ := strconv.Atoi(f[2])
			acc.Deposit(n)
			fmt.Printf("%s +%d -> %d\n", id, n, acc.Balance)
		case "withdraw":
			n, _ := strconv.Atoi(f[2])
			if err := acc.Withdraw(n); err != nil {
				fmt.Printf("%s: %v\n", id, err)
				continue
			}
			fmt.Printf("%s -%d -> %d\n", id, n, acc.Balance)
		case "show":
			fmt.Println(acc)
		}
	}
}
