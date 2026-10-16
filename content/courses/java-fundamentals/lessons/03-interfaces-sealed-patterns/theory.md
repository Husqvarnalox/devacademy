<!-- section: simple -->
## Интерфейсы, наследование и закрытые иерархии

Три инструмента задают, *какие бывают объекты* и *что с ними можно делать*:

* **Интерфейс** (`interface`) описывает контракт: набор методов, которые обязан поддерживать любой реализующий класс. Класс может реализовать сколько угодно интерфейсов. Начиная с Java 8 интерфейс может содержать готовые реализации (`default`-методы) и статические методы.
* **Наследование** (`extends`) - класс может расширить ровно один класс и унаследовать его поля и методы. Метод подкласса *переопределяет* метод родителя, и при вызове через переменную родительского типа выполняется версия **реального** класса объекта (динамическая диспетчеризация).
* **Закрытые типы** (`sealed`, Java 17) - вы сами перечисляете, кто имеет право реализовать интерфейс или расширить класс. Раз все варианты известны, компилятор умеет проверить, что `switch` разобрал **каждый** из них.

А **сопоставление с образцом** (pattern matching) позволяет проверить тип и тут же вытащить значения: вместо цепочки `if (x instanceof A) { A a = (A) x; ... }` вы пишете `if (x instanceof A a)` или раскладываете запись прямо в `switch`.

Вместе это даёт в Java стиль, знакомый по алгебраическим типам данных: «сумма» вариантов - `sealed interface`, каждый вариант - `record`, обработка - `switch` без `default`.

<!-- section: precise -->
## Точные правила

**Интерфейсы.** Методы без тела неявно `public abstract`. `default`-метод имеет тело и может быть переопределён; `static`-метод принадлежит интерфейсу; `private`-метод (Java 9) нужен для общего кода внутри интерфейса. Поля интерфейса - неявно `public static final`. Интерфейс с одним абстрактным методом называют *функциональным*, его можно реализовать лямбдой. Если класс наследует два `default`-метода с одной сигнатурой, он обязан переопределить метод и может вызвать конкретную версию: `Greeter.super.greet()`.

**Абстрактные классы и наследование.** `abstract class` может иметь состояние, конструкторы и абстрактные методы. Класс расширяет только один класс, но реализует много интерфейсов. Переопределение выбирается по **реальному типу объекта** в момент выполнения, а **перегрузка** (выбор из методов с одним именем) - по **статическим типам аргументов** на этапе компиляции. Поля **не** переопределяются, а скрываются: `b.name` зависит от типа переменной. Конструктор родителя выполняется до инициализации полей подкласса, поэтому вызов переопределяемого метода из конструктора увидит поля в состоянии «по умолчанию» (`0`, `null`). Аннотация `@Override` заставляет компилятор проверить, что вы действительно переопределили метод.

**Sealed-типы (Java 17).** `sealed interface Shape permits Circle, Rect, Group {}`. Каждый разрешённый подтип обязан быть `final` (записи неявно `final`), `sealed` или `non-sealed`. Если подтипы объявлены в том же файле, `permits` можно опустить. Рефлексия: `Shape.class.isSealed()`, `getPermittedSubclasses()`.

**Сопоставление с образцом.**

* `instanceof` с образцом (Java 16): `if (o instanceof String s && s.length() > 3)`. Переменная `s` видна там, где компилятор докажет успех проверки; после `if (!(o instanceof Integer n)) return;` переменная `n` доступна до конца блока.
* `switch` с образцами (Java 21): `case Circle c ->`, `case Rect r when r.w() == r.h() ->` (охранное условие `when`), `case null ->`. Ветки проверяются сверху вниз; ветка, которую перекрывает более общая выше, - ошибка компиляции («доминирование»). Если селектор `null` и ветки `case null` нет, выбрасывается `NullPointerException`.
* **Образцы записей** (Java 21): `case Rect(double w, double h)` раскладывает запись на компоненты, образцы могут вкладываться: `case Group(Circle c, Rect(double w, var h))`.
* **Полнота.** `switch`-выражение обязано быть исчерпывающим. Для sealed-типа достаточно перечислить все подтипы - ветка `default` не нужна, и при добавлении нового подтипа компилятор покажет все `switch`, которые надо дополнить. Именно ради этого `default` в таком `switch` не пишут.

**Версии (проверено компиляцией с `--release N`).** `instanceof` с образцом компилируется с `--release 16`, но не с 15; `sealed` - с 17, но не с 16; `switch` с образцами и образцы записей - с 21, но не с 20 (в 20 они были предварительными).

<!-- section: examples -->
## Примеры

**Пример 1. Интерфейсы, `default`-методы и динамическая диспетчеризация.**

```java run
public class Main {
    interface Greeter {
        String name();

        default String greet() {
            return prefix() + name();
        }

        private String prefix() {
            return "Привет, ";
        }

        static Greeter of(String n) {
            return () -> n;
        }
    }

    interface Loud {
        default String greet() {
            return "ЭЙ!";
        }
    }

    static class Both implements Greeter, Loud {
        public String name() { return "Both"; }

        public String greet() {
            return Greeter.super.greet() + " / " + Loud.super.greet();
        }
    }

    static abstract class Animal {
        abstract String sound();

        String speak() {
            return getClass().getSimpleName() + ": " + sound();
        }
    }

    static class Dog extends Animal {
        String sound() { return "гав"; }
    }

    static class Puppy extends Dog {
        @Override
        String sound() { return super.sound() + "!"; }
    }

    public static void main(String[] args) {
        System.out.println(Greeter.of("мир").greet());
        System.out.println(new Both().greet());
        Animal a = new Puppy();
        System.out.println(a.speak());
        System.out.println((a instanceof Dog) + " " + (a instanceof Puppy));
    }
}
```
```output
Привет, мир
Привет, Both / ЭЙ!
Puppy: гав!
true true
```

**Пример 2. Перегрузка по статическому типу, скрытие полей, конструктор и переопределяемый метод.**

```java run
public class Main {
    static class Base {
        String name = "base";

        Base() { init(); }

        void init() { System.out.println("Base.init"); }

        String hello(Object o) { return "Base.Object"; }

        String hello(String s) { return "Base.String"; }
    }

    static class Derived extends Base {
        String name = "derived";
        int value = 42;

        @Override
        void init() { System.out.println("Derived.init value=" + value); }

        @Override
        String hello(Object o) { return "Derived.Object"; }
    }

    public static void main(String[] args) {
        Base b = new Derived();
        System.out.println(b.name + " " + ((Derived) b).name);
        Object o = "str";
        System.out.println(b.hello(o) + " " + b.hello("s"));
        new Derived().init();
    }
}
```
```output
Derived.init value=0
base derived
Derived.Object Base.String
Derived.init value=0
Derived.init value=42
```

Конструктор `Base` вызвал `Derived.init`, когда поле `value` ещё не получило значение `42`. `b.hello(o)` выбрал перегрузку `hello(Object)` по статическому типу `Object`, а её реализация взята у `Derived`.

**Пример 3. Закрытая иерархия, `switch` без `default`, охранные условия и `null`.**

```java run
import java.util.Locale;

public class Main {
    sealed interface Shape permits Circle, Rect, Group {}

    record Circle(double r) implements Shape {}

    record Rect(double w, double h) implements Shape {}

    record Group(Shape a, Shape b) implements Shape {}

    static double area(Shape s) {
        return switch (s) {
            case Circle c -> Math.PI * c.r() * c.r();
            case Rect(double w, double h) -> w * h;
            case Group(Shape a, Shape b) -> area(a) + area(b);
        };
    }

    static String kind(Object o) {
        return switch (o) {
            case null -> "null";
            case Rect(double w, double h) when w == h -> "квадрат " + w;
            case Rect r -> "прямоугольник";
            case Circle c when c.r() > 10 -> "большой круг";
            case Circle c -> "круг";
            case Integer i when i > 100 -> "большое целое";
            case Integer i -> "целое " + i;
            case String s -> "строка длины " + s.length();
            default -> "другое";
        };
    }

    public static void main(String[] args) {
        Shape g = new Group(new Rect(2, 3), new Group(new Circle(1), new Rect(1, 1)));
        System.out.println(String.format(Locale.ROOT, "%.3f", area(g)));
        Object[] items = {new Rect(2, 2), new Rect(2, 3), new Circle(11), new Circle(1), 7, 700, "привет", null, 2.5};
        for (Object o : items) {
            System.out.println(kind(o));
        }
    }
}
```
```output
10.142
квадрат 2.0
прямоугольник
большой круг
круг
целое 7
большое целое
строка длины 6
null
другое
```

**Пример 4. `instanceof` с образцом и область видимости переменной.**

```java run
public class Main {
    static String old(Object o) {
        if (o instanceof String) {
            String s = (String) o;
            return s.toUpperCase();
        }
        return "?";
    }

    static String modern(Object o) {
        if (o instanceof String s && !s.isEmpty()) {
            return s.toUpperCase();
        }
        if (!(o instanceof Integer n)) {
            return "?";
        }
        return "int " + (n + 1);
    }

    public static void main(String[] args) {
        System.out.println(old("abc") + " " + old(5));
        System.out.println(modern("abc") + " | " + modern("") + " | " + modern(41) + " | " + modern(null));
    }
}
```
```output
ABC ?
ABC | ? | int 42 | ?
```

**Пример 5. Sealed-тип можно исследовать рефлексией.**

```java run
import java.util.Arrays;

public class Main {
    sealed interface Result permits Ok, Err {}

    record Ok(int value) implements Result {}

    record Err(String message) implements Result {}

    public static void main(String[] args) {
        System.out.println(Result.class.isSealed());
        System.out.println(Arrays.stream(Result.class.getPermittedSubclasses()).map(Class::getSimpleName).sorted().toList());
        Result r = new Err("нет данных");
        String text = switch (r) {
            case Ok(int v) -> "значение " + v;
            case Err(String m) -> "ошибка: " + m;
        };
        System.out.println(text);
    }
}
```
```output
true
[Err, Ok]
ошибка: нет данных
```

<!-- section: mistakes -->
## Типичные ошибки

* **Добавлять `default` в `switch` по sealed-типу.** Ветка `default` отключает проверку полноты: при добавлении нового подтипа компилятор промолчит, а программа попадёт в `default`.
* **Ставить общий образец выше частного.** `case Rect r` перед `case Rect(double w, double h) when w == h` делает вторую ветку недостижимой, и компилятор сообщит об ошибке.
* **Ждать, что переменная образца видна везде.** `if (o instanceof String s || s.isEmpty())` не компилируется: при `||` успех проверки не гарантирован.
* **Вызывать переопределяемые методы из конструктора.** Подкласс будет работать с полями до их инициализации.
* **Путать перегрузку и переопределение.** `b.hello(o)` выбирает метод по статическому типу `Object`, а не по реальному типу строки.
* **Забыть `@Override`.** Опечатка в сигнатуре создаёт новый метод, а не переопределяет старый, и ошибка проявится только в поведении.
* **Наследоваться ради повторного использования кода.** Если связь «является» неочевидна, предпочитайте композицию и интерфейс; закрытые иерархии заменяют многие случаи хрупкого наследования.
* **Забыть, что `switch` по `null` бросает `NullPointerException`.** Без ветки `case null` значение `null` в селекторе вызывает исключение.

<!-- section: summary -->
## Итоги

* Интерфейс - контракт (с `default`-методами с Java 8); класс наследует один класс и реализует много интерфейсов; переопределение выбирается по реальному типу, перегрузка - по статическому.
* `sealed` (Java 17) закрывает множество подтипов; вместе с `record` и `switch` с образцами (Java 21) даёт исчерпывающую обработку без `default`.
* `instanceof` с образцом (Java 16) и образцы записей (Java 21) заменяют приведения типов и ручное извлечение компонентов.
* Версии проверены компиляцией с `--release 16`, `17`, `20` и `21`.

Дальше: [обобщения и коллекции](lesson:java.fundamentals.generics-collections).
Назад: [классы, записи и перечисления](lesson:java.fundamentals.classes-records-enums).
Сравните: [интерфейсы и наследование в C#](lesson:csharp.fundamentals.interfaces-inheritance-polymorphism), [интерфейсы в Go](lesson:go.fundamentals.interfaces), [перечисления и сопоставление с образцом в Rust](lesson:rust.fundamentals.structs-enums-pattern-matching).
