<!-- section: simple -->
## Что проверяет эта проверка

`static`-методы с параметрами и `return`, массивы и их длина, `ArrayList` (`add`, `get`, `size`, `contains`, `remove`), `HashMap` (`put`, `get`, `containsKey`, `getOrDefault`, `merge`), строковые методы `equals`, `trim`, `split`, `substring`, `indexOf`, `toLowerCase` и сравнение строк.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Методы: static, параметры и return](lesson:java.from-zero.methods), [Массивы](lesson:java.from-zero.arrays), [ArrayList: список переменной длины](lesson:java.from-zero.arraylist), [HashMap: пары «ключ - значение»](lesson:java.from-zero.hashmap), [Методы строк: equals, trim, split, substring](lesson:java.from-zero.string-methods).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```java run
import java.util.*;

public class Main {
    static Map<String, Integer> count(String text) {
        Map<String, Integer> counts = new HashMap<>();
        for (String w : text.trim().toLowerCase().split("\\s+")) {
            counts.put(w, counts.getOrDefault(w, 0) + 1);
        }
        return counts;
    }

    public static void main(String[] args) {
        Map<String, Integer> c = count("  Да нет да ");
        System.out.println(c.get("да") + " " + c.get("нет") + " " + c.get("может") + " " + c.containsKey("нет"));
        List<String> words = new ArrayList<>(List.of("b", "a", "c"));
        words.add(1, "x");
        words.remove("c");
        System.out.println(words + " " + words.size() + " " + words.contains("a"));
        int[] a = {5, 3, 8};
        int[] b = a;
        b[0] = 1;
        System.out.println(a[0] + " " + a.length + " " + "hello".substring(1, 3) + " " + "a-b".equals("a-b"));
    }
}
```
```output
2 1 null true
[b, x, a] 3 true
1 3 el true
```
Метод `count` возвращает словарь; `getOrDefault` избавляет от проверки на отсутствие ключа. `get` для отсутствующего слова вернул бы `null` (его печать допустима, распаковка в `int` - нет). У списка `remove("c")` удаляет по значению, а `add(1, "x")` вставляет по индексу. Массивы `a` и `b` - одна и та же структура.

<!-- section: precise -->
## Что нужно уметь

* объявлять `static`-метод с параметрами и результатом и вызывать его из `main`;
* обходить массив и список циклом `for` и `for-each` и помнить, что индексы начинаются с нуля;
* считать частоты через `HashMap` (`getOrDefault`, `merge`) и сохранять порядок первого появления отдельным `ArrayList`;
* различать `remove(int)` и `remove(Object)` у `List<Integer>`;
* сравнивать строки через `equals`, а не `==`;
* разбивать строку `split("\\s+")` и отсекать пустые строки перед разбором;
* понимать, что массив и список передаются в метод по ссылке.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* `==` вместо `equals` для строк.
* `get` на отсутствующем ключе даёт `null`, и присваивание его переменной `int` бросает `NullPointerException`.
* `split(" ")` на строке с несколькими пробелами даёт пустые элементы; на пустой строке - массив из одного пустого элемента.
* Порядок обхода `HashMap` не определён: для порядка появления нужен отдельный список или `LinkedHashMap`.
* `ArrayIndexOutOfBoundsException` из-за `i <= a.length`.
* Присваивание `b = a` для массива не копирует его.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Методы: static, параметры и return](lesson:java.from-zero.methods), [Массивы](lesson:java.from-zero.arrays), [ArrayList: список переменной длины](lesson:java.from-zero.arraylist), [HashMap: пары «ключ - значение»](lesson:java.from-zero.hashmap), [Методы строк: equals, trim, split, substring](lesson:java.from-zero.string-methods). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
