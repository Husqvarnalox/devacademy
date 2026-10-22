<!-- section: simple -->
## Что проверяет эта проверка

Значения и типы, арифметика и остаток от деления, `intdiv`, строки и склейка, `if / elseif / else`, циклы `while` и `for`, `break` и `continue`, функции с параметрами и `return`.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Первая программа](lesson:php.from-zero.first-program), [Как читать сообщения об ошибках](lesson:php.from-zero.reading-errors), [Вывод: несколько значений, \n, \t и кавычки](lesson:php.from-zero.output), [Переменные](lesson:php.from-zero.variables), [Значения и их типы](lesson:php.from-zero.values-and-types), [Арифметика](lesson:php.from-zero.arithmetic), [Строки: склейка, длина, регистр и UTF-8](lesson:php.from-zero.strings), [Чтение ввода: fgets и trim](lesson:php.from-zero.reading-input), [Преобразование типов и проверка чисел](lesson:php.from-zero.type-conversion), [Форматированный вывод: printf и number_format](lesson:php.from-zero.formatted-output), [Сравнения и логические операции](lesson:php.from-zero.comparison-and-logic), [Условия: if, elseif, else](lesson:php.from-zero.if-else), [Цикл while](lesson:php.from-zero.while-loop), [Цикл for](lesson:php.from-zero.for-loop), [break, continue и вложенные циклы](lesson:php.from-zero.loop-control), [Функции, параметры и strict_types](lesson:php.from-zero.functions).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```php run
<?php
declare(strict_types=1);

function digitSum(int $n): int
{
    $total = 0;
    while ($n > 0) {
        $total += $n % 10;
        $n = intdiv($n, 10);
    }
    return $total;
}

echo digitSum(4096), " ", digitSum(0), "\n";
for ($i = 1; $i <= 6; $i++) {
    if ($i % 3 === 0) {
        continue;
    }
    echo $i, $i % 2 === 0 ? " чётное" : " нечётное", "\n";
}
echo 7 / 2, " ", intdiv(7, 2), " ", 7 % 2, " ", 1 + 2 . "3", "\n";
```
```output
19 0
1 нечётное
2 чётное
4 чётное
5 нечётное
3.5 3 1 33
```

Остаток `%` и `intdiv` разбирают число по цифрам, `continue` пропускает шаг цикла, условный оператор выбирает одно из двух значений. Обычное деление `/` даёт `3.5`, а `intdiv` отбрасывает дробную часть. В PHP 8 `+` старше склейки `.`, поэтому `1 + 2 . "3"` - это `"33"`.

<!-- section: precise -->
## Что нужно уметь

* писать функцию с типизированными параметрами, значением по умолчанию и `return` и вызывать её с разными значениями;
* выбирать между `while` и `for` и не ошибаться на границах (`<` и `<=`);
* использовать `%` и `intdiv`, понимать, что `7 / 2` равно `3.5`, а `-7 % 3` равно `-1`;
* использовать `break` и `continue` и вложенные условия;
* сравнивать через `===`, а не `==`, и предсказывать результат `+` и `.`;
* читать сообщения PHP (`Parse error`, `Fatal error`, `Warning`) и находить строку с ошибкой.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* `=` вместо `===` в условии: присваивание тихо портит программу.
* Граница цикла на единицу: `$i < $n` вместо `$i <= $n`.
* Функция без `return` на одном из путей: для результата `int` это `TypeError`.
* Деление `7 / 2` даёт `3.5`, а не `3`; для целой части нужен `intdiv`.
* Результат функции типа `string` должен быть строкой: `return $n;` в строгом режиме - ошибка, нужно `(string) $n`.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Первая программа](lesson:php.from-zero.first-program), [Как читать сообщения об ошибках](lesson:php.from-zero.reading-errors), [Вывод: несколько значений, \n, \t и кавычки](lesson:php.from-zero.output), [Переменные](lesson:php.from-zero.variables), [Значения и их типы](lesson:php.from-zero.values-and-types), [Арифметика](lesson:php.from-zero.arithmetic), [Строки: склейка, длина, регистр и UTF-8](lesson:php.from-zero.strings), [Чтение ввода: fgets и trim](lesson:php.from-zero.reading-input), [Преобразование типов и проверка чисел](lesson:php.from-zero.type-conversion), [Форматированный вывод: printf и number_format](lesson:php.from-zero.formatted-output), [Сравнения и логические операции](lesson:php.from-zero.comparison-and-logic), [Условия: if, elseif, else](lesson:php.from-zero.if-else), [Цикл while](lesson:php.from-zero.while-loop), [Цикл for](lesson:php.from-zero.for-loop), [break, continue и вложенные циклы](lesson:php.from-zero.loop-control), [Функции, параметры и strict_types](lesson:php.from-zero.functions). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
