package main

import (
	"io"
	"os"
	"testing"
)

// capture запускает f и возвращает всё, что f напечатала в стандартный вывод.
func capture(t *testing.T, f func()) string {
	t.Helper()
	old := os.Stdout
	r, w, err := os.Pipe()
	if err != nil {
		t.Fatal(err)
	}
	os.Stdout = w
	defer func() { os.Stdout = old }()
	f()
	w.Close()
	out, _ := io.ReadAll(r)
	return string(out)
}

func TestGreet(t *testing.T) {
	cases := []struct {
		name string
		arg  string
		want string
	}{
		{"cyrillic_name", "Анна", "Здравствуйте, Анна!\n"},
		{"latin_name", "Bob", "Здравствуйте, Bob!\n"},
		{"name_with_space", "Мария Ивановна", "Здравствуйте, Мария Ивановна!\n"},
		{"empty_name", "", "Здравствуйте, !\n"},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			got := capture(t, func() { greet(c.arg) })
			if got != c.want {
				t.Fatalf("greet(%q) напечатала %q, ожидалось %q", c.arg, got, c.want)
			}
		})
	}
}

func TestRepeatLine(t *testing.T) {
	cases := []struct {
		name  string
		text  string
		times int
		want  string
	}{
		{"three_times", "Go", 3, "Go\nGo\nGo\n"},
		{"once", "привет", 1, "привет\n"},
		{"zero_times", "x", 0, ""},
		{"negative_times", "x", -2, ""},
		{"five_times", "ab", 5, "ab\nab\nab\nab\nab\n"},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			got := capture(t, func() { repeatLine(c.text, c.times) })
			if got != c.want {
				t.Fatalf("repeatLine(%q, %d) напечатала %q, ожидалось %q", c.text, c.times, got, c.want)
			}
		})
	}
}
