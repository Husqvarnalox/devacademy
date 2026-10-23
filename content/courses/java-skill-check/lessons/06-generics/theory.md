<!-- section: simple -->
## Что проверяет эта проверка

Обобщённые методы и классы, ограничения `extends` и `Comparable<T>`, шаблоны `? extends` и `? super` (правило producer extends, consumer super), инвариантность `List<Integer>` и `List<Number>`, стирание типов, `Map` и `TreeMap`.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Обобщения и фреймворк коллекций](lesson:java.fundamentals.generics-collections).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```java run
import java.util.*;

public class Main {
    static <T extends Comparable<T>> T largest(List<T> xs) {
        T best = xs.get(0);
        for (T x : xs) {
            if (x.compareTo(best) > 0) {
                best = x;
            }
        }
        return best;
    }

    static double total(List<? extends Number> xs) {
        double s = 0;
        for (Number n : xs) {
            s += n.doubleValue();
        }
        return s;
    }

    static void fill(List<? super Integer> out) {
        out.add(1);
        out.add(2);
    }

    public static void main(String[] args) {
        System.out.println(largest(List.of(3, 9, 4)) + " " + largest(List.of("pear", "fig")));
        System.out.println(total(List.of(1, 2)) + " " + total(List.of(1.5, 2.5)));
        List<Object> mixed = new ArrayList<>();
        fill(mixed);
        System.out.println(mixed);
    }
}
```
```output
9 pear
3.0 4.0
[1, 2]
```
`T extends Comparable<T>` разрешает вызывать `compareTo` у элементов. `List<? extends Number>` можно читать (каждый элемент - `Number`), но нельзя в него добавлять; `List<? super Integer>` наоборот - в него добавляют `Integer`, но читают как `Object`. Для списка `List.of(3, 9, 4)` тип `T` выводится как `Integer`.

<!-- section: precise -->
## Что нужно уметь

* писать обобщённый метод `<T extends Comparable<T>> T maxOf(List<T>)` и вызывать `compareTo`;
* применять `? extends` для чтения (источник) и `? super` для записи (приёмник) - правило PECS;
* объяснять, почему `List<Integer>` нельзя присвоить `List<Number>`: обобщённые типы инвариантны;
* помнить, что после компиляции параметры типа стираются, поэтому `ArrayList<String>` и `ArrayList<Integer>` имеют один класс;
* выбирать `Map`, `TreeMap`, `HashMap` по нужному порядку обхода;
* читать сообщения компилятора об обобщённых типах (`incompatible types`).

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* `List<Number> ns = new ArrayList<Integer>();` не компилируется: `incompatible types`.
* Добавление в `List<? extends Number>` не компилируется: компилятор не знает точный тип элементов.
* Попытка создать `new T()` или `new T[10]` невозможна из-за стирания типов.
* Сырой тип (`List` без параметра) отключает проверки и переносит ошибку в выполнение (`ClassCastException`).
* `<T extends Comparable>` без параметра даёт предупреждения; правильная форма - `Comparable<T>`.
* Порядок обхода `HashMap` не определён, а `TreeMap` обходит ключи по возрастанию.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Обобщения и фреймворк коллекций](lesson:java.fundamentals.generics-collections). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
