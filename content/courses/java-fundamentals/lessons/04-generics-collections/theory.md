<!-- section: simple -->
## Обобщения и коллекции

**Обобщения** (generics, Java 5) - это параметры типов. `List<String>` - список, в который компилятор разрешит класть только строки и из которого вернёт `String` без приведения. Без обобщений пришлось бы хранить `Object` и приводить типы вручную, а ошибки проявлялись бы только во время выполнения (`ClassCastException`).

Главная особенность Java: обобщения существуют **только на этапе компиляции**. После проверки типов компилятор их стирает (*type erasure*): в байткоде `List<String>` и `List<Integer>` - один и тот же класс `List`. Это сохранило совместимость со старым кодом, но породило ограничения: нельзя написать `new T()`, `new T[10]`, `x instanceof List<String>`.

**Фреймворк коллекций** (`java.util`) - стандартный набор структур данных, построенных на интерфейсах:

* `List` - упорядоченная последовательность с индексами (`ArrayList`);
* `Set` - множество без дубликатов (`HashSet`, `LinkedHashSet`, `TreeSet`);
* `Map` - отображение «ключ - значение» (`HashMap`, `LinkedHashMap`, `TreeMap`);
* `Deque`/`Queue` - очередь и стек (`ArrayDeque`, `PriorityQueue`).

Вы программируете *по интерфейсу* (`List<String> xs = new ArrayList<>()`), а реализацию выбираете по требованиям к порядку и скорости.

<!-- section: precise -->
## Точные правила

**Обобщённые типы и методы.** `class Box<T>`, `record Pair<A, B>(A a, B b)`, метод `static <T> T first(List<T> xs)`. Параметр типа может иметь **ограничение**: `<T extends Comparable<T>>` разрешает вызывать `compareTo`. Оператор «ромб» `new ArrayList<>()` выводит аргумент типа из контекста. Параметрами типа могут быть только ссылочные типы: `List<int>` нельзя, нужен `List<Integer>` (с упаковкой, см. [прошлый курс](lesson:java.first-steps.primitives-objects-and-equality)).

**Стирание.** Для параметра `T` без ограничения компилятор подставляет `Object`, для `T extends Comparable<T>` - `Comparable`, и вставляет приведения в местах использования. Следствия:

* во время выполнения `new ArrayList<String>().getClass() == new ArrayList<Integer>().getClass()`;
* нельзя создать массив обобщённого типа (`new T[n]`, `new List<String>[n]`), использовать `instanceof` с аргументом типа, перегрузить методы, отличающиеся только аргументами типа;
* возможно **загрязнение кучи** (*heap pollution*): через «сырой» тип или непроверяемое приведение в `List<String>` попадает число, а `ClassCastException` возникнет позже, в месте чтения, где компилятор поставил приведение к `String`.

**Инвариантность и подстановочные символы.** `List<Integer>` **не** является подтипом `List<Number>` (иначе в него можно было бы положить `Double`). Массивы же ковариантны: `Number[] a = new Integer[1]` компилируется, а запись `a[0] = 1.5` бросает `ArrayStoreException`. Чтобы описать «список чего-то подходящего», используют подстановочные символы:

* `List<? extends Number>` - можно **читать** элементы как `Number`, но нельзя добавлять (кроме `null`): неизвестно, какой именно подтип там хранится;
* `List<? super Integer>` - можно **добавлять** `Integer`, а читать - только как `Object`;
* `List<?>` - список чего-либо; читать как `Object`.

Мнемоника **PECS**: *Producer Extends, Consumer Super*. Параметр, из которого вы только читаете (производитель), объявляйте `? extends T`; параметр, в который только пишете (потребитель), - `? super T`. Классический пример: `static <T> void copy(List<? super T> dst, List<? extends T> src)`. Сигнатура `Collections.sort` использует то же: `<T extends Comparable<? super T>> void sort(List<T>)`.

**Реализации коллекций и их свойства.**

| Структура | Порядок обхода | Типичная сложность |
| --- | --- | --- |
| `ArrayList` | порядок вставки | доступ по индексу O(1), вставка в середину O(n) |
| `ArrayDeque` | с обоих концов | добавление и удаление на концах O(1) |
| `HashMap`, `HashSet` | **не определён** | вставка, поиск, удаление O(1) в среднем |
| `LinkedHashMap`, `LinkedHashSet` | порядок вставки | как у хеш-структур |
| `TreeMap`, `TreeSet` | по ключу (естественный порядок или `Comparator`) | O(log n), есть `floorKey`, `headMap` |
| `PriorityQueue` | `poll()` возвращает минимальный; обход не упорядочен | вставка и извлечение O(log n) |

Хеш-структуры требуют согласованных `equals` и `hashCode` ([урок про записи](lesson:java.fundamentals.classes-records-enums)); упорядоченные - `Comparable` либо `Comparator`.

**Полезные операции.** `Map.merge(k, 1, Integer::sum)` - счётчик; `computeIfAbsent(k, x -> new ArrayList<>())` - многозначное отображение; `getOrDefault`; `List.sort(Comparator.comparing(...).thenComparing(...))`; `removeIf(predicate)`. Коллекции, созданные `List.of`, `Set.of`, `Map.of`, **неизменяемы** и не допускают `null`. `Arrays.asList(...)` даёт список фиксированного размера: `set` работает, `add` бросает `UnsupportedOperationException`. Структурное изменение коллекции во время обхода for-each вызывает `ConcurrentModificationException` (механизм *fail-fast*).

<!-- section: examples -->
## Примеры

**Пример 1. Стирание типов и загрязнение кучи.**

```java run
import java.util.ArrayList;
import java.util.List;

public class Main {
    @SuppressWarnings({"unchecked", "rawtypes"})
    public static void main(String[] args) {
        List<String> strings = new ArrayList<>();
        List<Integer> ints = new ArrayList<>();
        System.out.println(strings.getClass() == ints.getClass());
        List raw = strings;
        raw.add(42);
        System.out.println(strings.size());
        try {
            String s = strings.get(0);
            System.out.println(s);
        } catch (ClassCastException e) {
            System.out.println("ClassCastException при чтении");
        }
        Object[] arr = new String[1];
        try {
            arr[0] = 1;
        } catch (ArrayStoreException e) {
            System.out.println("ArrayStoreException: " + e.getMessage());
        }
    }
}
```
```output
true
1
ClassCastException при чтении
ArrayStoreException: java.lang.Integer
```

Добавление числа в `List<String>` через сырой тип прошло без ошибок: во время выполнения проверки нет. Исключение возникло в `strings.get(0)`, где компилятор вставил приведение к `String`.

**Пример 2. Ограничения типов и PECS.**

```java run
import java.util.ArrayList;
import java.util.Collection;
import java.util.List;

public class Main {
    record Pair<A, B>(A first, B second) {
        <C> Pair<A, C> withSecond(C c) {
            return new Pair<>(first, c);
        }
    }

    static <T extends Comparable<? super T>> T maxOf(Collection<? extends T> xs) {
        T best = null;
        for (T x : xs) {
            if (best == null || x.compareTo(best) > 0) {
                best = x;
            }
        }
        return best;
    }

    static double sum(List<? extends Number> xs) {
        double s = 0;
        for (Number n : xs) {
            s += n.doubleValue();
        }
        return s;
    }

    static void fillInts(List<? super Integer> dst, int n) {
        for (int i = 0; i < n; i++) {
            dst.add(i);
        }
    }

    static <T> void copy(List<? super T> dst, List<? extends T> src) {
        for (T t : src) {
            dst.add(t);
        }
    }

    public static void main(String[] args) {
        System.out.println(maxOf(List.of(3, 9, 4)) + " " + maxOf(List.of("b", "zz", "a")));
        System.out.println(sum(List.of(1, 2, 3)) + " " + sum(List.of(1.5, 2.5)));
        List<Number> nums = new ArrayList<>();
        fillInts(nums, 3);
        copy(nums, List.of(2.5, 3.5));
        System.out.println(nums);
        List<Object> objs = new ArrayList<>();
        copy(objs, List.of("x", "y"));
        System.out.println(objs);
        Pair<String, Integer> p = new Pair<>("n", 1);
        System.out.println(p.withSecond(List.of(true)));
    }
}
```
```output
9 zz
6.0 4.0
[0, 1, 2, 2.5, 3.5]
[x, y]
Pair[first=n, second=[true]]
```

**Пример 3. Порядок обхода и счётчики.**

```java run
import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.Deque;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.PriorityQueue;
import java.util.TreeMap;

public class Main {
    public static void main(String[] args) {
        String[] words = {"pear", "apple", "fig", "apple", "pear", "apple"};
        Map<String, Integer> linked = new LinkedHashMap<>();
        Map<String, Integer> sorted = new TreeMap<>();
        for (String w : words) {
            linked.merge(w, 1, Integer::sum);
            sorted.merge(w, 1, Integer::sum);
        }
        System.out.println(linked);
        System.out.println(sorted);

        Map<Integer, List<String>> byLength = new TreeMap<>();
        for (String w : linked.keySet()) {
            byLength.computeIfAbsent(w.length(), k -> new ArrayList<>()).add(w);
        }
        System.out.println(byLength);

        TreeMap<Integer, String> grades = new TreeMap<>(Map.of(0, "F", 50, "C", 70, "B", 90, "A"));
        System.out.println(grades.floorEntry(85).getValue() + " " + grades.headMap(70));

        Deque<Integer> stack = new ArrayDeque<>();
        stack.push(1);
        stack.push(2);
        stack.push(3);
        System.out.println(stack.pop() + " " + stack.peek());

        PriorityQueue<String> pq = new PriorityQueue<>(java.util.Comparator.comparing(String::length).thenComparing(s -> s));
        pq.addAll(List.of("ccc", "a", "bb", "aa", "b"));
        StringBuilder sb = new StringBuilder();
        while (!pq.isEmpty()) {
            sb.append(pq.poll()).append(' ');
        }
        System.out.println(sb.toString().trim());
    }
}
```
```output
{pear=2, apple=3, fig=1}
{apple=3, fig=1, pear=2}
{3=[fig], 4=[pear], 5=[apple]}
B {0=F, 50=C}
3 2
a b aa bb ccc
```

**Пример 4. Ловушки коллекций.**

```java run
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

public class Main {
    public static void main(String[] args) {
        List<Integer> xs = new ArrayList<>(List.of(10, 20, 30, 1));
        xs.remove(1);
        System.out.println(xs);
        xs.remove(Integer.valueOf(1));
        System.out.println(xs);

        List<Integer> nums = new ArrayList<>(List.of(1, 2, 3, 4, 5, 6));
        try {
            for (Integer n : nums) {
                if (n % 2 == 0) {
                    nums.remove(n);
                }
            }
        } catch (java.util.ConcurrentModificationException e) {
            System.out.println("ConcurrentModificationException");
        }
        nums = new ArrayList<>(List.of(1, 2, 3, 4, 5, 6));
        nums.removeIf(n -> n % 2 == 0);
        System.out.println(nums);

        List<String> fixed = Arrays.asList("a", "b");
        fixed.set(0, "z");
        System.out.println(fixed);
        try {
            fixed.add("c");
        } catch (UnsupportedOperationException e) {
            System.out.println("Arrays.asList: add запрещён");
        }
        try {
            List.of("a", null);
        } catch (NullPointerException e) {
            System.out.println("List.of не принимает null");
        }
    }
}
```
```output
[10, 30, 1]
[10, 30]
ConcurrentModificationException
[1, 3, 5]
[z, b]
Arrays.asList: add запрещён
List.of не принимает null
```

`xs.remove(1)` удалил элемент по **индексу** 1 (число 20), а не значение `1`; значение удаляют через `Integer.valueOf(1)`.

**Пример 5. Стабильная сортировка составным компаратором.**

```java run
import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;

public class Main {
    record Person(String name, int age) {}

    public static void main(String[] args) {
        List<Person> people = new ArrayList<>(List.of(
                new Person("Ann", 30), new Person("Bob", 25), new Person("Cid", 30), new Person("Dan", 25), new Person("Eve", 41)));
        people.sort(Comparator.comparingInt(Person::age).reversed().thenComparing(Person::name, Comparator.reverseOrder()));
        for (Person p : people) {
            System.out.println(p.age() + " " + p.name());
        }
    }
}
```
```output
41 Eve
30 Cid
30 Ann
25 Dan
25 Bob
```

<!-- section: mistakes -->
## Типичные ошибки

* **Полагаться на порядок обхода `HashMap` и `HashSet`.** Он не определён спецификацией и может измениться между версиями. Нужен порядок - берите `LinkedHashMap` или `TreeMap`.
* **Использовать сырые типы (`List` без аргумента).** Компилятор перестаёт проверять добавление, а ошибка всплывёт позже в виде `ClassCastException`.
* **Ждать, что `List<Integer>` можно передать в `List<Number>`.** Для чтения используйте `List<? extends Number>`.
* **Изменять коллекцию в цикле for-each.** Вызовет `ConcurrentModificationException`; применяйте `removeIf`, итератор или соберите изменения отдельно.
* **Путать `remove(int)` и `remove(Object)` в `List<Integer>`.** `xs.remove(1)` удаляет по индексу.
* **Менять ключ после вставки в `HashMap` или `TreeMap`.** Запись потеряется. Ключи делайте неизменяемыми.
* **Рассчитывать, что `List.of` и `Arrays.asList` ведут себя одинаково.** Первый неизменяем целиком и запрещает `null`, второй позволяет `set`, но не `add`.
* **Писать компаратор как `a - b`.** Разность может переполниться и дать неверный знак; используйте `Integer.compare(a, b)`.

<!-- section: summary -->
## Итоги

* Обобщения проверяются компилятором и стираются: во время выполнения `List<String>` и `List<Integer>` - один класс, а ошибки типов из-за сырых типов всплывают как `ClassCastException`.
* Обобщённые типы инвариантны; для гибкости используйте `? extends T` (читаем) и `? super T` (пишем) - правило PECS.
* Выбирайте реализацию по порядку и сложности: `ArrayList`, `ArrayDeque`, `HashMap`, `LinkedHashMap` (порядок вставки), `TreeMap` (порядок ключей).
* Для счётчиков и группировок есть `merge`, `computeIfAbsent`; для изменения при обходе - `removeIf`.

Дальше: [Streams API, лямбды и Optional](lesson:java.fundamentals.streams-lambdas-optional).
Назад: [интерфейсы, sealed-типы и образцы](lesson:java.fundamentals.interfaces-sealed-patterns).
Сравните: [обобщения и коллекции в C#](lesson:csharp.fundamentals.generics-and-collections), [обобщения в TypeScript](lesson:typescript.fundamentals.generics), [трейты и обобщения в Rust](lesson:rust.fundamentals.traits-and-generics), [словари и множества в Python](lesson:python.fundamentals.dicts-and-sets).
