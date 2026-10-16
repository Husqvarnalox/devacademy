package lru

// LRU - кеш фиксированной ёмкости с вытеснением давно не использованных записей.
//
// Подсказка по устройству: карта ключ -> элемент двусвязного списка
// (container/list) даёт O(1) на Get и Put; в начале списка - самая свежая запись.
type LRU struct {
	cap int
	// TODO: добавьте нужные поля
}

// NewLRU создаёт кеш на capacity записей (capacity < 1 считается единицей).
func NewLRU(capacity int) *LRU {
	// TODO
	return &LRU{cap: capacity}
}

// Get возвращает значение и делает запись самой свежей.
func (c *LRU) Get(key string) (int, bool) {
	// TODO
	return 0, false
}

// Put добавляет или обновляет запись (она становится самой свежей).
// При переполнении вытесняется самая давняя запись.
func (c *LRU) Put(key string, val int) {
	// TODO
}

// Len возвращает число записей.
func (c *LRU) Len() int {
	// TODO
	return 0
}

// Keys возвращает ключи от самой свежей записи к самой давней.
func (c *LRU) Keys() []string {
	// TODO
	return nil
}
