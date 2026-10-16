package slug_test

import (
	"fmt"

	"example.com/textkit/slug"
)

func ExampleSlugify() {
	fmt.Println(slug.Slugify("Hello, World!"))
	// Output: hello-world
}

func ExampleUnique() {
	taken := map[string]bool{"intro": true}
	fmt.Println(slug.Unique("intro", taken))
	// Output: intro-2
}
