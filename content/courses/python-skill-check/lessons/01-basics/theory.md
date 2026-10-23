<!-- section: simple -->
## Что проверяет эта проверка

Базовый синтаксис Python: переменные и ввод, числа и операции `//` и `%`, преобразование типов, f-строки, чтение traceback, условия `if / elif / else`, циклы `while` и `for` с `range`, `break` и `continue`.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Первая программа и команда print](lesson:python.fundamentals.how-python-runs), [Переменные и ввод данных](lesson:python.fundamentals.variables-and-input), [Числа и арифметика](lesson:python.fundamentals.numbers-and-operators), [Текст, f-строки и длина строки](lesson:python.fundamentals.text-and-f-strings), [Как читать сообщения об ошибках](lesson:python.fundamentals.reading-errors), [Условия: if, elif, else](lesson:python.fundamentals.conditions), [Цикл while](lesson:python.fundamentals.while-loops), [Цикл for и range, вложенные циклы](lesson:python.fundamentals.loops).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```python run
total = 0
for n in range(1, 8):
    if n % 3 == 0:
        continue
    total += n
print(total, 17 // 5, 17 % 5)
```
```output
19 3 2
```

Цикл складывает числа от 1 до 7, пропуская кратные трём; затем показаны целочисленное деление и остаток.

<!-- section: precise -->
## Что нужно уметь

* предсказывать результат `/`, `//`, `%` и сложных присваиваний (`+=`, `//=`);
* различать строку и число (`"5" * 2` и `int("5") * 2`);
* писать цикл `while` с условием остановки и `for` по `range` с шагом;
* применять `break` и `continue`;
* составлять условия из `and`, `or`, `not` и сравнений;
* писать небольшую функцию с циклом и условиями.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* Конец `range` не включается: `range(1, 10, 3)` даёт 1, 4, 7.
* `continue` пропускает остаток тела, но не завершает цикл.
* `input()` возвращает строку; без `int()` арифметика не получится.
* Остаток от деления на 100 и 400 нужно проверять в правильном порядке (високосный год).

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Первая программа и команда print](lesson:python.fundamentals.how-python-runs), [Переменные и ввод данных](lesson:python.fundamentals.variables-and-input), [Числа и арифметика](lesson:python.fundamentals.numbers-and-operators), [Текст, f-строки и длина строки](lesson:python.fundamentals.text-and-f-strings), [Как читать сообщения об ошибках](lesson:python.fundamentals.reading-errors), [Условия: if, elif, else](lesson:python.fundamentals.conditions), [Цикл while](lesson:python.fundamentals.while-loops), [Цикл for и range, вложенные циклы](lesson:python.fundamentals.loops). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
