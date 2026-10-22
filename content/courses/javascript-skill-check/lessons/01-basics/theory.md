<!-- section: simple -->
## Что проверяет эта проверка

Значения и типы, переменные `let` и `const`, арифметика и остаток от деления, склейка строк и приведение при `+`, строгое сравнение, `if / else`, циклы `while` и `for`, `break` и `continue`, функции с параметрами и `return`.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Первая программа](lesson:javascript.from-zero.first-program), [Как читать сообщения об ошибках](lesson:javascript.from-zero.reading-errors), [Значения и типы](lesson:javascript.from-zero.values-and-types), [Переменные](lesson:javascript.from-zero.variables), [Арифметика](lesson:javascript.from-zero.arithmetic), [Строки](lesson:javascript.from-zero.strings), [Сравнение и логика](lesson:javascript.from-zero.comparison-and-logic), [Функции](lesson:javascript.from-zero.functions), [Условия if и else](lesson:javascript.from-zero.if-else), [Цикл while](lesson:javascript.from-zero.while-loop), [Цикл for](lesson:javascript.from-zero.for-loop), [Управление циклом](lesson:javascript.from-zero.loop-control).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```javascript run
function sumDigits(n) {
  let total = 0;
  while (n > 0) {
    total += n % 10;
    n = Math.floor(n / 10);
  }
  return total;
}
console.log(sumDigits(4096), sumDigits(0));
for (let i = 1; i <= 6; i++) {
  if (i % 3 === 0) continue;
  console.log(i, i % 2 === 0 ? "чётное" : "нечётное");
}
console.log("7" + 1, "7" - 1, 7 === "7");
```
```output
19 0
1 нечётное
2 чётное
4 чётное
5 нечётное
71 6 false
```

Остаток `%` и `Math.floor` разбирают число по цифрам, `continue` пропускает шаг цикла, а `+` со строкой склеивает, тогда как `-` приводит строку к числу.

<!-- section: precise -->
## Что нужно уметь

* писать функцию с параметрами и `return` и вызывать её с разными значениями;
* выбирать между `while` и `for` и не ошибаться на границах (`<` и `<=`);
* использовать `%`, `Math.floor`, `Math.trunc` и понимать, что деление дробное;
* использовать `break` и `continue` и вложенные условия;
* предсказывать результат `+` для чисел и строк и сравнивать только через `===`;
* читать сообщения `ReferenceError` и `TypeError` и находить строку с ошибкой.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* `=` вместо `===` в условии и `==` вместо `===` при сравнении.
* Граница цикла на единицу: `i < n` вместо `i <= n`.
* Нет `return`: функция возвращает `undefined`.
* Деление `7 / 2` даёт `3.5`, а не `3`; целую часть берут через `Math.floor` или `Math.trunc`.
* `"5" + 3` - это `"53"`, а не `8`.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Первая программа](lesson:javascript.from-zero.first-program), [Как читать сообщения об ошибках](lesson:javascript.from-zero.reading-errors), [Значения и типы](lesson:javascript.from-zero.values-and-types), [Переменные](lesson:javascript.from-zero.variables), [Арифметика](lesson:javascript.from-zero.arithmetic), [Строки](lesson:javascript.from-zero.strings), [Сравнение и логика](lesson:javascript.from-zero.comparison-and-logic), [Функции](lesson:javascript.from-zero.functions), [Условия if и else](lesson:javascript.from-zero.if-else), [Цикл while](lesson:javascript.from-zero.while-loop), [Цикл for](lesson:javascript.from-zero.for-loop), [Управление циклом](lesson:javascript.from-zero.loop-control). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
