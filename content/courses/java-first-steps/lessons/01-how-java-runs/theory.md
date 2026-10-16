<!-- section: simple -->
## Java: сначала байткод, потом виртуальная машина

Программа на Java выполняется в два шага, и каждый делает отдельная программа:

1. **`javac`** (компилятор) читает `Main.java` и создаёт файл `Main.class` с **байткодом** - инструкциями для воображаемого процессора. Это и есть «компиляция».
2. **`java`** запускает **JVM** (Java Virtual Machine, виртуальная машина Java). Она загружает `Main.class`, проверяет его и выполняет байткод. Горячие места JVM по ходу работы компилирует в машинный код процессора (JIT).

Байткод не привязан к операционной системе и процессору, поэтому один и тот же `.class` запускается там, где есть подходящая JVM. Это и есть идея «написано один раз - запускается везде».
Именно так и работает проверка в этом приложении: `javac -d out Main.java`, затем `java -cp out Main`.

Точка входа - метод `public static void main(String[] args)` в классе, имя которого совпадает с именем файла: файл `Main.java` содержит `public class Main`.

Вывод - `System.out.println` в стандартный вывод. Ввод - `System.in`, удобная обёртка - `Scanner`:

```java
import java.util.Scanner;

public class Main {
    public static void main(String[] args) {
        Scanner sc = new Scanner(System.in);
        int n = sc.nextInt();
        System.out.println(n * 2);
    }
}
```

<!-- section: precise -->
## Что происходит внутри: компиляция, загрузка, интерпретация, JIT

**Файл `.class`.** Формат описан спецификацией JVM. Файл начинается с «магического числа» `0xCAFEBABE`, затем идут версия формата, **пул констант** (строки, имена, ссылки на методы) и описание классов, полей и методов.
Байткод - стековый: инструкции вроде `iload`, `iadd`, `invokevirtual` берут операнды со стека и кладут результат обратно.

**Загрузка и проверка.** Загрузчики классов находят `.class` по пути классов (`-cp out`), JVM проверяет байткод (верификатор), чтобы он не нарушал типобезопасность, затем выполняет инициализацию класса.
Поэтому битый `.class` отвергается ещё до выполнения, а ошибки вроде обращения к несуществующему методу проявляются как `NoSuchMethodError` при запуске, а не как «ошибка компиляции».

**Интерпретатор и JIT.** HotSpot сначала интерпретирует байткод, одновременно собирая статистику. Часто выполняемые методы и циклы компилируются в машинный код:
быстро и с лёгкой оптимизацией компилятором **C1**, а самые горячие - с агрессивными оптимизациями компилятором **C2**. Эта схема называется **ступенчатой компиляцией** (tiered compilation).
Результат программы от неё не зависит, но время «прогрева» есть: первые выполнения медленнее.

**Память.** Объекты живут в управляемой куче, освобождает их **сборщик мусора**; вручную память не освобождают. Локальные переменные примитивных типов и ссылки лежат в кадрах вызовов (на стеке потока), сами объекты - в куче.

**Примитивные типы.** `int` - 32 бита, `long` - 64 бита, `double` - 64 бита. Переполнение целых **не вызывает ошибки**: `Integer.MAX_VALUE + 1` даёт `Integer.MIN_VALUE`.
Для проверки есть `Math.addExact` и аналоги: они бросают `ArithmeticException`. Деление целых усекает к нулю: `7 / 2` равно `3`; `7 / 2.0` равно `3.5`. Литерал `long` пишут с суффиксом `L`: `2_000_000_000L`.

**Ввод через `Scanner`.** `nextInt()`, `nextLong()` пропускают пробельные символы (включая переводы строк) и читают следующее «слово»; поэтому формат ввода с числами на разных строках им разбирается без особых усилий.
`hasNext()` проверяет, есть ли ещё данные. `Scanner` удобен, но медленнее `BufferedReader` на больших данных. Для строки целиком - `nextLine()`.

**Форматирование и локаль.** `String.format("%.2f", x)` и `printf` используют **локаль по умолчанию**: в некоторых локалях десятичный разделитель - запятая. Для вывода, который читает программа, указывают локаль явно: `String.format(Locale.ROOT, "%.2f", x)`.
В форматной строке `%n` - перевод строки платформы, `%d` - целое, `%s` - строка.

**Исходный запуск.** Начиная с Java 11 команда `java Main.java` компилирует файл в памяти и сразу запускает (JEP 330). Это тот же `javac` и та же JVM, просто без записи `.class` на диск.

<!-- section: examples -->
## Примеры

**Пример 1. Класс-файл начинается с CAFEBABE.**

```java run
import java.io.InputStream;

public class Main {
    public static void main(String[] args) throws Exception {
        try (InputStream in = Main.class.getResourceAsStream("Main.class")) {
            byte[] head = in.readNBytes(4);
            StringBuilder sb = new StringBuilder();
            for (byte b : head) {
                sb.append(String.format("%02X", b));
            }
            System.out.println(sb);
        }
        System.out.println(System.getProperty("java.vm.specification.name"));
    }
}
```
```output
CAFEBABE
Java Virtual Machine Specification
```

Программа читает собственный скомпилированный файл и видит магическое число. Свойство `java.vm.specification.name` напоминает, что JVM - это спецификация, у которой есть разные реализации.

**Пример 2. Переполнение и `Math.addExact`.**

```java run
public class Main {
    public static void main(String[] args) {
        int x = Integer.MAX_VALUE;
        x++;
        System.out.println(x);
        try {
            Math.addExact(Integer.MAX_VALUE, 1);
        } catch (ArithmeticException e) {
            System.out.println(e.getMessage());
        }
        long big = 2_000_000_000L + 2_000_000_000L;
        System.out.println(big);
    }
}
```
```output
-2147483648
integer overflow
4000000000
```

**Пример 3. Деление и формат.**

```java run
import java.util.Locale;

public class Main {
    public static void main(String[] args) {
        System.out.println(7 / 2);
        System.out.println(7 / 2.0);
        System.out.println(String.format(Locale.ROOT, "%.2f", 5.0 / 3));
        System.out.printf("%d-%s%n", 42, "ok");
    }
}
```
```output
3
3.5
1.67
42-ok
```

**Пример 4. stdout и stderr.**

```java run
public class Main {
    public static void main(String[] args) {
        System.out.println("результат");
        System.err.println("диагностика");
    }
}
```
```output
результат
```

**Пример 5. Типовая форма ввода.**

```java
import java.util.Scanner;

public class Main {
    public static void main(String[] args) {
        Scanner sc = new Scanner(System.in);
        int n = sc.nextInt();
        long sum = 0;
        for (int i = 0; i < n; i++) {
            sum += sc.nextLong();
        }
        System.out.println(sum);
    }
}
```

<!-- section: mistakes -->
## Типичные ошибки

* **Имя файла и класса не совпадают.** Публичный класс `Main` обязан лежать в `Main.java`. Иначе компилятор сообщит об ошибке.
* **Сумма в `int`.** Сложение двух чисел до 2 000 000 000 переполняется молча. Используйте `long` и суффикс `L` в литералах.
* **`7 / 2` вместо дробного результата.** Если оба операнда целые, деление целочисленное. Приведите один операнд к `double`: `(double) sum / n`.
* **`printf("%.2f")` без локали.** В русской или немецкой локали получится `1,67`. Передайте `Locale.ROOT`.
* **`nextInt()` и затем `nextLine()`.** После `nextInt()` в буфере остаётся перевод строки, и `nextLine()` вернёт пустую строку. Читайте всё токенами или вызывайте лишний `nextLine()`.
* **Считать, что компилятор находит все ошибки.** `javac` проверяет типы и синтаксис, но `NullPointerException` и `ArrayIndexOutOfBoundsException` проявляются при выполнении.
* **Забыть `throws` для проверяемых исключений.** Метод, вызывающий `readNBytes`, обязан обработать `IOException` либо объявить его.
* **Ждать, что `System.out.println(char + int)` склеит строку.** Сложение `char` и `int` - число, а не текст.

<!-- section: summary -->
## Итоги

* `javac` создаёт байткод `.class`, JVM его загружает, проверяет и выполняет; горячий код компилируется в машинный (C1, затем C2).
* Байткод переносим между платформами; результат от ступенчатой компиляции не зависит.
* Целые при переполнении «заворачиваются»; для безопасных сумм используют `long` или `Math.addExact`.
* Ввод удобно читать через `Scanner`, а числа с точкой форматировать с `Locale.ROOT`.

Дальше: [примитивы, объекты и равенство](lesson:java.first-steps.primitives-objects-and-equality).
Сравните: [как запускается Python](lesson:python.fundamentals.how-python-runs).
