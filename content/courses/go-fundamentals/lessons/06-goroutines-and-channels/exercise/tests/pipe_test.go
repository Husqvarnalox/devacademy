package pipe

import (
	"slices"
	"testing"
	"time"
)

// guard - только защита от зависания при ошибке в решении, а не средство
// синхронизации: корректные решения укладываются в него с огромным запасом.
const guard = 2 * time.Second

// feed отправляет значения в канал и закрывает его (независимо от решения).
func feed(vals ...int) <-chan int {
	ch := make(chan int)
	go func() {
		defer close(ch)
		for _, v := range vals {
			ch <- v
		}
	}()
	return ch
}

// drain читает канал до закрытия; если канал не закрылся за guard, тест падает.
func drain(t *testing.T, ch <-chan int) []int {
	t.Helper()
	if ch == nil {
		t.Fatal("получен nil-канал")
	}
	var out []int
	timeout := time.After(guard)
	for {
		select {
		case v, ok := <-ch:
			if !ok {
				return out
			}
			out = append(out, v)
		case <-timeout:
			t.Fatalf("канал не закрыт за %v; получено %v", guard, out)
			return nil
		}
	}
}

func TestGenerate(t *testing.T) {
	t.Run("values", func(t *testing.T) {
		if got := drain(t, Generate(3, 1, 2)); !slices.Equal(got, []int{3, 1, 2}) {
			t.Fatalf("got %v", got)
		}
	})
	t.Run("empty", func(t *testing.T) {
		if got := drain(t, Generate()); len(got) != 0 {
			t.Fatalf("got %v", got)
		}
	})
	t.Run("closes", func(t *testing.T) {
		ch := Generate(1)
		<-ch
		select {
		case _, ok := <-ch:
			if ok {
				t.Fatal("лишнее значение")
			}
		case <-time.After(guard):
			t.Fatal("канал не закрыт после последнего значения")
		}
	})
}

func TestSquare(t *testing.T) {
	t.Run("basic", func(t *testing.T) {
		if got := drain(t, Square(feed(1, 2, 3, -4))); !slices.Equal(got, []int{1, 4, 9, 16}) {
			t.Fatalf("got %v", got)
		}
	})
	t.Run("empty", func(t *testing.T) {
		if got := drain(t, Square(feed())); len(got) != 0 {
			t.Fatalf("got %v", got)
		}
	})
	t.Run("chain", func(t *testing.T) {
		if got := drain(t, Square(Square(feed(2, 3)))); !slices.Equal(got, []int{16, 81}) {
			t.Fatalf("got %v", got)
		}
	})
}

func TestMerge(t *testing.T) {
	t.Run("all-values", func(t *testing.T) {
		got := drain(t, Merge(feed(1, 2, 3), feed(10, 20), feed(100)))
		slices.Sort(got) // порядок между каналами не определён
		if !slices.Equal(got, []int{1, 2, 3, 10, 20, 100}) {
			t.Fatalf("got %v", got)
		}
	})
	t.Run("none", func(t *testing.T) {
		if got := drain(t, Merge()); len(got) != 0 {
			t.Fatalf("got %v", got)
		}
	})
	t.Run("order-within-channel", func(t *testing.T) {
		got := drain(t, Merge(feed(1, 2, 3, 4, 5)))
		if !slices.Equal(got, []int{1, 2, 3, 4, 5}) {
			t.Fatalf("got %v", got)
		}
	})
	t.Run("many", func(t *testing.T) {
		var cs []<-chan int
		want := 0
		for i := 1; i <= 20; i++ {
			cs = append(cs, feed(i, i, i))
			want += 3 * i
		}
		sum := 0
		for _, v := range drain(t, Merge(cs...)) {
			sum += v
		}
		if sum != want {
			t.Fatalf("sum = %d, want %d", sum, want)
		}
	})
}

func TestCollect(t *testing.T) {
	t.Run("basic", func(t *testing.T) {
		done := make(chan []int, 1)
		go func() { done <- Collect(feed(5, 6, 7)) }()
		select {
		case got := <-done:
			if !slices.Equal(got, []int{5, 6, 7}) {
				t.Fatalf("got %v", got)
			}
		case <-time.After(guard):
			t.Fatal("Collect не вернулся")
		}
	})
}

func TestSumConcurrent(t *testing.T) {
	nums := make([]int, 1000)
	want := 0
	for i := range nums {
		nums[i] = i + 1
		want += i + 1
	}
	t.Run("basic", func(t *testing.T) {
		if got := SumConcurrent(nums, 4); got != want {
			t.Fatalf("got %d, want %d", got, want)
		}
	})
	t.Run("parts-vary", func(t *testing.T) {
		for _, p := range []int{1, 3, 7, 999, 1000, 5000} {
			if got := SumConcurrent(nums, p); got != want {
				t.Fatalf("parts=%d: got %d, want %d", p, got, want)
			}
		}
	})
	t.Run("bad-parts", func(t *testing.T) {
		if got := SumConcurrent([]int{1, 2, 3}, 0); got != 6 {
			t.Fatalf("parts=0: got %d", got)
		}
		if got := SumConcurrent([]int{1, 2, 3}, -5); got != 6 {
			t.Fatalf("parts<0: got %d", got)
		}
	})
	t.Run("empty", func(t *testing.T) {
		if got := SumConcurrent(nil, 3); got != 0 {
			t.Fatalf("got %d", got)
		}
	})
}
