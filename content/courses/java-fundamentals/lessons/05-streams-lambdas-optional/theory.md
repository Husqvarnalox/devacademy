<!-- section: simple -->
## Лямбды, потоки и Optional

**Лямбда-выражение** (Java 8) - короткая запись анонимной функции: `(a, b) -> a + b`. Лямбда не новый тип, а реализация **функционального интерфейса** - интерфейса с одним абстрактным методом: `Predicate<T>` (`T -> boolean`), `Function<T, R>`, `Supplier<T>`, `Consumer<T>`, `Comparator<T>`. **Ссылка на метод** (`String::length`, `System.out::println`) - ещё более короткая запись, когда лямбда лишь вызывает готовый метод.

**Stream API** описывает обработку данных как конвейер: *источник* (коллекция, массив, диапазон), цепочка *промежуточных операций* (`filter`, `map`, `sorted`, `limit`) и одна *терминальная* (`collect`, `forEach`, `count`, `findFirst`, `reduce`). Вы пишете, **что** получить, а не как выполнить цикл. Поток не хранит данные и не изменяет источник.

**Optional** - контейнер для значения, которого может не быть. Он нужен как *тип результата* метода: вместо `null`, который легко забыть проверить, вызывающий код видит `Optional<User>` и вынужден решить, что делать при отсутствии.

Вместе это даёт декларативный стиль: «возьми заказы, оставь оплаченные, сгруппируй по клиенту, посчитай сумму».

<!-- section: precise -->
## Точные правила

**Лямбды.**

* Лямбда подходит там, где ожидается функциональный интерфейс; тип выводится из контекста. Аннотация `@FunctionalInterface` проверяет, что абстрактный метод ровно один.
* Лямбда захватывает локальные переменные только **эффективно финальные** (которым значение присваивается один раз). Изменить захваченную локальную переменную нельзя; поля объекта изменять можно.
* `this` внутри лямбды - это `this` окружающего класса (в отличие от анонимного класса).
* Четыре вида ссылок на методы: статический метод (`Integer::parseInt`), метод конкретного объекта (`System.out::println`), метод произвольного объекта типа (`String::length`), конструктор (`ArrayList::new`).
* Составление: `Function.andThen`, `compose`; `Predicate.and`, `or`, `negate`; `Comparator.comparing(...).thenComparing(...).reversed()`.

**Потоки.**

* Конвейер **ленив**: промежуточные операции ничего не выполняют, пока не вызвана терминальная. Элементы идут по конвейеру по одному («вертикально»), а не слой за слоем; короткозамыкающие операции (`findFirst`, `limit`, `anyMatch`) прекращают работу досрочно.
* Поток можно использовать **один раз**: повторный вызов даёт `IllegalStateException`.
* Лямбды в потоках должны быть *без побочных эффектов* и не изменять источник во время обхода (требование невмешательства); `peek` предназначен для отладки.
* Примитивные потоки `IntStream`, `LongStream`, `DoubleStream` избегают упаковки: `IntStream.rangeClosed(1, 5)`, `mapToInt(...).sum()`, `boxed()`.
* Терминальные операции: `collect(Collectors.toList())`, а с Java 16 - `Stream.toList()` (возвращает **неизменяемый** список). Коллекторы: `groupingBy(f)` (по умолчанию `HashMap`; для порядка `groupingBy(f, TreeMap::new, downstream)`), `partitioningBy`, `counting`, `summingInt`, `averagingInt`, `mapping`, `joining`, `toMap` (дубликаты ключей без функции слияния дают `IllegalStateException`), `teeing` (Java 12).
* `parallelStream()` распараллеливает работу в общем пуле; результат терминальных операций с ассоциативными операциями не меняется, но порядок побочных эффектов не определён. Применяйте только при измеренной пользе.

**Optional.** `Optional.of(x)` (x не null, иначе `NullPointerException`), `ofNullable`, `empty`. Извлечение: `orElse(default)` (аргумент вычисляется **всегда**), `orElseGet(supplier)` (лениво), `orElseThrow()`, `ifPresent`, `ifPresentOrElse`. Преобразования: `map`, `flatMap`, `filter`. Не используйте `Optional` для полей, параметров и коллекций: он создан для возвращаемых значений. Для примитивов есть `OptionalInt`, `OptionalDouble`.

**Версии.** Лямбды, `Stream`, `Optional` - Java 8; `Stream.toList()` - Java 16 (проверено: `--release 15` отвергает вызов); `Collectors.teeing` - 12; `Optional.ifPresentOrElse`, `or` - 9; `isEmpty` - 11.

<!-- section: examples -->
## Примеры

**Пример 1. Функциональные интерфейсы и ссылки на методы.**

```java run
import java.util.ArrayList;
import java.util.List;
import java.util.function.BiFunction;
import java.util.function.Function;
import java.util.function.Predicate;
import java.util.function.Supplier;

public class Main {
    @FunctionalInterface
    interface Op {
        int apply(int a, int b);
    }

    static int run(Op op, int a, int b) {
        return op.apply(a, b);
    }

    public static void main(String[] args) {
        Function<String, Integer> len = String::length;
        Function<Integer, Integer> twice = x -> x * 2;
        System.out.println(len.andThen(twice).apply("привет"));
        Predicate<String> empty = String::isEmpty;
        System.out.println(empty.negate().test("") + " " + empty.or(s -> s.startsWith("a")).test("abc"));
        BiFunction<Integer, Integer, Integer> add = Integer::sum;
        System.out.println(add.apply(2, 3) + " " + run(Math::max, 4, 9) + " " + run((a, b) -> a * b, 4, 9));
        Supplier<List<String>> make = ArrayList::new;
        List<String> list = make.get();
        list.add("x");
        System.out.println(list);

        int base = 10;
        Function<Integer, Integer> plus = x -> x + base;
        System.out.println(plus.apply(5));
        int[] counter = {0};
        Runnable inc = () -> counter[0]++;
        inc.run();
        inc.run();
        System.out.println(counter[0]);
    }
}
```
```output
12
false true
5 9 36
[x]
15
2
```

Переменную `base` менять после захвата нельзя - `base++` сделает её не эффективно финальной, и лямбда не скомпилируется. Обход с массивом из одного элемента работает, но это признак того, что состояние лучше хранить иначе (например, в `AtomicInteger`).

**Пример 2. Ленивость и порядок обработки элементов.**

```java run
import java.util.List;
import java.util.Optional;
import java.util.stream.Stream;

public class Main {
    public static void main(String[] args) {
        List<String> trace = new java.util.ArrayList<>();
        Stream<Integer> pipeline = Stream.of(1, 2, 3, 4, 5, 6)
                .peek(x -> trace.add("src" + x))
                .filter(x -> x % 2 == 0)
                .peek(x -> trace.add("even" + x))
                .map(x -> x * 10);
        System.out.println("до терминальной операции: " + trace);
        Optional<Integer> first = pipeline.findFirst();
        System.out.println(first.get() + " " + trace);
        try {
            pipeline.count();
        } catch (IllegalStateException e) {
            System.out.println("поток уже использован");
        }
        System.out.println(Stream.iterate(1, x -> x * 2).limit(8).toList());
    }
}
```
```output
до терминальной операции: []
20 [src1, src2, even2]
поток уже использован
[1, 2, 4, 8, 16, 32, 64, 128]
```

Элементы 3 и далее не обрабатывались: `findFirst` остановил конвейер на первом подходящем.

**Пример 3. Коллекторы: группировка, разбиение, соединение.**

```java run
import java.util.List;
import java.util.Map;
import java.util.TreeMap;
import java.util.stream.Collectors;

public class Main {
    record Order(String customer, String status, int amount) {}

    public static void main(String[] args) {
        List<Order> orders = List.of(
                new Order("ann", "paid", 100), new Order("bob", "new", 50),
                new Order("ann", "paid", 40), new Order("cid", "paid", 70), new Order("bob", "paid", 30));

        Map<String, Integer> sums = orders.stream()
                .filter(o -> o.status().equals("paid"))
                .collect(Collectors.groupingBy(Order::customer, TreeMap::new, Collectors.summingInt(Order::amount)));
        System.out.println(sums);

        Map<Boolean, Long> parts = orders.stream()
                .collect(Collectors.partitioningBy(o -> o.amount() >= 50, Collectors.counting()));
        System.out.println(parts);

        System.out.println(orders.stream().map(Order::customer).distinct().sorted().collect(Collectors.joining(", ", "[", "]")));

        var stats = orders.stream().mapToInt(Order::amount).summaryStatistics();
        System.out.println(stats.getMin() + " " + stats.getMax() + " " + stats.getSum() + " " + stats.getAverage());

        Map<String, Integer> merged = orders.stream()
                .collect(Collectors.toMap(Order::customer, Order::amount, Integer::sum, TreeMap::new));
        System.out.println(merged);
        try {
            orders.stream().collect(Collectors.toMap(Order::customer, Order::amount));
        } catch (IllegalStateException e) {
            System.out.println("IllegalStateException: дубликат ключа");
        }
        var minMax = orders.stream().collect(Collectors.teeing(
                Collectors.minBy(java.util.Comparator.comparingInt(Order::amount)),
                Collectors.maxBy(java.util.Comparator.comparingInt(Order::amount)),
                (lo, hi) -> lo.get().amount() + ".." + hi.get().amount()));
        System.out.println(minMax);
    }
}
```
```output
{ann=140, bob=30, cid=70}
{false=2, true=3}
[ann, bob, cid]
30 100 290 58.0
{ann=140, bob=80, cid=70}
IllegalStateException: дубликат ключа
30..100
```

**Пример 4. Optional: цепочки и ленивые значения.**

```java run
import java.util.Map;
import java.util.Optional;

public class Main {
    static final Map<String, String> EMAILS = Map.of("ann", "ann@example.org", "bob", "");

    static Optional<String> email(String user) {
        return Optional.ofNullable(EMAILS.get(user)).filter(s -> !s.isBlank());
    }

    static String fallback() {
        System.out.println("fallback вычислен");
        return "none@example.org";
    }

    public static void main(String[] args) {
        System.out.println(email("ann").map(String::toUpperCase).orElse("-"));
        System.out.println(email("bob").isPresent() + " " + email("zed").isEmpty());
        System.out.println(email("ann").orElse(fallback()));
        System.out.println(email("ann").orElseGet(Main::fallback));
        email("bob").ifPresentOrElse(e -> System.out.println("есть " + e), () -> System.out.println("пусто"));
        System.out.println(email("zed").or(() -> email("ann")).get());
        try {
            email("zed").orElseThrow();
        } catch (java.util.NoSuchElementException e) {
            System.out.println("NoSuchElementException");
        }
        try {
            Optional.of(null);
        } catch (NullPointerException e) {
            System.out.println("Optional.of(null): NullPointerException");
        }
    }
}
```
```output
ANN@EXAMPLE.ORG
false true
fallback вычислен
ann@example.org
ann@example.org
пусто
ann@example.org
NoSuchElementException
Optional.of(null): NullPointerException
```

`orElse(fallback())` напечатал «fallback вычислен», хотя значение было, а `orElseGet` - нет.

**Пример 5. Примитивные потоки, `flatMap`, `toList` и изменяемость.**

```java run
import java.util.List;
import java.util.stream.Collectors;
import java.util.stream.IntStream;
import java.util.stream.Stream;

public class Main {
    public static void main(String[] args) {
        System.out.println(IntStream.rangeClosed(1, 4).map(x -> x * x).sum());
        System.out.println(IntStream.range(0, 5).boxed().collect(Collectors.toList()));
        List<List<Integer>> nested = List.of(List.of(1, 2), List.of(3), List.of());
        System.out.println(nested.stream().flatMap(List::stream).map(x -> x + 1).toList());
        System.out.println(Stream.of("a", "bb", "ccc").mapToInt(String::length).average().getAsDouble());
        System.out.println(Stream.of(5, 3, 8).reduce(Integer::sum).get() + " " + Stream.of(5, 3, 8).reduce(0, Integer::max));

        List<Integer> immutable = Stream.of(1, 2).toList();
        List<Integer> mutable = Stream.of(1, 2).collect(Collectors.toList());
        mutable.add(3);
        try {
            immutable.add(3);
        } catch (UnsupportedOperationException e) {
            System.out.println("toList(): неизменяемый " + mutable);
        }
    }
}
```
```output
30
[0, 1, 2, 3, 4]
[2, 3, 4]
2.0
16 8
toList(): неизменяемый [1, 2, 3]
```

<!-- section: mistakes -->
## Типичные ошибки

* **Забыть терминальную операцию.** `list.stream().filter(...).map(...)` ничего не делает: вычисления ленивы.
* **Использовать поток дважды.** Второй вызов даёт `IllegalStateException`; создайте новый поток из источника.
* **Менять внешнее состояние из лямбды потока** (добавлять в список, увеличивать счётчик). Это ломается при `parallelStream` и нарушает смысл конвейера; используйте `collect`, `reduce`, `count`.
* **`Optional.get()` без проверки.** Вместо него применяйте `orElse`, `orElseThrow`, `map`, `ifPresentOrElse`.
* **`orElse(дорогой вызов())`.** Аргумент вычисляется всегда; для тяжёлых или побочных значений используйте `orElseGet`.
* **Optional в полях, параметрах и коллекциях.** Это увеличивает сложность без выгоды; `Optional` - тип возвращаемого значения.
* **`toMap` без функции слияния.** Дубликаты ключей приводят к `IllegalStateException`.
* **Рассчитывать на изменяемость `Stream.toList()`.** Результат неизменяем; нужен изменяемый список - `collect(Collectors.toCollection(ArrayList::new))`.
* **Применять `parallelStream` «для скорости» по умолчанию.** Без измерений он часто медленнее и усложняет отладку.

<!-- section: summary -->
## Итоги

* Лямбды и ссылки на методы реализуют функциональные интерфейсы; захватываются только эффективно финальные локальные переменные.
* Конвейер потока ленив, одноразов и выполняется элемент за элементом; результат даёт терминальная операция.
* `groupingBy`, `partitioningBy`, `toMap`, `joining`, `summingInt` покрывают большинство агрегаций; для порядка ключей передавайте `TreeMap::new`.
* `Optional` - возвращаемый тип для «может не быть значения»; `orElseGet` ленив, `orElse` нет.

Дальше: [исключения и try-with-resources](lesson:java.fundamentals.exceptions-try-with-resources).
Назад: [обобщения и коллекции](lesson:java.fundamentals.generics-collections).
Сравните: [LINQ в C#](lesson:csharp.fundamentals.linq), [делегаты и лямбды в C#](lesson:csharp.fundamentals.delegates-lambdas-events), [итераторы и коллекции в Rust](lesson:rust.fundamentals.collections-and-iterators), [функции как объекты в Python](lesson:python.advanced.functions-as-objects).
