<!-- section: simple -->
## Зачем этот урок

Если вы знаете исключения и классы из другого языка, то `try/catch`, `throw`, поля и конструкторы в Java прочитаете сразу. Отличия начинаются глубже: исключения бывают **проверяемыми** (компилятор требует их обработать) и непроверяемыми; `finally` выполняется даже после `return`; порядок блоков `catch` важен; поля класса получают значения по умолчанию, а локальные переменные - нет; два объекта с одинаковыми полями по умолчанию **не равны**; внутренний класс без слова `static` требует объекта внешнего класса. Урок собирает эти места в одном месте. Успешное прохождение (упражнение и квиз) засчитывает уроки 22-23 курса «Java с нуля».

## Главная мысль

В Java исключение - это объект, а класс - тип, ссылка на который хранит переменная. Из этого следует почти всё: исключение можно перехватить по типу, создать своё и передать причину; переменная класса хранит ссылку, `==` сравнивает ссылки, а значения полей приходится описывать самому - в конструкторе, `toString` и `equals`.

<!-- section: examples -->
## Пример 1. Исключения: try, catch, finally

```java run
public class Main {
    static int f() {
        try {
            return 1;
        } finally {
            System.out.println("finally");
        }
    }

    static int parse(String text) {
        try {
            return Integer.parseInt(text.trim());
        } catch (NumberFormatException e) {
            throw new IllegalArgumentException("не число: [" + text + "]", e);
        }
    }

    public static void main(String[] args) {
        System.out.println(f());
        for (String t : new String[] {"15", " 7 ", "+9", "-3", "4.5", ""}) {
            try {
                System.out.println(parse(t));
            } catch (IllegalArgumentException e) {
                System.out.println(e.getMessage() + " (причина: " + e.getCause().getClass().getSimpleName() + ")");
            }
        }
        try {
            Integer.parseInt(" 42");
        } catch (NumberFormatException | NullPointerException e) {
            System.out.println(e.getMessage());
        }
        try {
            Object o = "text";
            Integer n = (Integer) o;
        } catch (ClassCastException e) {
            System.out.println("ClassCastException");
        }
        String s = null;
        try {
            System.out.println(s.length());
        } catch (NullPointerException e) {
            System.out.println("NullPointerException");
        }
    }
}
```
```output
finally
1
15
7
9
-3
не число: [4.5] (причина: NumberFormatException)
не число: [] (причина: NumberFormatException)
For input string: " 42"
ClassCastException
NullPointerException
```

Разбор:

* `finally` выполняется всегда - после нормального завершения, после `return` и после перехваченного или не перехваченного исключения. Поэтому там освобождают ресурсы (позже для этого есть `try-with-resources`).
* `Integer.parseInt` не обрезает пробелы (`" 42"` - ошибка), принимает знак `+` и `-` и не принимает дроби и пустую строку. Для `null` сообщение другое, а тип тот же.
* Конструктор исключения принимает **причину** вторым аргументом: исходное исключение сохраняется и доступно через `getCause()`.
* Несколько типов в одном `catch` разделяют вертикальной чертой. Сначала пишут более частные типы, потом общие.
* `ClassCastException` и `NullPointerException` - ошибки программиста, их обычно не перехватывают, а исправляют. Перехват здесь только для демонстрации.

## Пример 2. Проверяемые и непроверяемые исключения

```java run
public class Main {
    static void check(int n) throws Exception {
        if (n < 0) {
            throw new Exception("проверяемое: " + n);
        }
    }

    static void validate(int n) {
        if (n < 0) {
            throw new IllegalArgumentException("непроверяемое: " + n);
        }
    }

    public static void main(String[] args) {
        validate(5);
        try {
            check(-1);
        } catch (Exception e) {
            System.out.println(e.getMessage());
        }
        try {
            validate(-2);
        } catch (RuntimeException e) {
            System.out.println(e.getMessage() + " " + (e instanceof IllegalArgumentException));
        }
        System.out.println(10 / 4 + " " + 10.0 / 0);
        try {
            System.out.println(10 / (args.length));
        } catch (ArithmeticException e) {
            System.out.println(e.getMessage());
        }
    }
}
```
```output
проверяемое: -1
непроверяемое: -2 true
2 Infinity
/ by zero
```

Разбор:

* Исключения, наследующие `Exception`, но не `RuntimeException`, - **проверяемые**. Метод, который может такое бросить, обязан либо перехватить его, либо объявить `throws`; иначе программа не скомпилируется. Классы `RuntimeException` и `Error` - **непроверяемые**: обрабатывать их не обязывают.
* Для неверных данных от пользователя и неверных аргументов обычно бросают непроверяемые `IllegalArgumentException` и `IllegalStateException`. Проверяемые используют для сбоев внешнего мира (файл, сеть), где вызывающий код должен выбрать реакцию. Подробно - в курсе `java-fundamentals`.
* Целое деление на ноль бросает `ArithmeticException`; дробное даёт `Infinity` (не исключение).
* Блок `catch (Exception e)` перехватывает почти всё, включая ошибки программиста; ставьте его только там, где это действительно нужно.

## Пример 3. Класс, ссылки и равенство

```java run
class Item {
    private final String name;
    private int qty;

    Item(String name, int qty) {
        this.name = name;
        this.qty = qty;
    }

    void add(int k) {
        qty += k;
    }

    @Override
    public String toString() {
        return name + " x" + qty;
    }
}

class Counter {
    int count;
    String label;
}

record Pair(int a, int b) { }

public class Main {
    public static void main(String[] args) {
        Item a = new Item("pen", 2);
        Item b = new Item("pen", 2);
        Item c = a;
        c.add(3);
        System.out.println(a + " | " + b + " | " + (a == c) + " " + (a == b) + " " + a.equals(b));
        Counter counter = new Counter();
        System.out.println(counter.count + " " + counter.label);
        Item none = null;
        try {
            none.add(1);
        } catch (NullPointerException e) {
            System.out.println("NullPointerException");
        }
        System.out.println(new Pair(1, 2) + " " + new Pair(1, 2).equals(new Pair(1, 2)));
    }
}
```
```output
pen x5 | pen x2 | true false false
0 null
NullPointerException
Pair[a=1, b=2] true
```

Разбор:

* Файл `Main.java` может содержать несколько классов; публичным (с `public`) должен быть только `Main`, остальные пишут без модификатора.
* Поля `private` скрыты от других классов; доступ даёт метод. `final` значит «присваивается один раз» - в конструкторе или при объявлении.
* `this.name = name` отличает поле от параметра с тем же именем.
* `c = a` копирует ссылку, а не объект: `add` через `c` меняет тот же объект, что виден через `a`. `a == b` ложно, хотя поля одинаковы, и `a.equals(b)` тоже ложно, потому что стандартный `equals` сравнивает ссылки. Свою логику равенства задают, переопределяя `equals` и `hashCode`.
* Без `toString` печать объекта даёт имя класса и хэш вроде `Item@1b6d3586` (значение меняется между запусками). Метод `toString` с `@Override` решает это.
* Поля получают значения по умолчанию (`0`, `false`, `null`); вызов метода у `null` - `NullPointerException`.
* `record` (Java 16+) - краткая запись класса-данных: конструктор, `toString`, `equals` и `hashCode` создаются сами. Подробно - в курсе `java-fundamentals`.

**Попробуйте.** Уберите `toString` из `Item` и посмотрите, что напечатает `println(a)`; добавьте в `Item` поле `final int id;` без присваивания и прочитайте сообщение компилятора.

<!-- section: precise -->
* Иерархия: `Throwable` делится на `Error` (сбои JVM, их не перехватывают) и `Exception`. Подкласс `RuntimeException` - непроверяемый, остальные потомки `Exception` - проверяемые.
* `try { ... } catch (A | B e) { ... } finally { ... }`. Блок `finally` выполняется при любом выходе из `try`; `return` в `finally` перекрывает исключение и значение - так не пишут.
* Порядок `catch`: сначала частные типы, потом общие; обратный порядок - ошибка компиляции.
* `throw` бросает объект исключения; конструктор `new Exception(message, cause)` сохраняет причину, `getMessage()` и `getCause()` читают её. Собственное исключение - подкласс `RuntimeException` или `Exception` (подробно - в `java-fundamentals`).
* Метод, который может бросить проверяемое исключение, объявляет `throws`; вызывающий код перехватывает его или тоже объявляет `throws`. `main` может объявить `throws Exception`.
* `Integer.parseInt` и `Double.parseDouble` бросают `NumberFormatException` (подкласс `IllegalArgumentException`); пробелы вокруг числа не допускаются.
* Класс объявляет поля, конструкторы и методы. Конструктор носит имя класса и не имеет типа результата; если конструкторов нет, компилятор создаёт пустой без параметров. Если вы объявили свой, пустой больше не создаётся.
* Модификаторы доступа: `private` (только класс), без модификатора (пакет), `protected`, `public`. Поля обычно закрывают, а доступ даёт метод.
* `this` - ссылка на текущий объект; `static`-поле и `static`-метод принадлежат классу и не требуют объекта. Внутри `static`-метода нет `this`.
* Вложенный класс с `static` не связан с объектом внешнего класса; без `static` (внутренний) каждый его объект хранит ссылку на объект внешнего и создаётся только через него.
* Класс по умолчанию наследует `Object`: `equals` и `hashCode` сравнивают ссылки, `toString` печатает `Класс@хэш`. Переменные класса хранят ссылки; `null` означает «объекта нет».

<!-- section: mistakes -->
**1. Проверяемое исключение не обработано.** `Thread.sleep` объявляет `InterruptedException`:

```text
Main.java:3: error: unreported exception InterruptedException; must be caught or declared to be thrown
        Thread.sleep(10);
                    ^
```

Нужно обернуть вызов в `try/catch` или добавить `throws InterruptedException` в заголовок метода.

**2. Общий catch перед частным.** Второй блок недостижим:

```text
Main.java:4: error: exception NumberFormatException has already been caught
```

Переставьте блоки: частный тип первым.

**3. Внутренний класс без static в static-методе.** `class Inner { }` внутри `Main` и `new Inner()` из `main`:

```text
Main.java:6: error: non-static variable this cannot be referenced from a static context
        Inner i = new Inner();
                  ^
```

Внутреннему классу нужен объект `Main`. Если связи с внешним объектом нет, объявите `static class Inner`.

**4. Неверные аргументы конструктора.** Конструктор `Item(String, int)` вызван с одним аргументом:

```text
Main.java:7: error: constructor Item in class Item cannot be applied to given types;
  required: String,int
  found:    String
  reason: actual and formal argument lists differ in length
```

**5. Закрытое поле.** Чтение `j.name` вне класса:

```text
Main.java:9: error: name has private access in Item
```

Добавьте метод доступа или `toString`.

**6. Final-поле без значения.** Конструктор не присвоил `name`:

```text
Main.java:7: error: variable name might not have been initialized
```

**7. Ошибки, которые видны только при запуске.** `NullPointerException` при вызове метода у поля `null`; `NumberFormatException` для строки с пробелом или десятичной точкой; `catch (Exception e)`, который проглатывает ошибку программиста и скрывает проблему; `==` вместо `equals` для объектов; бросок исключения без сообщения. Всегда пишите сообщение, по которому можно найти причину: что получено и что ожидалось.

<!-- section: summary -->
Теперь вы знаете, как устроены исключения и первые классы в Java: проверяемые и непроверяемые исключения, `finally`, порядок `catch`, причина исключения; поля, конструктор, `this`, значения по умолчанию, ссылочное равенство, `toString` и вложенные классы. Следующий шаг - курс первых шагов о том, как Java выполняется: [Как выполняется Java: javac, байткод и JVM](lesson:java.first-steps.how-java-runs). Для подробностей по темам: [Исключения](lesson:java.from-zero.exceptions), [Первый класс](lesson:java.from-zero.first-class).

<!-- section: programmer -->
## Главное для тех, кто уже программирует

* Исключение - объект; перехват по типу. Проверяемых исключений нет в Python, JavaScript, C# и Go: в Java компилятор требует `catch` или `throws` для `Exception` и его потомков, кроме `RuntimeException`.
* `finally` срабатывает и после `return`; `catch` перехватывает по иерархии, поэтому частные типы идут первыми; несколько типов - через `|`.
* Причина исключения передаётся вторым аргументом конструктора (`getCause()`), аналог `raise ... from` в Python.
* `Integer.parseInt` не обрезает пробелы и бросает исключение (в Python `int(" 42")` работает, в JavaScript `parseInt` читает префикс). Для проверки данных обычно бросают `IllegalArgumentException`.
* Класс: поля, конструкторы, методы; нет свойств в стиле Python/C# - только методы доступа. Поля `private` по соглашению, `final` - присваивание один раз.
* Значения по умолчанию у полей (`0`, `false`, `null`); у локальных переменных их нет.
* `==` сравнивает ссылки; стандартный `equals` тоже; для значений - переопределённый `equals`/`hashCode` или `record`.
* `toString()` играет роль `__str__`/`ToString()`; без него печатается `Класс@хэш`.
* Вложенный класс без `static` связан с объектом внешнего класса (в C# вложенные классы такой связи не имеют); `static class` - независим.
* Несколько классов в одном файле допустимы, `public` - только у `Main`. Записи (`record`) покрывают большинство классов-данных; подробно - в `java-fundamentals`.

<!-- section: deep-dive -->
## Почему исключения бывают проверяемыми

Идея проверяемых исключений - сделать сбои частью контракта метода: компилятор не даст забыть про ошибку чтения файла. На практике они усложняют лямбды и потоки, поэтому современные библиотеки чаще используют непроверяемые исключения. В `java-fundamentals` разобраны оба подхода, `try-with-resources` и подавленные исключения.
