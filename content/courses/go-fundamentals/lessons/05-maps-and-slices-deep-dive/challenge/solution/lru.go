package lru

import "container/list"

type entry struct {
	key string
	val int
}

// LRU - кеш фиксированной ёмкости с вытеснением давно не использованных записей.
type LRU struct {
	cap   int
	order *list.List               // front - самая свежая запись
	items map[string]*list.Element // ключ -> элемент списка
}

// NewLRU создаёт кеш на capacity записей (capacity < 1 считается единицей).
func NewLRU(capacity int) *LRU {
	return &LRU{
		cap:   max(capacity, 1),
		order: list.New(),
		items: make(map[string]*list.Element),
	}
}

// Get возвращает значение и делает запись самой свежей.
func (c *LRU) Get(key string) (int, bool) {
	el, ok := c.items[key]
	if !ok {
		return 0, false
	}
	c.order.MoveToFront(el)
	return el.Value.(*entry).val, true
}

// Put добавляет или обновляет запись (она становится самой свежей).
// При переполнении вытесняется самая давняя запись.
func (c *LRU) Put(key string, val int) {
	if el, ok := c.items[key]; ok {
		el.Value.(*entry).val = val
		c.order.MoveToFront(el)
		return
	}
	c.items[key] = c.order.PushFront(&entry{key, val})
	if c.order.Len() > c.cap {
		last := c.order.Back()
		c.order.Remove(last)
		delete(c.items, last.Value.(*entry).key)
	}
}

// Len возвращает число записей.
func (c *LRU) Len() int { return c.order.Len() }

// Keys возвращает ключи от самой свежей записи к самой давней.
func (c *LRU) Keys() []string {
	keys := make([]string, 0, c.order.Len())
	for el := c.order.Front(); el != nil; el = el.Next() {
		keys = append(keys, el.Value.(*entry).key)
	}
	return keys
}
