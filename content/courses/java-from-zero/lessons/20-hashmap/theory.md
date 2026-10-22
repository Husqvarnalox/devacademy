<!-- section: simple -->
## Зачем нужен словарь

Список находит элемент по номеру. Но часто номер неудобен: «сколько яблок на складе?», «какая оценка у Анны?», «сколько раз встретилось слово?». Мы хотим искать значение по **имени**, а не по позиции. Для этого в Java есть `HashMap` - словарь, который хранит пары «ключ - значение»: ключ (например, название товара) однозначно определяет значение (количество).

## Одна новая идея: HashMap<ключ, значение>

```java
import java.util.HashMap;

HashMap<String, Integer> stock = new HashMap<>();
```

В угловых скобках теперь **два типа**: первый - тип **ключей**, второй - тип **значений**. `HashMap<String, Integer>` - «словарь, где ключ - текст, а значение - целое число». (Как у списка, вместо `int` пишут `Integer`.)

Основные действия:

| Запись | Что делает |
|---|---|
| `stock.put("яблоки", 12)` | записывает пару; если ключ уже был, **заменяет** значение |
| `stock.get("яблоки")` | возвращает значение по ключу, а если ключа нет - особое значение `null` («ничего») |
| `stock.getOrDefault("груши", 0)` | значение по ключу или заданное запасное, если ключа нет |
| `stock.containsKey("яблоки")` | есть ли такой ключ (`true` или `false`) |
| `stock.remove("яблоки")` | удаляет пару по ключу |
| `stock.size()` | число пар |

Ключи в словаре **уникальны**: у одного ключа не бывает двух значений. Порядок пар не определён: нельзя рассчитывать, что обход вернёт их в порядке добавления или по алфавиту.

Все ключи обходят циклом `for (String key : stock.keySet())`, все значения - `for (int v : stock.values())`. Значение по ключу внутри цикла получают как `stock.get(key)`.

Самый частый приём - **подсчёт**: `counts.put(word, counts.getOrDefault(word, 0) + 1)` означает «взять прежнее количество (или 0, если слова ещё не было) и увеличить на 1».

<!-- section: examples -->
## Пример 1. Создание и чтение

```java run
import java.util.HashMap;

public class Main {
    public static void main(String[] args) {
        HashMap<String, Integer> stock = new HashMap<>();
        stock.put("яблоки", 12);
        stock.put("груши", 5);
        System.out.println("Яблок: " + stock.get("яблоки"));
        System.out.println("Видов товара: " + stock.size());
        stock.put("яблоки", 20);
        System.out.println("Яблок после обновления: " + stock.get("яблоки"));
        System.out.println("Видов товара: " + stock.size());
    }
}
```
```output
Яблок: 12
Видов товара: 2
Яблок после обновления: 20
Видов товара: 2
```

Разбор:

* Ключ - название, значение - количество. `put` с новым ключом добавляет пару, `get` читает значение.
* Повторный `put("яблоки", 20)` не создал вторую пару, а заменил значение: ключ уникален, размер остался равным 2.

## Пример 2. Проверка ключа и запасное значение

```java run
import java.util.HashMap;

public class Main {
    public static void main(String[] args) {
        HashMap<String, Integer> stock = new HashMap<>();
        stock.put("яблоки", 12);
        System.out.println(stock.containsKey("яблоки"));
        System.out.println(stock.containsKey("груши"));
        System.out.println(stock.get("груши"));
        System.out.println(stock.getOrDefault("груши", 0));
        stock.remove("яблоки");
        System.out.println("Осталось видов: " + stock.size());
    }
}
```
```output
true
false
null
0
Осталось видов: 0
```

Разбор:

* Если ключа нет, `get` возвращает `null` - значение «ничего»; оно печатается как слово «null».
* `getOrDefault("груши", 0)` в этом случае возвращает запасное значение 0, поэтому им удобно пользоваться вместо проверки `containsKey` и `get` по отдельности.
* После `remove` пара «яблоки - 12» исчезла.

## Пример 3. Подсчёт слов

```java run
import java.util.HashMap;
import java.util.Scanner;

public class Main {
    public static void main(String[] args) {
        Scanner in = new Scanner("кот пёс кот рыба кот пёс");
        HashMap<String, Integer> counts = new HashMap<>();
        while (in.hasNext()) {
            String word = in.next();
            counts.put(word, counts.getOrDefault(word, 0) + 1);
        }
        System.out.println("кот: " + counts.getOrDefault("кот", 0));
        System.out.println("пёс: " + counts.getOrDefault("пёс", 0));
        System.out.println("рыба: " + counts.getOrDefault("рыба", 0));
        System.out.println("птица: " + counts.getOrDefault("птица", 0));
        System.out.println("Разных слов: " + counts.size());
    }
}
```
```output
кот: 3
пёс: 2
рыба: 1
птица: 0
Разных слов: 3
```

Разбор:

* Для каждого слова строка `counts.put(word, counts.getOrDefault(word, 0) + 1)` берёт прежнее число (0, если слово встречено впервые), прибавляет 1 и записывает обратно.
* Для слова, которого нет во вводе, `getOrDefault` даёт 0, поэтому отдельная проверка не нужна.
* Здесь слова печатаются по именам в заданном порядке. Если печатать обход самого словаря, порядок пар заранее не определён.

## Пример 4. Обход значений и ключей

```java run
import java.util.HashMap;

public class Main {
    public static void main(String[] args) {
        HashMap<String, Integer> stock = new HashMap<>();
        stock.put("яблоки", 12);
        stock.put("груши", 5);
        stock.put("сливы", 1);

        int total = 0;
        for (int amount : stock.values()) {
            total += amount;
        }
        System.out.println("Всего единиц: " + total);

        int rare = 0;
        for (String name : stock.keySet()) {
            if (stock.get(name) < 5) {
                rare++;
            }
        }
        System.out.println("Заканчиваются: " + rare);
    }
}
```
```output
Всего единиц: 18
Заканчиваются: 1
```

Разбор:

* `values()` - все значения; `keySet()` - все ключи. Каждый цикл прошёл по каждой паре ровно один раз.
* Результаты циклов не зависят от порядка обхода: сумма и счётчик одинаковы при любом порядке. Если нужен конкретный порядок, ключи берут из отдельного списка или упорядочивают перед печатью.
* `stock.get(name) < 5` сравнивает значение с числом: `Integer` автоматически становится `int`.

## Пример 5. Словарь в методе

```java run
import java.util.HashMap;

public class Main {
    static void addStock(HashMap<String, Integer> stock, String item, int qty) {
        stock.put(item, stock.getOrDefault(item, 0) + qty);
    }

    public static void main(String[] args) {
        HashMap<String, Integer> stock = new HashMap<>();
        addStock(stock, "яблоки", 10);
        addStock(stock, "груши", 4);
        addStock(stock, "яблоки", 5);
        System.out.println("Яблок: " + stock.get("яблоки"));
        System.out.println("Груш: " + stock.get("груши"));
    }
}
```
```output
Яблок: 15
Груш: 4
```

Разбор:

* Словарь передаётся в метод по ссылке (как массив и список), поэтому `addStock` изменяет тот словарь, который создан в `main`.
* Поставки одного товара складываются: второй `addStock` для яблок прибавил 5 к уже записанным 10.

**Попробуйте.** В примере 3 добавьте ещё несколько повторяющихся слов и проверьте, что их счётчики растут. Затем напишите метод, который возвращает слово, встретившееся чаще всех (обходите `keySet()` и сравнивайте `counts.get(word)`).

<!-- section: precise -->
## Точные правила

* `HashMap<K, V>`: `K` - тип ключа, `V` - тип значения; оба ссылочные (`String`, `Integer`, `Double`, `Boolean`, `Character`...). Примитивы в скобках не допускаются.
* Ключи уникальны. `put` с существующим ключом заменяет значение и возвращает старое.
* `get(ключ)` возвращает `null`, если ключа нет. Присваивание такого результата переменной `int` приводит к `NullPointerException`. Надёжные способы: `containsKey` или `getOrDefault`.
* `getOrDefault(ключ, запасное)` ничего не добавляет в словарь: если ключа нет, словарь после вызова остаётся прежним.
* Ключи сравниваются по содержимому (для строк - `equals`), а не по ссылке.
* Порядок обхода `HashMap` не определён и может зависеть от версии Java. Нельзя строить вывод программы на этом порядке, если задача требует определённый.
* Во время обхода словарь нельзя менять (добавлять и удалять ключи): это приводит к `ConcurrentModificationException`. Изменение значения существующего ключа допустимо.
* Присваивание `HashMap<...> b = a;` копирует ссылку, а не содержимое, как у массивов и списков.
* Для хранения в качестве ключа нужен тип с корректными `equals` и `hashCode`; у `String` и числовых обёрток они есть.
* Если порядок важен, есть другие словари (`LinkedHashMap` - в порядке добавления, `TreeMap` - по возрастанию ключей); о них в `java-fundamentals`.

<!-- section: mistakes -->
## Типичные ошибки

**1. `get` отсутствующего ключа, записанный в `int`.**

```java
import java.util.HashMap;

public class Main {
    public static void main(String[] args) {
        HashMap<String, Integer> stock = new HashMap<>();
        stock.put("яблоки", 12);
        int pears = stock.get("груши");
        System.out.println(pears);
    }
}
```

```text
Exception in thread "main" java.lang.NullPointerException: Cannot invoke "java.lang.Integer.intValue()" because the return value of "java.util.HashMap.get(Object)" is null
	at Main.main(Main.java:7)
```

`get` вернул `null`, а `int` не может хранить «ничего». Используйте `stock.getOrDefault("груши", 0)` или сначала проверьте `containsKey`.

**2. Примитив в угловых скобках.**

```java
import java.util.HashMap;

public class Main {
    public static void main(String[] args) {
        HashMap<String, int> stock = new HashMap<>();
    }
}
```

```text
Main.java:5: error: unexpected type
        HashMap<String, int> stock = new HashMap<>();
                        ^
  required: reference
  found:    int
1 error
```

Как и у списка, значение должно быть ссылочным типом: `Integer`.

**3. Квадратные скобки вместо `get`.**

```java
import java.util.HashMap;

public class Main {
    public static void main(String[] args) {
        HashMap<String, Integer> stock = new HashMap<>();
        System.out.println(stock["яблоки"]);
    }
}
```

```text
Main.java:6: error: incompatible types: String cannot be converted to int
        System.out.println(stock["яблоки"]);
                                 ^
Main.java:6: error: array required, but HashMap<String,Integer> found
        System.out.println(stock["яблоки"]);
                                ^
2 errors
```

Обращение через квадратные скобки есть только у массивов. В словаре значение берут вызовом `stock.get("яблоки")`.

**4. Значение не того типа.**

```java
import java.util.HashMap;

public class Main {
    public static void main(String[] args) {
        HashMap<String, Integer> stock = new HashMap<>();
        stock.put("яблоки", "много");
    }
}
```

```text
Main.java:6: error: incompatible types: String cannot be converted to Integer
        stock.put("яблоки", "много");
                            ^
Note: Some messages have been simplified; recompile with -Xdiags:verbose to get full output
1 error
```

Тип значения задан в угловых скобках: `Integer`. Строку в такой словарь положить нельзя.

**5. Забыт второй аргумент.**

```java
import java.util.HashMap;

public class Main {
    public static void main(String[] args) {
        HashMap<String, Integer> stock = new HashMap<>();
        stock.put("яблоки");
    }
}
```

```text
Main.java:6: error: method put in class HashMap<K,V> cannot be applied to given types;
        stock.put("яблоки");
             ^
  required: String,Integer
  found:    String
  reason: actual and formal argument lists differ in length
  where K,V are type-variables:
    K extends Object declared in class HashMap
    V extends Object declared in class HashMap
1 error
```

`put` принимает ключ и значение. Сообщение показывает, какие аргументы требуются (`required`), какие переданы (`found`) и почему они не подошли (`reason`).

**6. Ожидание порядка.** Если напечатать весь словарь (`System.out.println(stock)`) или обойти его циклом, пары могут выйти не в порядке добавления и не по алфавиту. Это не ошибка: порядок не определён. Для проверяемого вывода обращайтесь к значениям по известным ключам.

**7. Перезапись вместо накопления.** Строка `counts.put(word, 1)` на каждом слове записывает 1 и стирает прежний счётчик. Нужно прибавлять к прежнему значению: `counts.put(word, counts.getOrDefault(word, 0) + 1)`.

<!-- section: summary -->
## Итоги

* `HashMap<K, V>` хранит пары «ключ - значение»; ключ уникален, значение читают по ключу.
* `put` записывает или заменяет, `get` читает (`null`, если ключа нет), `getOrDefault` подставляет запасное значение, `containsKey` проверяет наличие, `size` считает пары.
* Подсчёт: `map.put(k, map.getOrDefault(k, 0) + 1)`.
* Обход - `keySet()` и `values()`; порядок пар не определён.

Данные собираются в структуры; теперь нужно уметь разбирать текст, из которого их получают: [Методы строк](lesson:java.from-zero.string-methods).

<!-- section: programmer -->
## Для тех, кто уже программирует

* `HashMap` - хеш-таблица, аналог `dict` в Python, `Map` в JavaScript, `map` в Go и `Dictionary` в C#. Ожидаемое время операций - константа. Как и `ArrayList`, хранит только ссылочные типы (`Integer`, а не `int`).
* Порядок обхода не гарантируется (в отличие от `dict` в Python 3.7+). Для порядка вставки - `LinkedHashMap`, для сортировки по ключу - `TreeMap`.
* `get` на отсутствующем ключе возвращает `null`, а не бросает исключение; распаковка в `int` даёт `NullPointerException`. Идиомы: `getOrDefault`, `merge(k, 1, Integer::sum)`, `computeIfAbsent(k, x -> new ArrayList<>())`.
* Ключи сравниваются через `equals` и `hashCode`. Изменяемый объект в роли ключа опасен: после изменения он «теряется» в таблице. Для строк и числовых обёрток проблем нет.
* `HashMap` допускает один ключ `null` и значения `null`; `Hashtable` и `ConcurrentHashMap` - нет. `HashMap` не потокобезопасен.
* Фабрика `Map.of("a", 1, "b", 2)` создаёт неизменяемый словарь; запись в него бросает `UnsupportedOperationException`.
* Обход пар: `for (Map.Entry<String, Integer> e : map.entrySet())` с `e.getKey()` и `e.getValue()`; в курсе используются `keySet()` и `values()`.

<!-- section: deep-dive -->
## Как устроена хеш-таблица

Это необязательный раздел: для заданий он не нужен.

`HashMap` хранит массив «корзин». Чтобы найти место для ключа, вызывают `hashCode()` ключа, смешивают биты полученного числа и берут остаток от деления на число корзин. В корзине лежит короткий список пар (начиная с Java 8 длинные цепочки превращаются в сбалансированные деревья). Поиск по ключу сводится к вычислению корзины и сравнению ключей через `equals` внутри неё, поэтому он занимает почти постоянное время.

Когда пар становится больше определённой доли от числа корзин (коэффициент заполнения по умолчанию 0,75), таблица увеличивается вдвое и все ключи раскладываются заново. Поэтому порядок обхода зависит от хеш-кодов, размера таблицы и порядка вставки - и меняться между версиями Java. Для строк хеш-код определён спецификацией (`s[0]*31^(n-1) + ...`), но порядок обхода всё равно не гарантируется.
