package lru

import (
	"slices"
	"testing"
)

func TestLRU(t *testing.T) {
	t.Run("get-put", func(t *testing.T) {
		c := NewLRU(2)
		c.Put("a", 1)
		c.Put("b", 2)
		if v, ok := c.Get("a"); !ok || v != 1 {
			t.Fatalf("Get(a) = %d, %v", v, ok)
		}
		if _, ok := c.Get("zzz"); ok {
			t.Fatal("Get неизвестного ключа должен вернуть false")
		}
		if c.Len() != 2 {
			t.Fatalf("Len = %d", c.Len())
		}
	})
	t.Run("evict-oldest", func(t *testing.T) {
		c := NewLRU(2)
		c.Put("a", 1)
		c.Put("b", 2)
		c.Put("c", 3) // вытесняет a
		if _, ok := c.Get("a"); ok {
			t.Fatal("a должен быть вытеснен")
		}
		if got := c.Keys(); !slices.Equal(got, []string{"c", "b"}) {
			t.Fatalf("Keys = %v", got)
		}
	})
	t.Run("get-refreshes", func(t *testing.T) {
		c := NewLRU(2)
		c.Put("a", 1)
		c.Put("b", 2)
		c.Get("a")    // a свежее b
		c.Put("c", 3) // вытесняет b
		if _, ok := c.Get("b"); ok {
			t.Fatal("b должен быть вытеснен")
		}
		if got := c.Keys(); !slices.Equal(got, []string{"c", "a"}) {
			t.Fatalf("Keys = %v", got)
		}
	})
	t.Run("update-existing", func(t *testing.T) {
		c := NewLRU(2)
		c.Put("a", 1)
		c.Put("b", 2)
		c.Put("a", 10) // обновление, не вставка: размер прежний, a свежее
		if c.Len() != 2 {
			t.Fatalf("Len = %d", c.Len())
		}
		c.Put("c", 3) // вытесняет b
		if v, ok := c.Get("a"); !ok || v != 10 {
			t.Fatalf("Get(a) = %d, %v", v, ok)
		}
		if _, ok := c.Get("b"); ok {
			t.Fatal("b должен быть вытеснен")
		}
	})
	t.Run("capacity-one", func(t *testing.T) {
		c := NewLRU(1)
		c.Put("a", 1)
		c.Put("b", 2)
		if _, ok := c.Get("a"); ok || c.Len() != 1 {
			t.Fatalf("len=%d", c.Len())
		}
		c = NewLRU(0)
		c.Put("x", 1)
		c.Put("y", 2)
		if c.Len() != 1 {
			t.Fatalf("при capacity < 1 ёмкость равна 1, Len = %d", c.Len())
		}
	})
	t.Run("many", func(t *testing.T) {
		c := NewLRU(3)
		for i := 0; i < 100; i++ {
			c.Put(string(rune('a'+i%26)), i)
		}
		if c.Len() != 3 {
			t.Fatalf("Len = %d", c.Len())
		}
		if got := c.Keys(); !slices.Equal(got, []string{"v", "u", "t"}) {
			t.Fatalf("Keys = %v", got)
		}
	})
	t.Run("keys-is-copy", func(t *testing.T) {
		c := NewLRU(2)
		c.Put("a", 1)
		k := c.Keys()
		if len(k) != 1 {
			t.Fatalf("Keys = %v", k)
		}
		k[0] = "hacked"
		if got := c.Keys(); got[0] != "a" {
			t.Fatalf("Keys отдаёт внутренний срез: %v", got)
		}
	})
}
