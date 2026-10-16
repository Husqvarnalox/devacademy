package iface

import "fmt"

// Describe описывает динамический тип и значение v:
//
//	nil        -> "nil"
//	int        -> "int 5"
//	string     -> `string "hi"` (через %q)
//	bool       -> "bool true"
//	[]int      -> "[]int len=3"
//	error      -> "error: <текст>"
//	fmt.Stringer -> "stringer: <String()>"
//	остальное  -> "other <%T>"
//
// Ошибки проверяются раньше Stringer.
func Describe(v any) string {
	// TODO: замените на type switch (switch x := v.(type))
	return fmt.Sprint(v)
}
