package pipe

// Generate отправляет nums в возвращаемый канал и закрывает его.
func Generate(nums ...int) <-chan int {
	out := make(chan int)
	go func() {
		// TODO: после отправки всех значений канал нужно закрыть,
		// иначе читатель через range будет ждать вечно
		for _, n := range nums {
			out <- n
		}
	}()
	return out
}

// Square читает in, отправляет квадраты и закрывает выход, когда in закрыт.
func Square(in <-chan int) <-chan int {
	// TODO
	return nil
}

// Merge объединяет каналы (fan-in). Выходной канал закрывается, когда
// закрыты и полностью прочитаны все входные. Используйте sync.WaitGroup.
func Merge(cs ...<-chan int) <-chan int {
	// TODO
	return nil
}

// Collect читает канал до закрытия и возвращает значения в порядке получения.
func Collect(ch <-chan int) []int {
	// TODO
	return nil
}

// SumConcurrent делит nums на parts примерно равных частей и суммирует каждую
// в своей горутине. parts <= 0 считается единицей.
func SumConcurrent(nums []int, parts int) int {
	// TODO
	return 0
}
