<!-- section: simple -->
## Java одним взглядом: что запускается и где живут данные

Исходный файл `.java` компилятор `javac` превращает в **байткод** - набор инструкций для воображаемого стекового процессора. Байткод хранится в файлах `.class`; его загружает и выполняет **JVM** (Java Virtual Machine). Вы уже запускали такой путь в [первом курсе](lesson:java.first-steps.how-java-runs); здесь мы заглянем под капот и разберёмся с двумя вопросами, от которых зависит корректность любой Java-программы:

1. **Какие бывают значения.** В Java два мира: **примитивы** (`int`, `long`, `double`, `boolean`, `char`, `byte`, `short`, `float`) - просто биты в переменной, и **ссылочные типы** (классы, интерфейсы, массивы, `String`) - переменная хранит *ссылку* на объект.
2. **Где это лежит.** Локальные переменные и параметры методов живут в **кадрах стека** потока. Все объекты и массивы живут в **куче** (heap). Освобождает объекты не программист, а **сборщик мусора**: он удаляет те объекты, до которых нельзя добраться из «корней» (локальных переменных, статических полей, потоков).

Из этого следует почти всё, что удивляет новичков: присваивание ссылки копирует адрес, а не объект; `==` у ссылок сравнивает адреса; сужение `long` в `int` молча отбрасывает старшие биты; а бесконечная рекурсия падает с `StackOverflowError`, потому что стек потока конечен.

<!-- section: precise -->
## Точная модель

**Компиляция.** `javac` проверяет типы и синтаксис, затем генерирует байткод. Обратите внимание: компилятор Java **не оптимизирует** агрессивно - основная оптимизация происходит позже, в JIT-компиляторе JVM. Байткод стековый: метод `int add(int a, int b) { return a + b; }` превращается в `iload_0`, `iload_1`, `iadd`, `ireturn`. Разобрать `.class` можно утилитой `javap -c`. Формат файла (магическое число `0xCAFEBABE`, версия, пул констант) задаёт спецификация JVM.

**Версия байткода.** Параметр `javac --release N` компилирует под Java N: он ограничивает язык и API версией N. Если использовать возможность новее N, компилятор откажет. Именно так в этом курсе проверены версии, в которых появились `record` (16), `sealed` (17), pattern matching в `switch` (21) и виртуальные потоки (21).

**Примитивные типы** и их размеры определены спецификацией языка и не зависят от платформы: `byte` 8 бит, `short` 16, `char` 16 (беззнаковый, код UTF-16), `int` 32, `long` 64, `float` 32, `double` 64, `boolean` - логическое значение. Значения по умолчанию (поля класса и элементы массива): `0`, `0.0`, `'\u0000'`, `false`, а для ссылок - `null`. Локальная переменная без присваивания не компилируется.

**Приведения.** *Расширение* (`int` в `long`, `int` в `double`) безопасно по диапазону. *Сужение* требует явного приведения `(byte) x` и **отбрасывает старшие биты**: `(byte) 200` равно `-56`. Дробное число приводится к целому с усечением к нулю и **насыщением**: `(int) 1e20` равно `Integer.MAX_VALUE`, а `(int) Double.NaN` равно `0`. Арифметика над `byte`, `short`, `char` выполняется в `int`; составное присваивание `c += 2` включает неявное приведение. Целочисленное деление усекает к нулю, а остаток имеет знак делимого: `-5 % 2` равно `-1`; для неотрицательного остатка есть `Math.floorMod`.

**Ссылки и объекты.** Переменная ссылочного типа хранит ссылку на объект в куче или `null`. Параметры методов всегда передаются **по значению**; для объекта по значению передаётся ссылка, поэтому метод может изменить объект, но не может подменить переменную вызывающего. Оператор `==` сравнивает ссылки; для содержимого нужен `equals`. Массив - тоже объект: `int[] b = a;` создаёт второе имя для того же массива.

**Область памяти по спецификации JVM.** У каждого потока свой **стек JVM** из кадров (локальные переменные и операнды). Общая для всех потоков **куча** хранит объекты и массивы. **Область методов** хранит метаданные классов; в HotSpot это Metaspace. Размер стека ограничен: исчерпание даёт `StackOverflowError`, а нехватка кучи - `OutOfMemoryError`. Оба наследуют `Error`, их обычно не перехватывают в рабочем коде.

**Сборка мусора.** Объект можно удалить, когда он недостижим из корней. Современные сборщики **поколенческие**: большинство объектов умирает молодыми, поэтому куча делится на молодое и старое поколения, и молодое собирается часто и дёшево. В HotSpot по умолчанию используется G1 (если машина достаточно мощная), в этом приложении код запускается с `-XX:+UseSerialGC`. Вызов `System.gc()` лишь просит запустить сборку - гарантий нет. Утечка памяти в Java - это объект, который *достижим*, но больше не нужен (например, забытая запись в статической коллекции); GC такие объекты не трогает.

**Оптимизации JIT.** Анализ побега (escape analysis) может вообще не создавать объект в куче, если он не покидает метод. Это внутренняя оптимизация: на семантику программы она не влияет, и опираться на неё нельзя.

<!-- section: examples -->
## Примеры

**Пример 1. Сужение, насыщение и арифметика.** Все числа ниже получены запуском.

```java run
public class Main {
    public static void main(String[] args) {
        System.out.println(Integer.SIZE + " " + Long.SIZE + " " + Byte.SIZE + " " + Character.SIZE);
        System.out.println((int) 3_000_000_000L);
        System.out.println((byte) 200);
        System.out.println((short) 70000);
        System.out.println((int) 3.99 + " " + (int) -3.99);
        System.out.println((int) 1e20);
        System.out.println((int) Double.NaN);
        char c = 'A';
        c += 2;
        System.out.println(c);
        System.out.println('A' + 1);
        System.out.println((char) ('A' + 1));
        System.out.println(0.1 + 0.2);
        System.out.println(-5 / 2 + " " + -5 % 2 + " " + Math.floorMod(-5, 2));
    }
}
```
```output
32 64 8 16
-1294967296
-56
4464
3 -3
2147483647
0
C
66
B
0.30000000000000004
-2 -1 1
```

**Пример 2. Ссылки, псевдонимы и передача по значению.**

```java run
import java.util.Arrays;

public class Main {
    static void fill(int[] arr) {
        arr[0] = 99;
        arr = new int[] {7, 7, 7};
        arr[1] = 55;
    }

    public static void main(String[] args) {
        int[] a = new int[3];
        int[] b = a;
        b[2] = 5;
        System.out.println(Arrays.toString(a));
        System.out.println(a == b);
        fill(a);
        System.out.println(Arrays.toString(a));
        int[] c = a.clone();
        System.out.println(Arrays.toString(c) + " " + (c == a) + " " + Arrays.equals(c, a));
        String s = null;
        System.out.println(s + "!");
        Integer boxed = null;
        try {
            int x = boxed;
        } catch (NullPointerException e) {
            System.out.println("NPE при распаковке");
        }
    }
}
```
```output
[0, 0, 5]
true
[99, 0, 5]
[99, 0, 5] false true
null!
NPE при распаковке
```

Метод `fill` изменил исходный массив через копию ссылки (`arr[0] = 99`), но присваивание `arr = new int[]{...}` изменило только локальную копию ссылки - в `main` массив остался прежним.

**Пример 3. Стек и куча имеют границы.**

```java run
public class Main {
    static int depth = 0;

    static void recurse() {
        depth++;
        recurse();
    }

    public static void main(String[] args) {
        try {
            recurse();
        } catch (StackOverflowError e) {
            System.out.println("стек исчерпан, глубина > 1000: " + (depth > 1000));
        }
        try {
            long[] huge = new long[Integer.MAX_VALUE];
            System.out.println(huge.length);
        } catch (OutOfMemoryError e) {
            System.out.println(e.getClass().getSimpleName());
        }
        try {
            int[] neg = new int[-1];
        } catch (NegativeArraySizeException e) {
            System.out.println("NegativeArraySizeException");
        }
    }
}
```
```output
стек исчерпан, глубина > 1000: true
OutOfMemoryError
NegativeArraySizeException
```

**Пример 4. Байткод метода, показанный самой JVM.** Программа находит собственный каталог с `.class`, запускает встроенный `javap` и печатает инструкции метода `add`.

```java run
import java.nio.file.Path;
import java.util.spi.ToolProvider;
import java.io.*;

public class Main {
    static int add(int a, int b) {
        return a + b;
    }

    public static void main(String[] args) throws Exception {
        Path dir = Path.of(Main.class.getProtectionDomain().getCodeSource().getLocation().toURI());
        StringWriter out = new StringWriter();
        ToolProvider.findFirst("javap").orElseThrow()
                .run(new PrintWriter(out), new PrintWriter(out), "-c", "-p", "-cp", dir.toString(), "Main");
        boolean inAdd = false;
        for (String line : out.toString().split("\n")) {
            String t = line.strip();
            if (t.startsWith("static int add")) { inAdd = true; continue; }
            if (inAdd && t.isEmpty()) break;
            if (inAdd && !t.equals("Code:")) System.out.println(t.substring(t.indexOf(':') + 1).strip());
        }
    }
}
```
```output
iload_0
iload_1
iadd
ireturn
```

Четыре инструкции без единого регистра: операнды кладутся на стек (`iload`), `iadd` снимает два значения и кладёт сумму, `ireturn` возвращает вершину стека. Тот же `Main.class` начинается с `CAFEBABE`, версия формата хранится в следующих байтах.

**Пример 5. Достижимость, а не область видимости, решает судьбу объекта.**

```java run
import java.util.ArrayList;
import java.util.List;

public class Main {
    static final List<byte[]> CACHE = new ArrayList<>();

    static void work() {
        byte[] temp = new byte[1024];
        temp[0] = 1;
    }

    static void leak() {
        CACHE.add(new byte[1024]);
    }

    public static void main(String[] args) {
        for (int i = 0; i < 100; i++) {
            work();
            leak();
        }
        System.out.println("в кэше удерживается массивов: " + CACHE.size());
        System.out.println("версия не ниже 17: " + (Runtime.version().feature() >= 17));
    }
}
```
```output
в кэше удерживается массивов: 100
версия не ниже 17: true
```

`temp` после выхода из `work` недостижим и может быть собран, а 100 массивов в `CACHE` достижимы через статическое поле и будут жить, пока жив класс.

<!-- section: mistakes -->
## Типичные ошибки

* **Считать, что `(byte) x` «обрезает до границы».** Сужение берёт младшие биты, поэтому `(byte) 200` даёт `-56`, а не `127`. Для проверки диапазона используйте `Math.toIntExact` и `Math.addExact`.
* **Складывать `char` и `int` в надежде получить строку.** `'A' + 1` равно `66`; чтобы получить `B`, приведите результат к `char`.
* **Думать, что объекты передаются по ссылке.** В Java всегда передача по значению; присваивание параметру не меняет переменную вызывающего.
* **Ожидать `0.3` от `0.1 + 0.2`.** `double` хранит двоичные дроби; для денег используйте целые числа (копейки) или `BigDecimal`.
* **Использовать `==` для объектов.** Для `String` и обёрток нужен `equals`; `==` может случайно сработать на кэшированных значениях и сломаться на других.
* **Ловить `OutOfMemoryError` и продолжать работу.** После него состояние программы непредсказуемо; ошибку чинят размером кучи или алгоритмом.
* **Вызывать `System.gc()` для управления памятью.** Вызов не гарантирует сборку, а время её выполнения вы не контролируете. Освобождайте ссылки (`null`, удаление из коллекции), а остальное оставьте GC.
* **Считать, что сборщик мусора исключает утечки.** Достижимый, но ненужный объект (запись в статической карте, неотписанный слушатель) остаётся в памяти навсегда.

<!-- section: summary -->
## Итоги

* `javac` создаёт стековый байткод, JVM его проверяет, интерпретирует и компилирует JIT-ом; `--release N` фиксирует версию языка и API.
* Примитивы имеют фиксированный размер; сужение отбрасывает старшие биты, дробное в целое усекается с насыщением.
* Ссылочные типы хранят адрес объекта в куче; параметры передаются по значению (копируется ссылка).
* Стек потока хранит кадры вызовов, куча - объекты; GC удаляет недостижимые объекты, но не лечит удержание ссылок.

Дальше: [классы, записи, перечисления и равенство](lesson:java.fundamentals.classes-records-enums).
Сравните: [система типов и память в C#](lesson:csharp.fundamentals.type-system-and-memory), [типы и нулевые значения в Go](lesson:go.fundamentals.types-values-zero-values).
