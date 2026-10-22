<!-- section: simple -->
## Зачем этот урок

Методы, массивы, списки, словари и строки в Java выглядят знакомо, но у каждой из этих тем есть ловушка, которая ломает программу, написанную «как на другом языке». Метод получает копии аргументов. Массив нельзя увеличить, а у `List<Integer>` есть два разных `remove`. `HashMap.get` возвращает `null`, а распаковка `null` в `int` падает. `String.split` принимает регулярное выражение и молча отбрасывает пустые хвосты. Урок собирает такие места вместе. Успешное прохождение (упражнение и квиз) засчитывает уроки 17-21 курса «Java с нуля».

## Главная мысль

В Java всё передаётся **по значению**, но значением переменной-объекта является ссылка. Метод может изменить объект, на который ссылается параметр, но не может заставить переменную вызывающего кода указать на другой объект. Из этого вытекают остальные правила: массивы и коллекции - объекты, `==` сравнивает ссылки, а содержимое сравнивают методами `equals` и `Arrays.equals`.

<!-- section: examples -->
## Пример 1. Методы и массивы

```java run
import java.util.Arrays;

public class Main {
    static void bump(int x) {
        x++;
    }

    static void fill(int[] a) {
        a[0] = 99;
    }

    static void reassign(int[] a) {
        a = new int[] {7, 7};
    }

    public static void main(String[] args) {
        int n = 1;
        bump(n);
        int[] arr = new int[3];
        fill(arr);
        reassign(arr);
        System.out.println(n + " " + Arrays.toString(arr));
        int[] alias = arr;
        alias[1] = 5;
        int[] copy = Arrays.copyOf(arr, 3);
        System.out.println(arr[1] + " " + (arr == copy) + " " + Arrays.equals(arr, copy));
        String[] names = new String[2];
        System.out.println(names[0] + " " + new boolean[2][1].length);
        try {
            arr[3] = 1;
        } catch (ArrayIndexOutOfBoundsException e) {
            System.out.println(e.getMessage());
        }
    }
}
```
```output
1 [99, 0, 0]
5 false true
null 2
Index 3 out of bounds for length 3
```

Разбор:

* `bump` увеличивает свою копию числа, `n` не меняется. `fill` получает копию ссылки на тот же массив, поэтому запись `a[0] = 99` видна в `main`. `reassign` переставляет только локальную ссылку - `arr` остаётся прежним.
* `alias = arr` не копирует массив, а создаёт вторую ссылку на него: запись через `alias` видна через `arr`. Настоящую копию делает `Arrays.copyOf`. Оператор `==` сравнивает ссылки (`false`), а `Arrays.equals` - содержимое (`true`).
* Массив заполняется значениями по умолчанию: `0`, `false`, `null` для объектов. `System.out.println(arr)` напечатает бесполезное `[I@...`, поэтому используйте `Arrays.toString`.
* Размер массива фиксирован. Выход за границы бросает `ArrayIndexOutOfBoundsException` - в Java нет отрицательных индексов, как в Python.

## Пример 2. Списки: remove и неизменяемость

```java run
import java.util.*;

public class Main {
    public static void main(String[] args) {
        List<Integer> xs = new ArrayList<>(List.of(10, 20, 30, 1));
        xs.remove(1);
        System.out.println(xs);
        xs.remove(Integer.valueOf(1));
        System.out.println(xs);

        List<Integer> fixed = List.of(1, 2);
        try {
            fixed.add(3);
        } catch (UnsupportedOperationException e) {
            System.out.println("List.of неизменяем");
        }
        List<Integer> view = Arrays.asList(1, 2, 3);
        view.set(0, 9);
        System.out.println(view);
        try {
            view.add(4);
        } catch (UnsupportedOperationException e) {
            System.out.println("Arrays.asList имеет фиксированный размер");
        }

        List<Integer> nums = new ArrayList<>(List.of(1, 2, 3, 4));
        try {
            for (int n : nums) {
                if (n == 2) {
                    nums.remove(Integer.valueOf(n));
                }
            }
        } catch (ConcurrentModificationException e) {
            System.out.println("изменение списка во время обхода");
        }
        nums.removeIf(n -> n % 2 == 0);
        System.out.println(nums);
    }
}
```
```output
[10, 30, 1]
[10, 30]
List.of неизменяем
[9, 2, 3]
Arrays.asList имеет фиксированный размер
изменение списка во время обхода
[1, 3]
```

Разбор:

* `xs.remove(1)` удаляет элемент **с индексом** 1 (значение 20), потому что аргумент типа `int` выбирает перегрузку `remove(int)`. Чтобы удалить значение, оберните его: `Integer.valueOf(1)` или `(Integer) 1`.
* В угловых скобках допустимы только классы: `List<Integer>`, а не `List<int>`. Примитив автоматически упаковывается в `Integer` при `add` и распаковывается при чтении.
* `List.of(...)`, `Map.of(...)` и `Set.of(...)` создают **неизменяемые** коллекции. `Arrays.asList` возвращает список фиксированного размера: заменять элементы можно, добавлять и удалять нельзя. Копию, которую можно менять, даёт `new ArrayList<>(...)`.
* Нельзя добавлять и удалять элементы списка внутри цикла `for-each` по нему: итератор замечает изменение и бросает `ConcurrentModificationException`. Для удаления по условию есть `removeIf`, для хитрых случаев - явный `Iterator` или обход с конца по индексу.

## Пример 3. Словарь HashMap

```java run
import java.util.*;

public class Main {
    public static void main(String[] args) {
        Map<String, Integer> stock = new HashMap<>();
        stock.put("яблоко", 3);
        stock.merge("яблоко", 2, Integer::sum);
        stock.merge("груша", 1, Integer::sum);
        System.out.println(stock.get("слива"));
        System.out.println(stock.getOrDefault("слива", 0));
        try {
            int n = stock.get("слива");
            System.out.println(n);
        } catch (NullPointerException e) {
            System.out.println("распаковка null");
        }
        System.out.println(stock.containsKey("груша") + " " + stock.size());
        Map<String, Integer> sorted = new TreeMap<>(stock);
        System.out.println(sorted);
        for (Map.Entry<String, Integer> e : sorted.entrySet()) {
            System.out.println(e.getKey() + "=" + e.getValue());
        }
    }
}
```
```output
null
0
распаковка null
true 2
{груша=1, яблоко=5}
груша=1
яблоко=5
```

Разбор:

* `get` для отсутствующего ключа возвращает `null`, а не бросает исключение. Присвоение `int n = stock.get(...)` распаковывает `Integer`, и для `null` это `NullPointerException`. Используйте `getOrDefault` или `containsKey`.
* `merge(ключ, значение, функция)` - стандартный способ посчитать частоты: если ключа нет, кладёт значение, иначе вызывает функцию над старым и новым. `Integer::sum` - ссылка на метод; лямбды и ссылки подробно разбираются позже.
* Порядок обхода `HashMap` не определён и может зависеть от версии Java. Если порядок важен для вывода, используйте `TreeMap` (сортировка по ключу) или `LinkedHashMap` (порядок вставки).
* Ключи сравниваются через `equals` и `hashCode`, поэтому ключами удобно делать строки и числа. Изменяемый объект в роли ключа - источник тонких ошибок.

## Пример 4. Строки

```java run
import java.util.*;

public class Main {
    public static void main(String[] args) {
        String a = "java";
        String b = new String("java");
        System.out.println((a == b) + " " + a.equals(b) + " " + a.equalsIgnoreCase("JAVA"));
        String s = "  Привет, мир  ";
        s.toUpperCase();
        System.out.println("[" + s.trim().toUpperCase() + "]");
        System.out.println(Arrays.toString("a,b,,c,,".split(",")));
        System.out.println(Arrays.toString("a,b,,c,,".split(",", -1)));
        System.out.println(Arrays.toString("a|b".split("|")));
        System.out.println(Arrays.toString("a|b".split("\\|")));
        System.out.println("".split(",").length + " " + Arrays.toString("x1y22z".split("\\d+")));
        System.out.println("hello".substring(1, 3) + " " + "hello".indexOf('l') + " " + "hello".indexOf("z"));
        System.out.println(String.join("-", List.of("a", "b", "c")) + " " + "ab".repeat(3) + " " + " ".isBlank());
    }
}
```
```output
false true true
[ПРИВЕТ, МИР]
[a, b, , c]
[a, b, , c, , ]
[a, |, b]
[a, b]
1 [x, y, z]
el 2 -1
a-b-c ababab true
```

Разбор:

* `==` для строк сравнивает ссылки. Строка, созданная через `new String` или прочитанная со `Scanner`, - отдельный объект. Результат `==` на литералах может быть `true` из-за общего пула строк, и именно это вводит в заблуждение. Сравнивайте строки через `equals`.
* Строки неизменяемы: `s.toUpperCase()` возвращает новую строку, `s` не меняется. Результат нужно использовать.
* `split` принимает **регулярное выражение**. Строка `"|"` в регулярных выражениях означает «или», поэтому `"a|b".split("|")` режет по каждому символу. Служебные символы (`| . + * ? ( ) [ ] \`) экранируют: `"\\|"`.
* `split` отбрасывает пустые элементы в конце результата; чтобы сохранить их, передают `-1`. Пустая строка даёт массив из одного пустого элемента.
* `substring(от, до)` не включает правую границу; `indexOf` возвращает `-1`, если подстроки нет. `String.join`, `repeat` и `isBlank` экономят циклы.

**Попробуйте.** Замените `remove(1)` на `remove(Integer.valueOf(20))` и предскажите список; поменяйте `"\\|"` на `"\\|+"` и посмотрите, как изменится результат.

<!-- section: precise -->
* Передача параметров - всегда по значению. Для примитива копируется число, для объекта - ссылка. Метод видит и меняет тот же объект, но присваивание параметру не влияет на переменную вызывающего.
* Массив `T[]` - объект фиксированного размера; элементы получают значения по умолчанию (`0`, `false`, `'\0'`, `null`). `arr.length` - поле, у `String` - метод `length()`, у коллекций - `size()`.
* `Arrays.toString`, `Arrays.equals`, `Arrays.copyOf`, `Arrays.sort`, `Arrays.fill` - инструменты для массивов; для вложенных массивов `Arrays.deepToString` и `deepEquals`.
* `ArrayList<E>` хранит только объекты; примитивы упаковываются (`int` в `Integer`). `get(i)` за границами бросает `IndexOutOfBoundsException`. `List.of`, `Set.of`, `Map.of` и `Collections.unmodifiableList` дают неизменяемые коллекции; `Arrays.asList` - список фиксированного размера над массивом.
* `HashMap<K, V>` и `HashSet<E>` допускают один `null`-ключ, не гарантируют порядка; `TreeMap` сортирует ключи, `LinkedHashMap` хранит порядок вставки. Для `HashMap`/`HashSet` ключ должен корректно реализовывать `equals` и `hashCode`.
* Строки: неизменяемые последовательности UTF-16; индексы идут по единицам `char`, символ вне базовой плоскости (например, эмодзи) занимает два. `equals` сравнивает содержимое, `compareTo` - порядок, `equalsIgnoreCase` - без учёта регистра.
* Склейка строк в цикле через `+=` создаёт новую строку на каждой итерации; для больших объёмов используют `StringBuilder` (подробно - в курсе `java-fundamentals`).
* `split(regex)`, `replaceAll(regex, ...)` и `matches(regex)` принимают регулярные выражения; `replace(a, b)` работает с обычным текстом.
* Метод может иметь несколько перегрузок с одним именем; выбор делается по типам аргументов на этапе компиляции, поэтому `remove(1)` и `remove((Integer) 1)` - разные вызовы.

<!-- section: mistakes -->
**1. Примитив в угловых скобках.** `List<int> xs` не компилируется:

```text
Main.java:4: error: unexpected type
        List<int> xs = new ArrayList<>();
             ^
  required: reference
  found:    int
```

Нужен класс-обёртка: `List<Integer>`.

**2. Длина как в другом контейнере.** У массива `length` - поле, у строки `length()` - метод, у списка `size()`:

```text
Main.java:6: error: cannot find symbol
        System.out.println(arr.length());
                              ^
  symbol:   method length()
  location: variable arr of type int[]
```

То же сообщение появится для `s.length` (`symbol: variable length`) и `arr.size()`.

**3. Индексирование строки.** `s[0]` на строке:

```text
Main.java:10: error: array required, but String found
```

Нужен метод `s.charAt(0)`.

**4. Неверный тип элемента.** Добавление строки в `List<Integer>`:

```text
Main.java:8: error: incompatible types: String cannot be converted to Integer
```

**5. Неверное число аргументов.** `m.put("a")` вместо двух аргументов:

```text
Main.java:13: error: method put in interface Map<K,V> cannot be applied to given types;
  required: String,Integer
  found:    String
  reason: actual and formal argument lists differ in length
```

**6. Ошибки, которые компилятор не видит.** `xs.remove(i)` удаляет по индексу, а не по значению; `split("|")` и `split(".")` режут по регулярному выражению; `map.get(k)` с распаковкой в `int`; `a == b` для строк; изменение списка в `for-each`; обход `HashMap` в расчёте на порядок; `s.trim();` без использования результата. Во время выполнения это проявляется как неверный результат или исключение: `ArrayIndexOutOfBoundsException`, `NullPointerException`, `ConcurrentModificationException`, `UnsupportedOperationException`.

<!-- section: summary -->
Теперь вы знаете, как в Java работает передача параметров и где прячутся ловушки коллекций и строк: ссылка по значению, `remove(int)` против `remove(Object)`, неизменяемые списки, `null` из `HashMap.get`, неопределённый порядок `HashMap`, `split` с регулярным выражением и `equals` вместо `==`. Следующий шаг - исключения и первые классы: [Исключения и классы: ловушки Java](lesson:java.programmers.exceptions-and-classes). Для подробностей по темам: [Методы](lesson:java.from-zero.methods), [Массивы](lesson:java.from-zero.arrays), [ArrayList](lesson:java.from-zero.arraylist), [HashMap](lesson:java.from-zero.hashmap), [Методы строк](lesson:java.from-zero.string-methods).

<!-- section: programmer -->
## Главное для тех, кто уже программирует

* Параметры передаются по значению, но для объектов значение - ссылка (как в Python и JavaScript при передаче объектов; в C# это `class` без `ref`). Изменить переменную вызывающего нельзя - только объект.
* Массивы - объекты фиксированной длины, индексы только неотрицательные (нет `a[-1]` как в Python). Списки: `ArrayList` вместо динамического массива Python/JS; `List<Integer>` хранит объекты, а не числа.
* `remove(int)` и `remove(Object)` - разные методы; для `List<Integer>` легко удалить не то.
* `List.of`/`Map.of` неизменяемы; `Arrays.asList` - фиксированный размер. Копирование коллекции - `new ArrayList<>(other)`.
* `HashMap.get` возвращает `null` (не бросает `KeyError` и не возвращает `undefined`); `getOrDefault` и `merge` заменяют `defaultdict`/`Counter`. Порядок `HashMap` не гарантирован; для порядка - `LinkedHashMap`, для сортировки - `TreeMap`.
* Строки неизменяемы и сравниваются через `equals` (`==` сравнивает ссылки, в отличие от Python и C#). Нет индексирования `s[i]` - `charAt`; нет срезов - `substring(от, до)`.
* `split` принимает регулярное выражение (как в JavaScript с `RegExp`, в отличие от `str.split` в Python); хвостовые пустые элементы отбрасываются.
* Нет перегрузки операторов для коллекций и строк (кроме `+` для строк): сравнение - `equals`, порядок - `compareTo`.
* Метод `main` читает ввод через `Scanner`; удобно собирать логику в `static`-методы, а ввод-вывод оставлять в `main`.

<!-- section: deep-dive -->
## Почему строки сравнивают через equals

Литералы одного содержимого хранятся в общем пуле строк, поэтому `"java" == "java"` чаще всего `true`. Строка, полученная во время выполнения (ввод, склейка, `new String`), - новый объект, и `==` на ней `false`, хотя содержимое совпадает. Именно поэтому ошибка не проявляется в маленьких тестах на литералах и появляется на настоящих данных. Подробнее об устройстве пула и упаковке `Integer` - в [Примитивы и объекты: == против equals, упаковка](lesson:java.first-steps.primitives-objects-and-equality).
