<!-- section: simple -->
## Что проверяет эта проверка

Базовый синтаксис Go: пакеты и `main`, переменные и нулевые значения, числовые типы и преобразования, целочисленное деление и остаток, `if`, `switch`, `for` с `break` и `continue`, ввод и вывод через `fmt`.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Первая программа](lesson:go.first-steps.first-program), [Как читать сообщения об ошибках](lesson:go.first-steps.compiler-errors), [Вывод: Println, Print и перевод строки](lesson:go.first-steps.output), [Переменные](lesson:go.first-steps.variables), [Базовые типы](lesson:go.first-steps.basic-types), [Арифметика](lesson:go.first-steps.arithmetic), [Чтение ввода](lesson:go.first-steps.reading-input), [Преобразование типов](lesson:go.first-steps.type-conversion), [Форматированный вывод](lesson:go.first-steps.formatted-output), [Константы и комментарии](lesson:go.first-steps.constants-and-comments), [Сравнения и логика](lesson:go.first-steps.comparison-and-logic), [Условие if / else](lesson:go.first-steps.if-else), [switch](lesson:go.first-steps.switch), [Цикл for](lesson:go.first-steps.for-loop), [Циклы с условием и управление ими](lesson:go.first-steps.loop-control), [Вложенные циклы](lesson:go.first-steps.nested-loops).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```go run
package main

import "fmt"

func main() {
	total := 0
	for i := 1; i <= 5; i++ {
		if i%2 == 0 {
			continue
		}
		total += i
	}
	fmt.Println(total, 7/2, float64(7)/2)
}
```
```output
9 3 3.5
```

Цикл суммирует нечётные числа до 5, затем показаны целочисленное деление и деление с явным преобразованием.

<!-- section: precise -->
Проверка предполагает умение:

* предсказывать результат `/` и `%` для целых и дробных значений;
* знать, что неиспользуемая переменная и импорт - ошибки компиляции;
* применять `if`, `switch` без проваливания и `for` в разных формах;
* приводить числовые типы явно;
* писать небольшую функцию с циклом и условиями.

<!-- section: mistakes -->
Как подготовиться и на чём чаще всего ошибаются.

* Целочисленное деление констант даёт целое даже при присваивании в `float64`.
* Остаток от деления отрицательного числа отрицателен.
* Ветки `switch` не проваливаются; `fallthrough` нужен явно.
* Повторение: см. список уроков в конце страницы.

<!-- section: summary -->
Если задания показались трудными, начните с уроков из списка: [Первая программа](lesson:go.first-steps.first-program), [Как читать сообщения об ошибках](lesson:go.first-steps.compiler-errors), [Вывод: Println, Print и перевод строки](lesson:go.first-steps.output), [Переменные](lesson:go.first-steps.variables), [Базовые типы](lesson:go.first-steps.basic-types), [Арифметика](lesson:go.first-steps.arithmetic), [Чтение ввода](lesson:go.first-steps.reading-input), [Преобразование типов](lesson:go.first-steps.type-conversion), [Форматированный вывод](lesson:go.first-steps.formatted-output), [Константы и комментарии](lesson:go.first-steps.constants-and-comments), [Сравнения и логика](lesson:go.first-steps.comparison-and-logic), [Условие if / else](lesson:go.first-steps.if-else), [switch](lesson:go.first-steps.switch), [Цикл for](lesson:go.first-steps.for-loop), [Циклы с условием и управление ими](lesson:go.first-steps.loop-control), [Вложенные циклы](lesson:go.first-steps.nested-loops). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
