package main

import "testing"

func TestGreet(t *testing.T) {
	got := Greet("Ada")
	want := "Hello, Ada!"
	if got != want {
		t.Fatalf("Greet() = %q, want %q", got, want)
	}
}
