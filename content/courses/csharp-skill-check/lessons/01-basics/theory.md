<!-- section: simple -->
## Что проверяет эта проверка

Переменные и типы, арифметика и остаток от деления, целочисленное деление, строки и склейка, `if / else` и `switch`, циклы `while` и `for`, `break` и `continue`, методы с параметрами и `return`.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Первая программа](lesson:csharp.from-zero.first-program), [Как читать сообщения компилятора](lesson:csharp.from-zero.compiler-errors), [Вывод: Write, WriteLine и специальные символы](lesson:csharp.from-zero.output), [Переменные и типы](lesson:csharp.from-zero.variables-and-types), [Арифметика с целыми числами](lesson:csharp.from-zero.arithmetic), [Дробные числа: double и decimal](lesson:csharp.from-zero.double-and-decimal), [Строки: склейка и интерполяция](lesson:csharp.from-zero.strings), [Сравнения и логика](lesson:csharp.from-zero.comparison-and-logic), [Условия: if / else](lesson:csharp.from-zero.if-else), [Выбор по значению: switch](lesson:csharp.from-zero.switch), [Цикл while](lesson:csharp.from-zero.while-loop), [Цикл for](lesson:csharp.from-zero.for-loop), [Управление циклом: break и continue](lesson:csharp.from-zero.loop-control), [Методы](lesson:csharp.from-zero.methods), [Результат метода: return](lesson:csharp.from-zero.return-values).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```csharp run
static int DigitSum(int n)
{
    int total = 0;
    while (n > 0)
    {
        total += n % 10;
        n /= 10;
    }
    return total;
}

Console.WriteLine($"{DigitSum(4096)} {DigitSum(0)}");
for (int i = 1; i <= 6; i++)
{
    if (i % 3 == 0)
    {
        continue;
    }
    string parity = i % 2 == 0 ? "чётное" : "нечётное";
    Console.WriteLine($"{i} {parity}");
}
Console.WriteLine("7" + 1);
Console.WriteLine(7 / 2 + " " + 7 % 2);
```
```output
19 0
1 нечётное
2 чётное
4 чётное
5 нечётное
71
3 1
```

Остаток `%` и деление `/` на `int` разбирают число по цифрам, `continue` пропускает шаг цикла, тернарный оператор выбирает одно из двух значений, а `+` со строкой склеивает, тогда как `/` и `%` работают с числами. Деление двух `int` целочисленное: `7 / 2` равно `3`.

<!-- section: precise -->
## Что нужно уметь

* писать метод с параметрами и `return` и вызывать его с разными значениями;
* выбирать между `while` и `for` и не ошибаться на границах (`<` и `<=`);
* использовать `%` и целочисленное деление, понимать, что `7 / 2` равно `3`;
* использовать `break` и `continue` и вложенные условия;
* предсказывать результат `+` для чисел и строк и приводить типы явно;
* читать сообщения компилятора `error CSxxxx` и находить строку с ошибкой.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* `=` вместо `==` в условии (для `int` это ошибка компиляции, для `bool` - предупреждение).
* Граница цикла на единицу: `i < n` вместо `i <= n`.
* Метод без `return` на одном из путей не собирается: `not all code paths return a value`.
* Деление `7 / 2` даёт `3`, а не `3.5`; для дробного результата хотя бы один операнд должен быть `double`.
* `"5" + 3` - это `"53"`, а не `8`.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Первая программа](lesson:csharp.from-zero.first-program), [Как читать сообщения компилятора](lesson:csharp.from-zero.compiler-errors), [Вывод: Write, WriteLine и специальные символы](lesson:csharp.from-zero.output), [Переменные и типы](lesson:csharp.from-zero.variables-and-types), [Арифметика с целыми числами](lesson:csharp.from-zero.arithmetic), [Дробные числа: double и decimal](lesson:csharp.from-zero.double-and-decimal), [Строки: склейка и интерполяция](lesson:csharp.from-zero.strings), [Сравнения и логика](lesson:csharp.from-zero.comparison-and-logic), [Условия: if / else](lesson:csharp.from-zero.if-else), [Выбор по значению: switch](lesson:csharp.from-zero.switch), [Цикл while](lesson:csharp.from-zero.while-loop), [Цикл for](lesson:csharp.from-zero.for-loop), [Управление циклом: break и continue](lesson:csharp.from-zero.loop-control), [Методы](lesson:csharp.from-zero.methods), [Результат метода: return](lesson:csharp.from-zero.return-values). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
