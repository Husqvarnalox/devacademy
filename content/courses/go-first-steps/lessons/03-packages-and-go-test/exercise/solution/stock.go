// Package shop - учёт остатков склада. Ошибки возвращаются значениями и сравниваются через errors.Is.
package shop

import (
	"errors"
	"fmt"
	"strconv"
	"strings"
)

var (
	// ErrBadQuantity - количество должно быть положительным.
	ErrBadQuantity = errors.New("количество должно быть положительным")
	// ErrUnknownItem - товара нет на складе.
	ErrUnknownItem = errors.New("неизвестный товар")
	// ErrInsufficient - на складе меньше, чем запрошено.
	ErrInsufficient = errors.New("недостаточно товара")
)

// Reserve списывает qty единиц товара item со склада stock и возвращает остаток этого товара.
//
// Порядок проверок:
//  1. qty <= 0: ошибка, для которой errors.Is(err, ErrBadQuantity) истинно;
//  2. товара нет в карте: errors.Is(err, ErrUnknownItem);
//  3. остаток меньше qty: errors.Is(err, ErrInsufficient).
//
// В ошибках оборачивайте сентинел через fmt.Errorf с %w и добавляйте имя товара.
// При любой ошибке склад не меняется.
func Reserve(stock map[string]int, item string, qty int) (int, error) {
	if qty <= 0 {
		return 0, fmt.Errorf("резерв %q x%d: %w", item, qty, ErrBadQuantity)
	}
	have, ok := stock[item]
	if !ok {
		return 0, fmt.Errorf("резерв %q: %w", item, ErrUnknownItem)
	}
	if have < qty {
		return have, fmt.Errorf("резерв %q: нужно %d, есть %d: %w", item, qty, have, ErrInsufficient)
	}
	stock[item] = have - qty
	return stock[item], nil
}

// ParseOrder разбирает строку заказа вида "apple x3" на название и количество.
// Название - всё до последнего " x", после него идёт целое число.
// Лишние пробелы по краям строки игнорируются.
// Если формат нарушен или число не целое, возвращается ошибка. Нулевое и отрицательное количество
// допустимо для разбора: его отвергает уже Reserve.
func ParseOrder(line string) (string, int, error) {
	line = strings.TrimSpace(line)
	i := strings.LastIndex(line, " x")
	if i <= 0 {
		return "", 0, fmt.Errorf("заказ %q: ожидается формат \"название xN\"", line)
	}
	qty, err := strconv.Atoi(line[i+2:])
	if err != nil {
		return "", 0, fmt.Errorf("заказ %q: количество: %w", line, err)
	}
	return strings.TrimSpace(line[:i]), qty, nil
}
