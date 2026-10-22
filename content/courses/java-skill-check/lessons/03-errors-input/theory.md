<!-- section: simple -->
## Что проверяет эта проверка

Чтение строк до конца ввода (`hasNextLine`, `nextLine`), порядок вызовов `nextInt` и `nextLine`, `Integer.parseInt` и `NumberFormatException`, `try/catch`, проверка допустимых значений, подсчёт неверных строк и форматированный вывод с `Locale.ROOT`.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Чтение ввода: Scanner](lesson:java.from-zero.reading-input), [Преобразование типов](lesson:java.from-zero.type-conversion), [Исключения: try, catch и throw](lesson:java.from-zero.exceptions).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```java run
import java.util.Locale;

public class Main {
    static int parseScore(String text) {
        try {
            int v = Integer.parseInt(text.trim());
            if (v < 0 || v > 100) {
                throw new IllegalArgumentException("вне диапазона: " + v);
            }
            return v;
        } catch (NumberFormatException e) {
            throw new IllegalArgumentException("не число: " + text);
        }
    }

    public static void main(String[] args) {
        int bad = 0;
        int sum = 0;
        int ok = 0;
        for (String t : new String[] {"90", " 75 ", "abc", "101", ""}) {
            try {
                sum += parseScore(t);
                ok++;
            } catch (IllegalArgumentException e) {
                bad++;
                System.out.println("пропуск: " + e.getMessage());
            }
        }
        System.out.printf(Locale.ROOT, "принято %d, ошибок %d, среднее %.2f%n", ok, bad, (double) sum / ok);
    }
}
```
```output
пропуск: не число: abc
пропуск: вне диапазона: 101
пропуск: не число: 
принято 2, ошибок 3, среднее 82.50
```
`parseInt` бросает `NumberFormatException` для текста с буквами, пустой строки и строки с пробелами, поэтому перед разбором делается `trim()`. Метод превращает ошибки формата и диапазона в одно исключение, а `main` решает, что делать: сосчитать и пропустить. `Locale.ROOT` гарантирует точку в дроби, а `(double)` включает дробное деление.

<!-- section: precise -->
## Что нужно уметь

* читать строки ввода до конца циклом `while (in.hasNextLine())` и пропускать пустые;
* делить строку по разделителю `split(";", -1)` и проверять число частей;
* разбирать число через `Integer.parseInt` внутри `try/catch (NumberFormatException e)` и убирать пробелы через `trim()`;
* проверять допустимый диапазон значений после разбора;
* считать принятые и пропущенные строки и не падать на одной неверной;
* печатать дробные числа через `printf` или `String.format` с `Locale.ROOT`;
* помнить, что `nextLine()` после `nextInt()` возвращает остаток строки.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* Исключение не перехвачено: одна неверная строка роняет программу с `NumberFormatException`.
* `parseInt(" 5")` с пробелом бросает исключение; нужен `trim()`.
* `split(";")` отбрасывает пустой хвост: строка `ann;5;` даёт две части, как `ann;5`. Для точной проверки используйте `split(";", -1)`.
* Деление `sum / ok` двух `int` целочисленное; для среднего нужна дробь. Деление на ноль при `ok == 0` нужно обойти.
* `printf("%.2f", x)` без `Locale.ROOT` печатает запятую на русской локали.
* `nextLine()` после `nextInt()` читает пустой остаток строки.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Чтение ввода: Scanner](lesson:java.from-zero.reading-input), [Преобразование типов](lesson:java.from-zero.type-conversion), [Исключения: try, catch и throw](lesson:java.from-zero.exceptions). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
