package iface

import "fmt"

type NotFoundError struct {
	ID int
}

func (e *NotFoundError) Error() string {
	return fmt.Sprintf("id %d not found", e.ID)
}

var db = map[int]string{1: "ann", 2: "bob", 3: "cid"}

// Validate должен возвращать nil, если id известен, иначе *NotFoundError.
func Validate(id int) error {
	var err *NotFoundError
	if _, ok := db[id]; !ok {
		err = &NotFoundError{ID: id}
	}
	// TODO: err имеет тип *NotFoundError; интерфейс error, собранный из
	// nil-указателя, НЕ равен nil. Верните ошибку без этой ловушки.
	return err
}

func Find(id int) (string, error) {
	if err := Validate(id); err != nil {
		return "", fmt.Errorf("find: %w", err)
	}
	return db[id], nil
}
