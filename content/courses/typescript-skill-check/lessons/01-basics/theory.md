<!-- section: simple -->
## Что проверяет эта проверка

Типы значений и аннотации, переменные `let` и `const`, арифметика и остаток от деления, склейка строк и сообщения проверки типов, строгое сравнение, `if / else`, циклы `while` и `for`, `break` и `continue`, функции с типизированными параметрами и `return`.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Первая программа](lesson:typescript.from-zero.first-program), [Чтение ошибок](lesson:typescript.from-zero.reading-errors), [Значения и типы](lesson:typescript.from-zero.values-and-types), [Переменные](lesson:typescript.from-zero.variables), [Проверка типов](lesson:typescript.from-zero.type-checking), [Арифметика](lesson:typescript.from-zero.arithmetic), [Строки](lesson:typescript.from-zero.strings), [Сравнение и логика](lesson:typescript.from-zero.comparison-and-logic), [Функции](lesson:typescript.from-zero.functions), [Условия if/else](lesson:typescript.from-zero.if-else), [Цикл while](lesson:typescript.from-zero.while-loop), [Цикл for](lesson:typescript.from-zero.for-loop), [Управление циклами](lesson:typescript.from-zero.loop-control).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```typescript run
function factorial(n: number): number {
  let result: number = 1;
  for (let i = 2; i <= n; i++) {
    result *= i;
  }
  return result;
}
console.log(factorial(5), factorial(0));
for (let i = 1; i <= 6; i++) {
  if (i % 3 === 0) {
    continue;
  }
  console.log(i, i % 2 === 0 ? "чётное" : "нечётное");
}
console.log("7" + 1, Number("7") - 1, 7 === Number("7"));
```
```output
120 1
1 нечётное
2 чётное
4 чётное
5 нечётное
71 6 true
```

Параметр и результат функции получают типы, а цикл `for` с `continue` пропускает ненужные значения. Плюс со строкой склеивает, поэтому `"7" + 1` даёт `"71"`; для вычитания строку сначала переводят в число через `Number`. Строгое сравнение `===` не приводит типы.

<!-- section: precise -->
## Что нужно уметь

* писать функцию с аннотированными параметрами и результатом и вызывать её с разными значениями;
* различать `number`, `string` и `boolean` и объяснять ошибку `TS2322` «тип нельзя присвоить типу»;
* предсказывать `/`, `%`, `**`, `Math.floor`, `Math.round` и склейку через `+`;
* писать условия с `&&`, `||`, `!` и сравнением `===`;
* выбирать между `while` и `for`, использовать `break` и `continue`;
* читать сообщения `tsc`: файл, строка, код ошибки и причина.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* `7 / 2` в TypeScript равно `3.5`: отдельного целочисленного деления нет, целая часть берётся через `Math.floor` или `Math.trunc`.
* Значение `"5"` в кавычках - строка, и `"5" + 3` даёт `"53"`; присвоить его переменной `number` проверка не даст.
* Остаток от деления для отрицательных чисел сохраняет знак делимого: `-7 % 3` равно `-1`, поэтому проверка нечётности через `n % 2 === 1` не работает для отрицательных.
* Забыт `return` в одной из веток: проверка сообщает, что не все пути возвращают значение.
* Условие цикла `while` не меняется внутри тела, и программа не завершается.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Первая программа](lesson:typescript.from-zero.first-program), [Чтение ошибок](lesson:typescript.from-zero.reading-errors), [Значения и типы](lesson:typescript.from-zero.values-and-types), [Переменные](lesson:typescript.from-zero.variables), [Проверка типов](lesson:typescript.from-zero.type-checking), [Арифметика](lesson:typescript.from-zero.arithmetic), [Строки](lesson:typescript.from-zero.strings), [Сравнение и логика](lesson:typescript.from-zero.comparison-and-logic), [Функции](lesson:typescript.from-zero.functions), [Условия if/else](lesson:typescript.from-zero.if-else), [Цикл while](lesson:typescript.from-zero.while-loop), [Цикл for](lesson:typescript.from-zero.for-loop), [Управление циклами](lesson:typescript.from-zero.loop-control). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
