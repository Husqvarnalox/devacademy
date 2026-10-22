<!-- section: simple -->
## Зачем нужны классы

До сих пор данные об одной вещи хранились в нескольких переменных: название, цена, количество. Когда вещей десятки, такие переменные легко перепутать, а отдельные методы приходится снабжать длинными списками параметров. **Класс** позволяет описать «вещь» один раз: какие данные у неё есть и что с ней можно делать. Затем из описания создают сколько угодно **объектов** - конкретных вещей.

Вы уже работали с готовыми классами: `String`, `Scanner`, `ArrayList`, `HashMap`. Теперь вы напишете свой.

## Одна идея: класс - чертёж, объект - то, что по нему создано

Класс описывает три вещи:

* **поля** - данные, которые есть у каждого объекта (переменные внутри класса);
* **конструктор** - особый блок, который задаёт начальные значения полей при создании объекта;
* **методы** - действия, которые объект умеет выполнять над своими данными.

```java
class Product {
    String name;
    int price;

    Product(String name, int price) {
        this.name = name;
        this.price = price;
    }

    String describe() {
        return name + ": " + price + " руб.";
    }
}
```

Объект создают словом `new`, которое вызывает конструктор:

```java
Product tea = new Product("Чай", 120);
System.out.println(tea.describe());
```

Через точку обращаются к полям и методам конкретного объекта: `tea.name`, `tea.describe()`. Слово `this` внутри класса означает «текущий объект, с которым работает метод». Запись `this.name = name` читается так: «в поле `name` этого объекта положить значение параметра `name`».

Несколько классов могут лежать в одном файле `Main.java`. Публичным (`public`) может быть только класс `Main`, чьё имя совпадает с именем файла. Остальные классы записывают без слова `public`, как в примерах ниже.

<!-- section: examples -->
## Пример 1. Класс с полями

```java run
class Product {
    String name;
    int price;
}

public class Main {
    public static void main(String[] args) {
        Product tea = new Product();
        tea.name = "Чай";
        tea.price = 120;

        Product coffee = new Product();
        coffee.name = "Кофе";
        coffee.price = 300;

        System.out.println(tea.name + " - " + tea.price);
        System.out.println(coffee.name + " - " + coffee.price);
        tea.price = 130;
        System.out.println(tea.price + " и " + coffee.price);
    }
}
```
```output
Чай - 120
Кофе - 300
130 и 300
```

Разбор:

* `new Product()` создаёт новый объект; у него свои копии полей `name` и `price`.
* Поля чая и кофе независимы: изменение `tea.price` не затронуло `coffee.price`.
* Пока поля заполняются вручную, легко забыть одно из них. Для этого нужен конструктор.

## Пример 2. Конструктор и значения по умолчанию

```java run
class Product {
    String name;
    int price;

    Product(String name, int price) {
        this.name = name;
        this.price = price;
    }
}

class Empty {
    String label;
    int count;
    boolean ready;
}

public class Main {
    public static void main(String[] args) {
        Product tea = new Product("Чай", 120);
        System.out.println(tea.name + " " + tea.price);

        Empty e = new Empty();
        System.out.println(e.label);
        System.out.println(e.count);
        System.out.println(e.ready);
    }
}
```
```output
Чай 120
null
0
false
```

Разбор:

* Конструктор называется так же, как класс, и не имеет типа возвращаемого значения (даже `void`). Вызов `new Product("Чай", 120)` передаёт в него значения.
* Внутри конструктора имена параметров совпадают с именами полей. `this.name` - поле объекта, просто `name` - параметр. Без `this` параметр заслонил бы поле.
* Если поле не заполнить, оно получает значение по умолчанию: `0` для чисел, `false` для `boolean`, `null` («нет объекта») для ссылок вроде `String`.

## Пример 3. Методы объекта

```java run
class Product {
    String name;
    int price;
    int quantity;

    Product(String name, int price, int quantity) {
        this.name = name;
        this.price = price;
        this.quantity = quantity;
    }

    int total() {
        return price * quantity;
    }

    void sell(int count) {
        if (count > 0 && count <= quantity) {
            quantity -= count;
        }
    }

    String describe() {
        return name + ": " + quantity + " шт., на " + total() + " руб.";
    }
}

public class Main {
    public static void main(String[] args) {
        Product tea = new Product("Чай", 120, 5);
        System.out.println(tea.describe());
        tea.sell(2);
        System.out.println(tea.describe());
        tea.sell(10);
        System.out.println(tea.describe());
    }
}
```
```output
Чай: 5 шт., на 600 руб.
Чай: 3 шт., на 360 руб.
Чай: 3 шт., на 360 руб.
```

Разбор:

* Методы без слова `static` вызываются у объекта и сами видят его поля. В `total()` имена `price` и `quantity` означают поля того объекта, у которого вызван метод.
* `sell` изменяет поле `quantity` этого объекта. Продажа 10 штук не выполнилась, потому что в наличии осталось 3.
* Метод может вызывать другие методы того же объекта: `describe()` вызывает `total()`.

## Пример 4. Несколько объектов и список

```java run
import java.util.ArrayList;

class Product {
    String name;
    int price;

    Product(String name, int price) {
        this.name = name;
        this.price = price;
    }
}

public class Main {
    public static void main(String[] args) {
        ArrayList<Product> cart = new ArrayList<>();
        cart.add(new Product("Чай", 120));
        cart.add(new Product("Кофе", 300));
        cart.add(new Product("Сахар", 80));

        int sum = 0;
        for (Product p : cart) {
            sum += p.price;
            System.out.println(p.name);
        }
        System.out.println("Итого: " + sum);
    }
}
```
```output
Чай
Кофе
Сахар
Итого: 500
```

Разбор:

* Класс можно использовать как тип элементов списка: `ArrayList<Product>`. Каждый элемент - отдельный объект.
* Цикл for-each выдаёт объекты по одному; через точку берутся их поля.

## Пример 5. Объект передаётся по ссылке

```java run
class Counter {
    int value;
}

public class Main {
    static void addTen(Counter c) {
        c.value += 10;
    }

    public static void main(String[] args) {
        Counter a = new Counter();
        addTen(a);
        System.out.println(a.value);

        Counter b = a;
        b.value = 99;
        System.out.println(a.value);
    }
}
```
```output
10
99
```

Разбор:

* В переменной `a` хранится не сам объект, а **ссылка** на него. Метод `addTen` получил копию ссылки и изменил тот же объект: `a.value` стало равно 10.
* Присваивание `Counter b = a;` не создаёт второй объект: обе переменные указывают на один и тот же. Поэтому изменение через `b` видно через `a`. Так же ведут себя массивы и списки.

**Попробуйте.** В примере 3 добавьте методу `describe` слово «закончился», когда `quantity` равно 0. Затем создайте второй объект `sugar` и проверьте, что продажа чая не меняет сахар.

<!-- section: precise -->
## Точные правила

* Объявление: `class Имя { поля; конструкторы; методы }`. Имена классов принято писать с большой буквы.
* Поле - переменная, объявленная в теле класса вне методов. У каждого объекта своя копия. Значения по умолчанию: `0` и `0.0` для чисел, `false` для `boolean`, `'\u0000'` для `char`, `null` для ссылочных типов.
* Конструктор: имя совпадает с именем класса, возвращаемого типа нет. Он выполняется при `new`. Можно объявить несколько конструкторов с разными списками параметров.
* Если конструктор не объявлен, компилятор добавляет пустой конструктор без параметров. Как только вы объявили любой свой конструктор, пустой больше не добавляется: `new Product()` перестаёт компилироваться.
* `new Имя(аргументы)` создаёт объект и возвращает ссылку на него.
* Метод без `static` вызывается у объекта: `объект.метод()`. Внутри метода поля текущего объекта доступны по имени.
* `this` - ссылка на текущий объект. Он нужен, когда имя параметра совпадает с именем поля (`this.name = name`); в остальных случаях его можно опустить.
* Метод с `static` (как `main`) принадлежит классу, а не объекту; у него нет `this`, и вызвать из него метод объекта без объекта нельзя.
* Переменная объектного типа хранит ссылку. `null` означает «ссылки на объект нет»; обращение через точку к `null` бросает `NullPointerException`.
* Присваивание `b = a` копирует ссылку, а не объект. При передаче в метод также копируется ссылка, поэтому метод может изменить поля объекта, но не может подменить переменную вызывающего кода.
* В одном файле может быть несколько классов, но `public` допускается только у класса, имя которого совпадает с именем файла.
* В этом уроке поля и методы записаны без модификаторов доступа. Ограничение доступа (`private`) и методы чтения полей рассматриваются в курсе `java-first-steps` и далее.
* Без собственного метода `toString` вывод объекта через `println` показывает имя класса и служебное число (вида `Product@1b6d3586`), а не его поля. Поэтому в примерах выводят поля или результат метода `describe()`.

<!-- section: mistakes -->
## Типичные ошибки

**1. Забыт `new`.**

```java
class Product {
    String name;
    int price;

    Product(String name, int price) {
        this.name = name;
        this.price = price;
    }
}

public class Main {
    public static void main(String[] args) {
        Product tea = Product("Чай", 120);
        System.out.println(tea.name);
    }
}
```

```text
Main.java:13: error: cannot find symbol
        Product tea = Product("Чай", 120);
                      ^
  symbol:   method Product(String,int)
  location: class Main
1 error
```

Без `new` Java считает `Product(...)` вызовом обычного метода и не находит такого. Объект создают только через `new Product(...)`.

**2. Неверные аргументы конструктора.**

```java
class Product {
    String name;
    int price;

    Product(String name, int price) {
        this.name = name;
        this.price = price;
    }
}

public class Main {
    public static void main(String[] args) {
        Product tea = new Product("Чай");
    }
}
```

```text
Main.java:13: error: constructor Product in class Product cannot be applied to given types;
        Product tea = new Product("Чай");
                      ^
  required: String,int
  found:    String
  reason: actual and formal argument lists differ in length
1 error
```

Конструктор требует два аргумента, передан один. Сообщение показывает, что требуется (`required`) и что передано (`found`).

**3. Пропущен `this` в конструкторе.**

```java run
class Product {
    String name;
    int price;

    Product(String name, int price) {
        name = name;
        price = price;
    }
}

public class Main {
    public static void main(String[] args) {
        Product tea = new Product("Чай", 120);
        System.out.println(tea.name + " " + tea.price);
    }
}
```
```output
null 0
```

Программа компилируется, но поля остались со значениями по умолчанию. Внутри конструктора `name` - это параметр, и запись `name = name` присваивает параметр самому себе. Правильно: `this.name = name;`.

**4. Метод объекта вызван без объекта.**

```java
public class Main {
    String describe() {
        return "Товар: Чай";
    }

    public static void main(String[] args) {
        System.out.println(describe());
    }
}
```

```text
Main.java:7: error: non-static method describe() cannot be referenced from a static context
        System.out.println(describe());
                           ^
1 error
```

`main` - статический метод: он принадлежит классу и не работает ни с каким конкретным объектом. Метод `describe` без `static` требует объекта, поэтому его вызывают у созданного объекта: `new Main().describe()` или `product.describe()`. Если действие не зависит от данных объекта, метод можно сделать `static`.

**5. Обращение к `null`.**

```java
class Product {
    String name = "Чай";
}

public class Main {
    public static void main(String[] args) {
        Product tea = null;
        System.out.println(tea.name);
    }
}
```

```text
Exception in thread "main" java.lang.NullPointerException: Cannot read field "name" because "<local1>" is null
	at Main.main(Main.java:8)
```

Переменная объявлена, но объект не создан: в ней `null`. В сообщении названо поле, которое пытались прочитать; вместо имени переменной может стоять `<local1>`, а место ошибки показывает строка стека (`Main.java:8`). Перед обращением к объекту, который может быть `null`, проверяйте `if (tea != null)`.

<!-- section: summary -->
## Итоги

* Класс описывает поля, конструктор и методы; объект - конкретный экземпляр класса, созданный через `new`.
* Конструктор задаёт начальные значения полей; `this` отличает поле объекта от параметра с тем же именем.
* Методы без `static` работают с полями своего объекта и вызываются через точку.
* Переменная объектного типа хранит ссылку: присваивание и передача в метод копируют ссылку, а не объект.

Вы изучили все основные понятия этого курса. Теперь их можно собрать в одной программе: [Мини-проект: ведомость оценок](lesson:java.from-zero.mini-project).

<!-- section: programmer -->
## Для тех, кто уже программирует

* Класс Java похож на класс Python (`__init__` становится конструктором, `self` - неявный `this`), JavaScript и C#. В Go ближайший аналог - структура с методами.
* Конструктор носит имя класса, возвращаемого типа у него нет. Перегрузка конструкторов поддерживается; значений параметров по умолчанию нет.
* `this` в Java неявный: `self` не нужно писать в параметрах метода, а обращение к полю по голому имени допустимо, если оно не заслонено параметром.
* Модификатор доступа по умолчанию - «пакетный» (виден в пакете), а не `public`. В реальном коде поля почти всегда `private`, с методами доступа или `record` для данных (Java 16+); в этом уроке модификаторы опущены намеренно.
* Один публичный класс на файл с совпадающим именем - требование языка (но не для вложенных и непубличных классов).
* Переменные объектных типов - ссылки; `==` сравнивает ссылки, `equals` по умолчанию тоже; осмысленное сравнение требует переопределения `equals` и `hashCode`. Явного освобождения памяти нет: сборщик мусора удаляет недостижимые объекты.
* `null` допустим для любого объектного типа; `NullPointerException` - самая частая ошибка времени выполнения.
* Перегрузки операторов нет; `toString()` переопределяют, чтобы управлять строковым представлением.

<!-- section: deep-dive -->
## Где живут объекты

Это необязательный раздел: для заданий он не нужен.

Локальные переменные и параметры методов хранятся в стеке вызовов, а сами объекты - в куче (heap), общей области памяти. Переменная объектного типа содержит ссылку на объект в куче. Поэтому присваивание `b = a` копирует лишь ссылку, а два имени оказываются указывателями на один объект.

Каждый объект имеет заголовок с служебной информацией (например, ссылкой на описание класса и данными для блокировок), за которым следуют значения полей. Когда на объект не остаётся ссылок, он становится недостижимым, и сборщик мусора вправе освободить его память; в какой момент это произойдёт, программа не определяет. Подробнее об устройстве JVM - в курсе `java-first-steps`.
