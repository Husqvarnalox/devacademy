package iface

import "fmt"

type NotFoundError struct {
	ID int
}

func (e *NotFoundError) Error() string {
	return fmt.Sprintf("id %d not found", e.ID)
}

var db = map[int]string{1: "ann", 2: "bob", 3: "cid"}

// Validate возвращает nil, если id известен, иначе *NotFoundError.
func Validate(id int) error {
	if _, ok := db[id]; !ok {
		return &NotFoundError{ID: id}
	}
	return nil // литерал nil, а не типизированный nil-указатель
}

func Find(id int) (string, error) {
	if err := Validate(id); err != nil {
		return "", fmt.Errorf("find: %w", err)
	}
	return db[id], nil
}
