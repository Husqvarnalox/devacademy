<!-- section: simple -->
## В Java два мира: примитивы и объекты

Переменная типа `int`, `long`, `double`, `boolean`, `char` хранит **само значение**. Переменная любого другого типа (`String`, `Integer`, массив, ваш класс) хранит **ссылку** на объект в куче.
От этого зависят три вещи, в которых ошибаются чаще всего:

* оператор `==` у примитивов сравнивает значения, а у ссылок - **один ли это объект**;
* для сравнения содержимого объектов используют метод **`equals`**;
* между примитивами и объектами-обёртками (`int` и `Integer`) компилятор вставляет автоматические преобразования - **упаковку** и **распаковку** (autoboxing).

```java
String a = new String("java");
String b = new String("java");
System.out.println(a == b);       // false: два разных объекта
System.out.println(a.equals(b));  // true: одинаковое содержимое
```

<!-- section: precise -->
## Ссылки, equals, упаковка и кэш Integer

**Передача параметров.** В Java всегда передаётся **копия значения**. Для примитива это число, для объекта - копия ссылки. Метод может изменить объект, на который указывает ссылка (например, элемент массива),
но не может заставить переменную вызывающего указывать на другой объект. Присваивание `arr = new int[5]` внутри метода меняет только локальную копию ссылки.

**`==` и `equals`.** Для примитивов `==` сравнивает значения. Для ссылок - идентичность: тот же ли объект. `Object.equals` по умолчанию тоже сравнивает идентичность, но большинство классов (`String`, `Integer`, `List`) переопределяют его так, чтобы сравнивалось содержимое.
Контракт `equals` требует рефлексивности, симметричности, транзитивности и того, что `x.equals(null)` ложно. `equals` у `Integer` возвращает `true` только для другого `Integer` с тем же числом: `Integer.valueOf(5).equals(5L)` ложно, потому что `5L` упаковывается в `Long`.

**Строки.** Литералы `"java"` компилятор помещает в общий пул, и два одинаковых литерала - один объект, так что `"java" == "java"` истинно. `new String("java")` и строки, прочитанные из ввода, - отдельные объекты, и `==` для них ложно.
Метод `intern()` возвращает строку из пула. Сравнивать строки всегда нужно через `equals` (или `equalsIgnoreCase`). Чтобы избежать `NullPointerException`, пишут `"java".equals(s)` или `Objects.equals(a, b)`.

**Упаковка и распаковка.** `Integer x = 5;` компилируется в `Integer.valueOf(5)`, а `int y = x;` - в `x.intValue()`. Спецификация гарантирует, что упаковка значений от -128 до 127 возвращает один и тот же объект (кэш), поэтому `Integer a = 127, b = 127; a == b` истинно.
Для значений вне этого диапазона спецификация ничего не обещает; на практике создаются разные объекты, и `Integer a = 1000, b = 1000; a == b` ложно. Результат `==` для обёрток нельзя использовать как сравнение чисел.
Распаковка `null` бросает `NullPointerException`: `Integer x = null; int y = x;`.

**Сравнение `double`.** `0.0 == -0.0` истинно, и `Double.NaN == Double.NaN` ложно. Но `Double.equals` и `Double.compare` ведут себя иначе: `Double.valueOf(0.0).equals(-0.0)` ложно, а `Double.valueOf(Double.NaN).equals(Double.NaN)` истинно. Такие правила нужны, чтобы `equals` оставался отношением эквивалентности для коллекций.

**Перегрузка и упаковка.** `List<Integer> list; list.remove(1)` вызывает `remove(int index)` и удаляет элемент по индексу, а `list.remove(Integer.valueOf(1))` удаляет значение 1. Компилятор предпочитает вызов без упаковки.

**Коллекции и примитивы.** Обобщённые типы (`List<T>`, `Map<K,V>`) работают только с объектами, поэтому `List<int>` недопустим, а `List<Integer>` хранит упакованные значения. Это дороже по памяти и времени, чем массив `int[]`.

<!-- section: examples -->
## Примеры

**Пример 1. `==` и `equals` для строк.**

```java run
public class Main {
    public static void main(String[] args) {
        String lit1 = "java";
        String lit2 = "java";
        String made = new String("java");
        System.out.println(lit1 == lit2);
        System.out.println(lit1 == made);
        System.out.println(lit1.equals(made));
        System.out.println(lit1 == made.intern());
    }
}
```
```output
true
false
true
true
```

Одинаковые литералы - один объект из пула; `new String` создаёт новый объект, а `intern()` возвращает объект из пула.

**Пример 2. Кэш `Integer`.**

```java run
public class Main {
    public static void main(String[] args) {
        Integer a = 127, b = 127;
        Integer c = 1000, d = 1000;
        System.out.println(a == b);
        System.out.println(c.equals(d));
        System.out.println(c.intValue() == d.intValue());
        Integer e = 5;
        Long f = 5L;
        System.out.println(e.equals(f));
    }
}
```
```output
true
true
true
false
```

Для сравнения чисел-обёрток используйте `equals` или распаковку. `Integer` и `Long` с равными числами не равны через `equals`: это разные классы.

**Пример 3. Что видит метод.**

```java run
import java.util.Arrays;

public class Main {
    static void change(int[] arr, int x) {
        arr[0] = 99;
        x = 99;
        arr = new int[] {7, 7};
    }

    public static void main(String[] args) {
        int[] data = {1, 2};
        int n = 1;
        change(data, n);
        System.out.println(Arrays.toString(data) + " " + n);
    }
}
```
```output
[99, 2] 1
```

Элемент массива изменился, потому что метод получил копию ссылки на тот же массив. Переменная `n` не изменилась, а новый массив в `arr` виден только внутри метода.

**Пример 4. Ловушки с `null` и `remove`.**

```java run
import java.util.ArrayList;
import java.util.List;

public class Main {
    public static void main(String[] args) {
        List<Integer> list = new ArrayList<>(List.of(10, 1, 2));
        list.remove(1);
        System.out.println(list);
        list.remove(Integer.valueOf(10));
        System.out.println(list);
        try {
            Integer x = null;
            int y = x;
            System.out.println(y);
        } catch (NullPointerException e) {
            System.out.println("NPE при распаковке");
        }
    }
}
```
```output
[10, 2]
[2]
NPE при распаковке
```

**Пример 5. `double`: `==` против `equals`.**

```java run
public class Main {
    public static void main(String[] args) {
        System.out.println(0.0 == -0.0);
        System.out.println(Double.valueOf(0.0).equals(-0.0));
        System.out.println(Double.NaN == Double.NaN);
        System.out.println(Double.valueOf(Double.NaN).equals(Double.NaN));
    }
}
```
```output
true
false
false
true
```

<!-- section: mistakes -->
## Типичные ошибки

* **`if (s == "yes")`.** Для строк из ввода, из `substring` или `new String` условие ложно, даже если текст совпадает. Пишите `"yes".equals(s)`.
* **Сравнение `Integer` через `==`.** Работает только в кэше (-128..127) и ломается на 128. Используйте `equals` либо распаковку.
* **Распаковка `null`.** `int v = map.get(key);` при отсутствии ключа бросает `NullPointerException`. Проверяйте `containsKey` или используйте `getOrDefault`.
* **`list.remove(i)` для `List<Integer>`.** Удаляется элемент по индексу, а не значение. Для значения нужен `Integer.valueOf(...)`.
* **Ожидание, что метод поменяет ссылку вызывающего.** Присваивание параметру не влияет на переменную снаружи.
* **Переопределить `equals`, но не `hashCode`.** Объекты с равным содержимым попадут в разные корзины `HashMap`. Эти методы нужно менять парой.
* **Смешивать `Integer` и `Long` в `equals`.** `Integer.valueOf(5).equals(5L)` ложно из-за разных классов.
* **Использовать `==` для `double` после вычислений.** `0.1 + 0.2 == 0.3` ложно из-за двоичного представления; сравнивайте с допуском.

<!-- section: summary -->
## Итоги

* Примитивы хранят значения, остальное - ссылки на объекты в куче; параметры методов всегда передаются копией (значения или ссылки).
* `==` у ссылок сравнивает идентичность; содержимое сравнивает `equals`. Строки и обёртки сравнивайте только через `equals`.
* Упаковка в диапазоне -128..127 использует кэш; вне диапазона `==` для `Integer` ненадёжно.
* `Integer.equals(Long)` ложно; у `Double` `equals` и `==` расходятся для `NaN` и `-0.0`.
* Распаковка `null` и `remove(int)` против `remove(Object)` - частые источники багов.

Сравните с [равенством в JavaScript](lesson:javascript.first-steps.values-and-equality) и [именами и объектами Python](lesson:python.fundamentals.variables-and-input).
