<!-- section: simple -->
## Что проверяет эта проверка

Лямбды и ссылки на методы, конвейер `stream() -> промежуточные операции -> терминальная операция`, `filter`, `map`, `sorted`, `toList`, `Collectors.groupingBy` и `summingInt`, `max` с `Comparator`, ленивость вычислений, `Optional` (`map`, `orElse`, `isPresent`).

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Лямбды, Streams API и Optional](lesson:java.fundamentals.streams-lambdas-optional).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```java run
import java.util.*;
import java.util.stream.*;

public class Main {
    record Item(String name, int price) {}

    public static void main(String[] args) {
        List<Item> items = List.of(new Item("чай", 120), new Item("кофе", 300), new Item("сок", 120));
        List<String> cheap = items.stream()
                .filter(i -> i.price() <= 120)
                .map(Item::name)
                .sorted()
                .toList();
        System.out.println(cheap);
        Map<Integer, Long> byPrice = items.stream()
                .collect(Collectors.groupingBy(Item::price, TreeMap::new, Collectors.counting()));
        System.out.println(byPrice);
        Optional<Item> top = items.stream().max(Comparator.comparingInt(Item::price));
        System.out.println(top.map(Item::name).orElse("нет"));
        Optional<Item> none = Optional.empty();
        System.out.println(none.map(Item::name).orElse("нет"));
    }
}
```
```output
[сок, чай]
{120=2, 300=1}
кофе
нет
```
Конвейер собирается из источника (`stream()`), промежуточных операций (`filter`, `map`, `sorted`) и терминальной операции (`toList`, `collect`, `max`), только она запускает вычисления. `Optional` описывает «значения может не быть»: `map` применяется только к существующему значению, а `orElse` задаёт запасное.

<!-- section: precise -->
## Что нужно уметь

* выражать условие и преобразование лямбдой и ссылкой на метод (`Employee::name`);
* строить конвейер `filter` -> `map` -> `sorted` -> `toList()` и собирать результат;
* группировать через `Collectors.groupingBy` с `TreeMap::new` и `summingInt` и понимать порядок ключей;
* искать максимум через `max(Comparator.comparingInt(...))` и обрабатывать `Optional`;
* помнить, что промежуточные операции ленивы, а поток можно использовать один раз;
* возвращать `Optional` вместо `null` и не вызывать `get()` без проверки.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* Использование уже потреблённого потока: `IllegalStateException: stream has already been operated upon or closed`.
* Промежуточные операции без терминальной ничего не выполняют.
* `Optional.get()` на пустом значении бросает `NoSuchElementException`; используйте `orElse` или `map`.
* Побочные эффекты в `peek` и `map` делают конвейер зависимым от порядка и ленивости.
* `groupingBy` без `TreeMap::new` возвращает `HashMap` с неопределённым порядком ключей.
* `sorted()` без компаратора требует, чтобы элементы были `Comparable`.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Лямбды, Streams API и Optional](lesson:java.fundamentals.streams-lambdas-optional). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
