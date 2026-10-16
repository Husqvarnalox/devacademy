<!-- section: simple -->
## Коллекции, функции высшего порядка и последовательности

Стандартная библиотека Kotlin строится вокруг трёх идей.

* **Интерфейсы только для чтения и изменяемые интерфейсы.** `List`, `Set`, `Map` не имеют методов изменения; `MutableList`, `MutableSet`, `MutableMap` имеют. Создавайте `listOf(...)`, когда изменения не нужны, и `mutableListOf(...)`, когда нужны.
* **Функции высшего порядка.** `map`, `filter`, `fold`, `groupBy`, `sortedBy` принимают лямбду. Вместо циклов с индексами вы описываете преобразование.
* **Эффективность через `Sequence`.** Операции над `List` **жадные**: каждая создаёт новую коллекцию. Операции над `Sequence` **ленивые**: элементы идут по цепочке по одному, и вычисление останавливается, как только результат получен.

Выбор простой: небольшие коллекции и несколько шагов - обычные списки; длинные цепочки, большие источники или `take`/`first` на поздних шагах - последовательности.

<!-- section: precise -->
## Подробнее

**Только для чтения не значит неизменяемый.** `List<T>` - это «вид» без методов записи; под ним может быть изменяемый список, и его могут менять через другую ссылку. Копия `toList()` фиксирует состояние.

**Лямбды и типы функций.** `(Int) -> Boolean` - тип функции. Если последний параметр - функция, лямбду выносят за скобки (`list.filter { it > 0 }`). Единственный параметр можно не называть: он `it`. Ссылка на функцию: `list.map(::println)`. Лямбда захватывает переменные окружения и может их изменять (замыкание).

**Часто используемые операции.**
* `map`, `filter`, `mapNotNull`, `flatMap`, `distinct`;
* `fold(init) { acc, x -> ... }` и `reduce`;
* `groupBy { key }` возвращает `Map<K, List<T>>`, `groupingBy { key }.eachCount()` считает частоты без промежуточных списков;
* `associateBy`, `associate`, `partition`, `zip`, `windowed`, `chunked`;
* `sortedBy`, `sortedWith(compareBy(...).thenBy(...))`, `maxByOrNull`;
* `firstOrNull`, `getOrNull`, `any`, `all`, `none`, `count`;
* `sumOf { }` для сумм заданного типа.

**Последовательности.** `asSequence()` превращает коллекцию в `Sequence`. Промежуточные операции (`map`, `filter`, `take`) ленивые и ничего не вычисляют. Терминальные (`toList`, `first`, `sum`, `forEach`) запускают конвейер. Элемент проходит все шаги, прежде чем начнётся обработка следующего, поэтому `first { ... }` или `take(m)` останавливают работу сразу. Бесконечные последовательности строят `generateSequence(seed) { next }` и билдер `sequence { yield(x) }`.

**Когда последовательность не нужна.** Для коротких коллекций накладные расходы ленивости могут быть выше выигрыша. Последовательность однопроходная в том смысле, что каждый терминальный вызов заново выполняет цепочку; кэшировать результат нужно явно (`toList()`).

**Изменяемые коллекции.** `MutableList.add`, `removeAll { }`, `sortBy { }` меняют на месте; `sortedBy { }` возвращает новый список. Для `Map`: `getOrPut(key) { ... }`, `merge`, `computeIfAbsent` (из Java).

<!-- section: examples -->
## Примеры

**Пример 1. Преобразования, группировка и сортировка.**

```kotlin run
data class Emp(val name: String, val dept: String, val salary: Int)

fun main() {
    val staff = listOf(
        Emp("ann", "dev", 300), Emp("bob", "ops", 200),
        Emp("cid", "dev", 250), Emp("dan", "ops", 220), Emp("eve", "qa", 180),
    )
    println(staff.filter { it.salary >= 220 }.map { it.name })
    println(staff.groupBy { it.dept }.mapValues { (_, v) -> v.sumOf { it.salary } })
    val (high, low) = staff.partition { it.salary > 230 }
    println("${high.size} ${low.size}")
    println(staff.sortedWith(compareBy<Emp> { it.dept }.thenByDescending { it.salary }).map { it.name })
    println(staff.associateBy({ it.name }, { it.salary })["cid"])
    println(staff.fold(0) { acc, e -> acc + e.salary })
    println(staff.maxByOrNull { it.salary }?.name)
    println(listOf(1, 2, 3, 4, 5).windowed(2).map { it.sum() })
}
```
```output
[ann, cid, dan]
{dev=550, ops=420, qa=180}
2 3
[ann, cid, dan, bob, eve]
250
1150
ann
[3, 5, 7, 9]
```

**Пример 2. Жадные списки и ленивая последовательность.**

```kotlin run
fun main() {
    val data = listOf(1, 2, 3, 4)

    println("список:")
    val eager = data
        .map { println("  map $it"); it * 10 }
        .filter { println("  filter $it"); it > 10 }
        .first()
    println("результат $eager")

    println("последовательность:")
    val lazy = data.asSequence()
        .map { println("  map $it"); it * 10 }
        .filter { println("  filter $it"); it > 10 }
        .first()
    println("результат $lazy")
}
```
```output
список:
  map 1
  map 2
  map 3
  map 4
  filter 10
  filter 20
  filter 30
  filter 40
результат 20
последовательность:
  map 1
  filter 10
  map 2
  filter 20
результат 20
```

**Пример 3. Функции как значения и бесконечные последовательности.**

```kotlin run
fun twice(f: (Int) -> Int): (Int) -> Int = { x -> f(f(x)) }

fun counter(): () -> Int {
    var n = 0
    return { ++n }                       // замыкание хранит n
}

fun main() {
    val inc = { x: Int -> x + 1 }
    println(twice(inc)(5))
    println(twice { it * 3 }(2))
    val c = counter()
    c(); c()
    println(c())
    println(listOf("a", "bb", "ccc").map(String::length))

    val fib = generateSequence(1 to 1) { (a, b) -> b to a + b }.map { it.first }
    println(fib.take(10).toList())

    val evensSquares = sequence {
        var i = 0
        while (true) { yield(i * i); i += 2 }
    }
    println(evensSquares.drop(1).take(4).toList())
}
```
```output
7
18
3
[1, 2, 3]
[1, 1, 2, 3, 5, 8, 13, 21, 34, 55]
[4, 16, 36, 64]
```

<!-- section: mistakes -->
## Типичные ошибки

* **Ждать неизменяемости от `List`.** Это только интерфейс чтения; под ним может быть `ArrayList`.
* **Забыть терминальную операцию.** `seq.map { ... }` сама ничего не вычисляет и не выводит.
* **Длинная жадная цепочка на большом списке.** Каждый шаг создаёт промежуточную коллекцию; перейдите на `asSequence()`.
* **Побочные эффекты внутри `map`.** Порядок и число вызовов у списков и последовательностей разные; для эффектов используйте `forEach` или `onEach`.
* **Повторно обходить последовательность и ждать кэша.** Каждый терминальный вызов повторяет конвейер.
* **`sortBy` вместо `sortedBy`.** Первый меняет изменяемый список на месте, второй возвращает новый.
* **Путать `reduce` и `fold` на пустой коллекции.** `reduce` бросает исключение, у `fold` есть начальное значение.
* **Вызывать `first()` на пустой коллекции.** Используйте `firstOrNull()`.

<!-- section: summary -->
## Итоги

* `List`/`Set`/`Map` только читают, `Mutable*` меняют; `listOf` - значение по умолчанию.
* `map`, `filter`, `fold`, `groupBy`, `sortedWith` описывают преобразования вместо циклов.
* `Sequence` ленива: элемент проходит весь конвейер до следующего, `first`/`take` останавливают работу.
* Лямбды захватывают окружение, функции можно возвращать и передавать.

Сравните с [LINQ в C#](lesson:csharp.fundamentals.linq) (там `IEnumerable` тоже ленив) и [итераторами в Rust](lesson:rust.fundamentals.collections-and-iterators). Дальше: [обобщения и вариантность](lesson:kotlin.fundamentals.generics-and-variance).

<!-- section: facts -->
## Факты

* Операции над `Iterable` вычисляются жадно, над `Sequence` - лениво, элемент за элементом.
* `groupingBy { }.eachCount()` считает частоты без создания списков групп.
