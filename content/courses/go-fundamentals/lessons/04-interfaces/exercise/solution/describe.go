package iface

import "fmt"

// Describe описывает динамический тип и значение v.
func Describe(v any) string {
	switch x := v.(type) {
	case nil:
		return "nil"
	case int:
		return fmt.Sprintf("int %d", x)
	case string:
		return fmt.Sprintf("string %q", x)
	case bool:
		return fmt.Sprintf("bool %t", x)
	case []int:
		return fmt.Sprintf("[]int len=%d", len(x))
	case error:
		return "error: " + x.Error()
	case fmt.Stringer:
		return "stringer: " + x.String()
	default:
		return fmt.Sprintf("other %T", x)
	}
}
