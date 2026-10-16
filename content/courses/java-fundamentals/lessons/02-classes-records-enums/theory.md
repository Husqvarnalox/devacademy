<!-- section: simple -->
## Класс, запись и перечисление: три способа описать понятие

В Java почти всё - это класс, но у языка есть три формы объявления типа, и выбор между ними выражает ваш замысел:

* **`class`** - объект с поведением и, как правило, изменяемым состоянием; по умолчанию два объекта равны, только если это *один и тот же* объект.
* **`record`** (Java 16) - прозрачный носитель данных. Вы перечисляете компоненты, а компилятор сам пишет конструктор, методы доступа, `equals`, `hashCode` и `toString`. Равенство - по значению компонентов.
* **`enum`** - закрытое множество именованных значений: уровень логирования, день недели, состояние автомата. Экземпляры создаются один раз при загрузке класса и сравниваются через `==`.

Главная привычка этой темы - **неизменяемость**. Объект, который нельзя изменить после создания, безопасно передавать между потоками, использовать как ключ в `HashMap` и не опасаться, что кто-то испортит его за вашей спиной. «Изменение» такого объекта - это создание нового.

<!-- section: precise -->
## Точные правила

**Класс и контракт `equals`/`hashCode`.** Реализация по умолчанию в `Object` сравнивает ссылки (`equals`) и возвращает значение, связанное с идентичностью (`hashCode`). Если вы переопределяете `equals`, то обязаны переопределить и `hashCode`. Контракт:

* `equals` рефлексивен, симметричен, транзитивен, постоянен и для `null` возвращает `false`;
* равные объекты **обязаны** иметь равные `hashCode`; неравные могут иметь равные (коллизия).

`HashMap` и `HashSet` сначала ищут корзину по `hashCode`, затем сравнивают через `equals`. Если поле, входящее в `hashCode`, изменить после вставки в множество, объект окажется «потерян»: он лежит в старой корзине, а поиск идёт в новой. Поэтому ключи делают неизменяемыми. Вспомогательные методы: `Objects.equals(a, b)` (учитывает `null`), `Objects.hash(...)`.

**Записи (`record`, Java 16).** `record Point(int x, int y) {}` создаёт финальный класс с закрытыми финальными полями, каноническим конструктором, методами доступа `x()` и `y()` (без префикса `get`), а также `equals`, `hashCode`, `toString` формата `Point[x=1, y=2]`. Запись нельзя наследовать и она не может наследовать класс, но может реализовывать интерфейсы, иметь статические члены и дополнительные методы. Нельзя объявить дополнительные поля экземпляра.

* **Компактный конструктор** `Point { if (x < 0) throw new IllegalArgumentException(); }` выполняет проверку и нормализацию параметров до присваивания полей.
* Равенство сравнивает компоненты: ссылочные - через `equals`, примитивы - по значению (`float` и `double` через `compare`, поэтому `NaN` равно `NaN`).
* **Запись неглубоко неизменяема.** Если компонент - `List` или массив, сам список можно изменить. Массив в записи сравнивается по ссылке. Для списков делайте защитную копию: `List.copyOf(items)` в компактном конструкторе.
* Изменить запись нельзя; «вариации» делают методами, возвращающими новую запись (шаблон *wither*): `Point withX(int nx) { return new Point(nx, y); }`.

**Перечисления.** `enum` - это класс, у которого ровно заданные экземпляры. Они могут иметь поля, конструкторы (неявно закрытые), методы и даже собственное тело для каждой константы. `values()` возвращает массив констант в порядке объявления, `valueOf(String)` ищет по имени и бросает `IllegalArgumentException`, `ordinal()` - номер, `name()` - имя. Для `enum` выражение `switch` с `->` без ветки `default` должно покрыть все константы, иначе не компилируется. Для карт и множеств с ключами-перечислениями есть быстрые `EnumMap` и `EnumSet`, которые итерируются в порядке объявления.

**Неизменяемость на практике.** Чтобы класс был неизменяемым: все поля `final`, класс `final` (или конструкторы закрыты), нет методов-мутаторов, а изменяемые компоненты копируются на входе и выходе. Слово `final` защищает *переменную от переприсваивания*, но не объект от изменения: `final List<String> xs` можно дополнять. Фабрики `List.of`, `Set.of`, `Map.of` (Java 9) создают **неизменяемые** коллекции, запрещающие `null` и бросающие `UnsupportedOperationException` при изменении. `Collections.unmodifiableList(xs)` - лишь **представление**: оно отражает последующие изменения исходного списка.

**Версии.** `record` - стандартная возможность с Java 16 (в 14 и 15 была предварительной). Перечисления есть с Java 5. `var` для локальных переменных - Java 10. Это проверяется параметром `javac --release N`.

<!-- section: examples -->
## Примеры

**Пример 1. Запись: поведение «из коробки» и проверка в компактном конструкторе.**

```java run
import java.util.HashSet;
import java.util.Set;

public class Main {
    record Point(int x, int y) {
        Point {
            if (x < 0 || y < 0) {
                throw new IllegalArgumentException("negative: " + x + "," + y);
            }
        }

        Point withX(int nx) {
            return new Point(nx, y);
        }

        double dist() {
            return Math.sqrt(x * x + y * y);
        }
    }

    public static void main(String[] args) {
        Point p = new Point(3, 4);
        Point q = new Point(3, 4);
        System.out.println(p);
        System.out.println(p == q);
        System.out.println(p.equals(q));
        System.out.println(p.hashCode() == q.hashCode());
        System.out.println(p.x() + " " + p.dist());
        System.out.println(p.withX(0) + " " + p);
        Set<Point> set = new HashSet<>();
        set.add(p);
        set.add(q);
        System.out.println(set.size());
        try {
            new Point(-1, 0);
        } catch (IllegalArgumentException e) {
            System.out.println(e.getMessage());
        }
    }
}
```
```output
Point[x=3, y=4]
false
true
true
3 5.0
Point[x=0, y=4] Point[x=3, y=4]
1
negative: -1,0
```

**Пример 2. Мутабельный ключ «теряется» в HashSet.**

```java run
import java.util.HashSet;
import java.util.Objects;
import java.util.Set;

public class Main {
    static class Key {
        int id;

        Key(int id) { this.id = id; }

        @Override
        public boolean equals(Object o) {
            return o instanceof Key k && k.id == id;
        }

        @Override
        public int hashCode() {
            return Objects.hash(id);
        }
    }

    public static void main(String[] args) {
        Set<Key> set = new HashSet<>();
        Key k = new Key(1);
        set.add(k);
        System.out.println(set.contains(k));
        k.id = 2;
        System.out.println(set.contains(k));
        System.out.println(set.size());
        System.out.println(set.iterator().next() == k);
        System.out.println(set.contains(new Key(1)));
    }
}
```
```output
true
false
1
true
false
```

Объект по-прежнему в множестве (размер 1, итератор его находит), но `contains` не видит его ни по новому, ни по старому значению. Тот же сценарий с записью-ключом невозможен: компоненты записи нельзя изменить.

**Пример 3. Перечисление с полями, телом констант и EnumMap.**

```java run
import java.util.EnumMap;
import java.util.Map;

public class Main {
    enum Level {
        LOW(1), MEDIUM(5) {
            @Override
            String label() { return "средний"; }
        }, HIGH(10);

        private final int weight;

        Level(int weight) { this.weight = weight; }

        int weight() { return weight; }

        String label() { return name().toLowerCase(); }
    }

    static String describe(Level l) {
        return switch (l) {
            case LOW -> "тихо";
            case MEDIUM -> "внимание";
            case HIGH -> "тревога";
        };
    }

    public static void main(String[] args) {
        for (Level l : Level.values()) {
            System.out.println(l + " " + l.ordinal() + " " + l.weight() + " " + l.label() + " " + describe(l));
        }
        System.out.println(Level.valueOf("HIGH") == Level.HIGH);
        System.out.println(Level.MEDIUM.getClass() == Level.class);
        System.out.println(Level.MEDIUM.getDeclaringClass() == Level.class);
        Map<Level, Integer> counts = new EnumMap<>(Level.class);
        counts.merge(Level.HIGH, 1, Integer::sum);
        counts.merge(Level.LOW, 2, Integer::sum);
        counts.merge(Level.HIGH, 1, Integer::sum);
        System.out.println(counts);
        try {
            Level.valueOf("CRITICAL");
        } catch (IllegalArgumentException e) {
            System.out.println(e.getMessage());
        }
    }
}
```
```output
LOW 0 1 low тихо
MEDIUM 1 5 средний внимание
HIGH 2 10 high тревога
true
false
true
{LOW=2, HIGH=2}
No enum constant Main.Level.CRITICAL
```

Константа `MEDIUM` с телом - анонимный подкласс `Level`, поэтому `getClass()` для неё не равно `Level.class`, зато `getDeclaringClass()` возвращает сам `enum`.

**Пример 4. Неглубокая неизменяемость записи и защитная копия.**

```java run
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;

public class Main {
    record Loose(List<String> tags) {}

    record Safe(List<String> tags) {
        Safe {
            tags = List.copyOf(tags);
        }
    }

    record WithArray(int[] data) {}

    public static void main(String[] args) {
        List<String> src = new ArrayList<>(List.of("a", "b"));
        Loose loose = new Loose(src);
        Safe safe = new Safe(src);
        src.add("c");
        System.out.println(loose.tags() + " " + safe.tags());
        try {
            safe.tags().add("x");
        } catch (UnsupportedOperationException e) {
            System.out.println("UnsupportedOperationException");
        }
        List<String> view = Collections.unmodifiableList(src);
        List<String> copy = List.copyOf(src);
        src.add("d");
        System.out.println(view.size() + " " + copy.size());
        System.out.println(new WithArray(new int[] {1}).equals(new WithArray(new int[] {1})));
        int[] shared = {1};
        System.out.println(new WithArray(shared).equals(new WithArray(shared)));
        System.out.println(Arrays.equals(new int[] {1}, new int[] {1}));
    }
}
```
```output
[a, b, c] [a, b]
UnsupportedOperationException
4 3
false
true
true
```

**Пример 5. Контракт equals: симметрия и наследование.**

```java run
public class Main {
    static class P {
        final int x;

        P(int x) { this.x = x; }

        @Override
        public boolean equals(Object o) {
            return o instanceof P p && p.x == x;
        }

        @Override
        public int hashCode() { return x; }
    }

    static class Q extends P {
        final String tag;

        Q(int x, String tag) { super(x); this.tag = tag; }

        @Override
        public boolean equals(Object o) {
            return o instanceof Q q && q.x == x && q.tag.equals(tag);
        }

        @Override
        public int hashCode() { return 31 * x + tag.hashCode(); }
    }

    public static void main(String[] args) {
        P p = new P(1);
        Q q = new Q(1, "t");
        System.out.println(p.equals(q));
        System.out.println(q.equals(p));
    }
}
```
```output
true
false
```

Результат несимметричен: `p.equals(q)` истинно, а `q.equals(p)` ложно. Это нарушение контракта; решения - запретить наследование (`final`, `record`) или сравнивать точные классы через `getClass()`.

<!-- section: mistakes -->
## Типичные ошибки

* **Переопределить `equals` и забыть `hashCode`.** Объекты, равные по `equals`, окажутся в разных корзинах, и `HashSet` будет хранить дубликаты.
* **Сигнатура `equals(Point p)` вместо `equals(Object o)`.** Это перегрузка, а не переопределение; `HashSet` её не вызовет. Аннотация `@Override` ловит такую ошибку при компиляции.
* **Изменять поле, входящее в `hashCode`, у объекта внутри `HashMap` или `HashSet`.** Объект «потеряется». Ключи делайте неизменяемыми.
* **Думать, что `record` неизменяем «насквозь».** Компонент-список остаётся изменяемым, массив сравнивается по ссылке. Копируйте списки через `List.copyOf` в компактном конструкторе.
* **Путать `final` и неизменяемость.** `final` запрещает переприсвоение переменной, а не изменение объекта.
* **Сравнивать строковое имя константы вместо самой константы.** Перечисления сравнивают через `==`; `valueOf` с неизвестным именем бросает исключение, поэтому ввод пользователя разбирают в `try/catch`.
* **Использовать `ordinal()` как постоянный идентификатор.** Порядок констант изменится при вставке новой константы; для сохранения используйте `name()` или явное поле.
* **Ждать от `Collections.unmodifiableList` копии.** Это лишь представление: изменения исходного списка видны через него.

<!-- section: summary -->
## Итоги

* Класс по умолчанию равен только самому себе; `equals` и `hashCode` переопределяют только вместе и по неизменяемым полям.
* `record` (Java 16) даёт конструктор, доступ, `equals`, `hashCode` и `toString`; компактный конструктор проверяет данные, но глубокую неизменяемость нужно обеспечивать копированием.
* `enum` - закрытое множество констант с полями и методами; `switch` по перечислению проверяется на полноту.
* `List.of`, `Set.of`, `Map.of` и `List.copyOf` дают неизменяемые коллекции, `Collections.unmodifiableList` - только представление.

Дальше: [интерфейсы, наследование и sealed-типы](lesson:java.fundamentals.interfaces-sealed-patterns).
Назад: [JVM, байткод, типы и память](lesson:java.fundamentals.jvm-types-and-memory).
Сравните: [классы, записи и структуры в C#](lesson:csharp.fundamentals.classes-records-structs), [структуры и методы в Go](lesson:go.fundamentals.structs-methods-embedding).
