<!-- section: simple -->
## Что проверяет эта проверка

Переменные и типы `int`, `double`, `boolean`, `char`, `String`, арифметика и остаток от деления, целочисленное деление и приведение типов, чтение ввода через `Scanner`, форматированный вывод, сравнение и логические операторы, `if / else` и `switch`, циклы `while` и `for`, `break` и `continue`, чтение сообщений компилятора.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Первая программа](lesson:java.from-zero.first-program), [Как читать сообщения об ошибках](lesson:java.from-zero.compiler-errors), [Вывод: print, println и соединение текста](lesson:java.from-zero.output), [Переменные](lesson:java.from-zero.variables), [Типы данных: int, double, boolean, char, String](lesson:java.from-zero.basic-types), [Арифметика](lesson:java.from-zero.arithmetic), [Строки: длина, буквы и регистр](lesson:java.from-zero.strings), [Чтение ввода: Scanner](lesson:java.from-zero.reading-input), [Преобразование типов](lesson:java.from-zero.type-conversion), [Форматированный вывод](lesson:java.from-zero.formatted-output), [Сравнение и логические выражения](lesson:java.from-zero.comparison-and-logic), [Условие: if, else if и else](lesson:java.from-zero.if-else), [Выбор из многих вариантов: switch](lesson:java.from-zero.switch), [Цикл while: повторение, пока условие верно](lesson:java.from-zero.while-loop), [Цикл for: счётчик в заголовке](lesson:java.from-zero.for-loop), [break, continue и вложенные циклы](lesson:java.from-zero.loop-control).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```java run
import java.util.Scanner;

public class Main {
    public static void main(String[] args) {
        Scanner in = new Scanner("4 7 10 3");
        int sum = 0;
        int odd = 0;
        while (in.hasNextInt()) {
            int x = in.nextInt();
            sum += x;
            if (x % 2 != 0) {
                odd++;
            }
        }
        System.out.println(sum + " " + odd + " " + sum / 4 + " " + sum / 4.0);
        for (int i = 1; i <= 6; i++) {
            if (i % 3 == 0) {
                continue;
            }
            System.out.println(i + (i % 2 == 0 ? " чётное" : " нечётное"));
        }
    }
}
```
```output
24 2 6 6.0
1 нечётное
2 чётное
4 чётное
5 нечётное
```
Цикл `while (in.hasNextInt())` читает числа, пока они есть; `%` определяет чётность, а `continue` пропускает шаг цикла. Деление целых даёт целое (`24 / 4` здесь делится нацело, но `sum / 4` для другой суммы отбросило бы дробную часть), а `sum / 4.0` - дробное.

<!-- section: precise -->
## Что нужно уметь

* читать числа до конца ввода через `hasNextInt` и `nextInt` и хранить сумму, счётчики и максимум в переменных;
* начинать максимум с первого прочитанного числа, а не с нуля, чтобы он был верным и для отрицательных чисел;
* использовать `%` и сравнение с нулём (`x % 2 == 0`) для проверки чётности, в том числе для отрицательных чисел;
* помнить, что деление двух `int` целочисленное, и выбирать `double`, когда нужна дробь;
* выбирать тип по размеру значений и замечать переполнение `int`;
* предсказывать работу `break` и `continue` во вложенных условиях и циклах;
* читать сообщения `javac` и находить строку с ошибкой.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* Проверка нечётности через `x % 2 == 1`: для `-3` остаток равен `-1`, и условие не срабатывает.
* Максимум, начатый с `0`, неверен для списка из одних отрицательных чисел.
* `sum / count` с целыми `sum` и `count` отбрасывает дробную часть; для пустого ввода деление на ноль бросает исключение.
* Граница цикла на единицу: `i < n` вместо `i <= n`.
* `=` вместо `==` в условии не скомпилируется, а `&&` и `||` с неверным приоритетом молча дают другой результат.
* `1_000_000 * 3000` вычисляется в `int` и переполняется, даже если результат присваивается `long`.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Первая программа](lesson:java.from-zero.first-program), [Как читать сообщения об ошибках](lesson:java.from-zero.compiler-errors), [Вывод: print, println и соединение текста](lesson:java.from-zero.output), [Переменные](lesson:java.from-zero.variables), [Типы данных: int, double, boolean, char, String](lesson:java.from-zero.basic-types), [Арифметика](lesson:java.from-zero.arithmetic), [Строки: длина, буквы и регистр](lesson:java.from-zero.strings), [Чтение ввода: Scanner](lesson:java.from-zero.reading-input), [Преобразование типов](lesson:java.from-zero.type-conversion), [Форматированный вывод](lesson:java.from-zero.formatted-output), [Сравнение и логические выражения](lesson:java.from-zero.comparison-and-logic), [Условие: if, else if и else](lesson:java.from-zero.if-else), [Выбор из многих вариантов: switch](lesson:java.from-zero.switch), [Цикл while: повторение, пока условие верно](lesson:java.from-zero.while-loop), [Цикл for: счётчик в заголовке](lesson:java.from-zero.for-loop), [break, continue и вложенные циклы](lesson:java.from-zero.loop-control). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
