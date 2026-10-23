<!-- section: simple -->
## Зачем нужны исключения

Ввод бывает неправильным: вместо числа пришло «abc», делитель оказался нулём, индекс вышел за границы массива. Когда программа не может выполнить действие, Java создаёт **исключение** - объект с описанием ошибки - и прерывает обычный ход программы. Если ничего не сделать, программа аварийно завершается с сообщением. Вы уже видели такие сообщения: `NumberFormatException`, `ArrayIndexOutOfBoundsException`.

Исключение можно **перехватить** и обработать: сообщить о проблеме, пропустить плохие данные, подставить запасное значение. Тогда программа продолжает работу. Именно так пишут программы, которые не падают из-за одной плохой строки ввода.

## Одна идея: try пробует, catch обрабатывает

```java
try {
    int n = Integer.parseInt(text);
    System.out.println("Число: " + n);
} catch (NumberFormatException e) {
    System.out.println("Это не число");
}
```

* В блок `try` помещают код, который может завершиться исключением.
* Если исключения нет, блок `catch` пропускается.
* Если в `try` возникло исключение указанного в `catch` типа, остаток блока `try` пропускается и выполняется блок `catch`. Потом программа идёт дальше, после всей конструкции.

Исключение можно и **создать самому**: оператор `throw new ...` сообщает «здесь данные недопустимы». Так метод сообщает вызывающему коду, что получил неправильное значение:

```java
throw new IllegalArgumentException("Возраст не может быть отрицательным");
```

`IllegalArgumentException` - стандартный тип исключения для «метод получил недопустимый аргумент». В скобках - текст сообщения; его можно получить в `catch` вызовом `e.getMessage()`.

<!-- section: examples -->
## Пример 1. Перехват NumberFormatException

```java run
public class Main {
    public static void main(String[] args) {
        String text = "abc";
        try {
            int n = Integer.parseInt(text);
            System.out.println("Число: " + n);
        } catch (NumberFormatException e) {
            System.out.println("Это не число: " + text);
        }
        System.out.println("Программа продолжает работу");
    }
}
```
```output
Это не число: abc
Программа продолжает работу
```

Разбор:

* `Integer.parseInt("abc")` бросает `NumberFormatException`. Строка с `println("Число: ...")` не выполняется: управление сразу переходит в `catch`.
* После конструкции `try/catch` программа идёт дальше. Без перехвата она завершилась бы на строке с `parseInt`.

## Пример 2. Что хранит исключение

```java run
public class Main {
    public static void main(String[] args) {
        try {
            int n = Integer.parseInt("12x");
            System.out.println(n);
        } catch (NumberFormatException e) {
            System.out.println("Сообщение: " + e.getMessage());
        }
        try {
            int d = 0;
            System.out.println(10 / d);
        } catch (ArithmeticException e) {
            System.out.println("Сообщение: " + e.getMessage());
        }
    }
}
```
```output
Сообщение: For input string: "12x"
Сообщение: / by zero
```

Разбор:

* Переменная `e` в `catch` - объект исключения. Метод `getMessage()` возвращает текст с описанием причины.
* Деление целого числа на ноль бросает `ArithmeticException`; для каждого типа ошибки пишут свой `catch`.

## Пример 3. Метод, который сообщает об ошибке через throw

```java run
public class Main {
    static int checkedAge(int age) {
        if (age < 0 || age > 150) {
            throw new IllegalArgumentException("Недопустимый возраст: " + age);
        }
        return age;
    }

    public static void main(String[] args) {
        int[] ages = {25, -3, 200, 40};
        for (int age : ages) {
            try {
                System.out.println("Возраст: " + checkedAge(age));
            } catch (IllegalArgumentException e) {
                System.out.println("Ошибка: " + e.getMessage());
            }
        }
    }
}
```
```output
Возраст: 25
Ошибка: Недопустимый возраст: -3
Ошибка: Недопустимый возраст: 200
Возраст: 40
```

Разбор:

* Метод `checkedAge` не решает, что делать с плохим возрастом: он выбрасывает исключение с понятным сообщением. Решает вызывающий код - здесь `main` печатает ошибку и идёт к следующему элементу.
* При выбросе исключения строка `return age;` не выполняется, а метод завершается досрочно.
* Цикл продолжается после каждого `catch`: плохие значения не останавливают обработку остальных.

## Пример 4. Метод isNumber и запасное значение

```java run
public class Main {
    static boolean isNumber(String text) {
        try {
            Integer.parseInt(text);
            return true;
        } catch (NumberFormatException e) {
            return false;
        }
    }

    static int parseOrDefault(String text, int fallback) {
        try {
            return Integer.parseInt(text);
        } catch (NumberFormatException e) {
            return fallback;
        }
    }

    public static void main(String[] args) {
        System.out.println(isNumber("42"));
        System.out.println(isNumber("4.2"));
        System.out.println(isNumber(""));
        System.out.println(parseOrDefault("17", 0));
        System.out.println(parseOrDefault("нет", 0));
    }
}
```
```output
true
false
false
17
0
```

Разбор:

* `isNumber` отвечает «можно ли превратить строку в целое число»: если `parseInt` не бросила исключение, возвращается `true`, иначе - `false` из `catch`.
* `parseOrDefault` возвращает число, а для неподходящей строки - заранее заданное значение.
* Пустая строка и дробное число `4.2` не являются целыми числами, поэтому дают `NumberFormatException`.

## Пример 5. Подсчёт плохих строк

```java run
public class Main {
    public static void main(String[] args) {
        String[] tokens = {"10", "x", "25", "3.5", "5"};
        int sum = 0;
        int bad = 0;
        for (String token : tokens) {
            try {
                sum += Integer.parseInt(token);
            } catch (NumberFormatException e) {
                bad++;
            }
        }
        System.out.println("Сумма: " + sum);
        System.out.println("Пропущено: " + bad);
    }
}
```
```output
Сумма: 40
Пропущено: 2
```

Разбор:

* Для каждого элемента `parseInt` либо прибавляет число к сумме, либо бросает исключение: тогда `sum` не меняется, а счётчик плохих значений увеличивается на 1.
* Это основной шаблон обработки ненадёжного ввода: **посчитать и пропустить**. Он понадобится в итоговом мини-проекте.

**Попробуйте.** В примере 5 добавьте в массив ещё несколько элементов, в том числе пустую строку `""` и число `2147483648` (оно слишком велико для `int`). Предскажите, сколько значений будет пропущено, и проверьте.

<!-- section: precise -->
## Точные правила

* Конструкция: `try { ... } catch (ТипИсключения e) { ... }`. Блоков `catch` может быть несколько, для разных типов; выполняется первый подходящий.
* Если в `try` не возникло исключение, `catch` пропускается. Если возникло, оставшиеся строки `try` не выполняются.
* Если ни один `catch` не подходит, исключение идёт дальше, к вызывающему коду. Если оно дошло до `main` и не обработано, программа завершается с сообщением и стеком вызовов.
* Переменная, объявленная в `try`, видна только внутри этого блока (область видимости, как у любого блока). Если значение нужно после конструкции, переменную объявляют до `try`.
* `throw new Тип("сообщение")` создаёт и выбрасывает исключение. Метод при этом завершается досрочно, значение не возвращается.
* Для недопустимых аргументов используют `IllegalArgumentException`. Его выбрасывают в начале метода, пока ничего не изменено: так сообщение указывает на настоящую причину.
* `e.getMessage()` возвращает текст сообщения; для `NumberFormatException` это `For input string: "..."`.
* Ловить нужно конкретный тип: `NumberFormatException` для `parseInt`/`parseDouble`, `ArithmeticException` для деления целых на ноль. Тип `Exception` перехватывает почти любые исключения, включая ошибки в самой программе, поэтому им пользуются осторожно.
* Исключения не заменяют проверок: если ошибку легко предусмотреть условием (`if (d != 0)`), условие проще и понятнее. Исключения нужны там, где проверить заранее сложно (разбор числа из текста) или где метод должен сообщить о нарушении контракта.
* Пустой `catch` скрывает ошибку и усложняет поиск причины. В блоке нужно хотя бы посчитать, записать или сообщить о проблеме.
* Все исключения этого урока - непроверяемые: Java не требует их перехватывать или объявлять. Проверяемые исключения и слово `throws` появляются при работе с файлами (`java-fundamentals`).

<!-- section: mistakes -->
## Типичные ошибки

**1. Исключение не перехвачено.**

```java
public class Main {
    public static void main(String[] args) {
        int n = Integer.parseInt("abc");
        System.out.println("Число: " + n);
    }
}
```

```text
Exception in thread "main" java.lang.NumberFormatException: For input string: "abc"
	...
	at Main.main(Main.java:3)
```

Программа завершается на строке с `parseInt`, а `println` не выполняется. Первая строка сообщения называет тип и причину, а строка `at Main.main(Main.java:3)` - место в вашем коде, откуда читать причину. Если ошибку можно ожидать, оберните вызов в `try/catch`.

**2. Перехвачен не тот тип.**

```java
public class Main {
    public static void main(String[] args) {
        try {
            int n = Integer.parseInt("abc");
            System.out.println(n);
        } catch (ArithmeticException e) {
            System.out.println("Деление на ноль");
        }
    }
}
```

```text
Exception in thread "main" java.lang.NumberFormatException: For input string: "abc"
	...
	at Main.main(Main.java:4)
```

`catch` срабатывает только для указанного типа. Здесь бросается `NumberFormatException`, а ловится `ArithmeticException`, поэтому исключение прошло мимо.

**3. Переменная из try не видна снаружи.**

```java
public class Main {
    public static void main(String[] args) {
        try {
            int n = Integer.parseInt("42");
        } catch (NumberFormatException e) {
            System.out.println("Не число");
        }
        System.out.println(n);
    }
}
```

```text
Main.java:8: error: cannot find symbol
        System.out.println(n);
                           ^
  symbol:   variable n
  location: class Main
1 error
```

Переменная `n` существует только внутри блока `try`. Если число нужно после конструкции, объявите переменную до неё (`int n = 0;`) и присваивайте внутри `try`.

**4. throw без new.**

```java
public class Main {
    static void check(int age) {
        if (age < 0) {
            throw IllegalArgumentException("Возраст меньше нуля");
        }
    }

    public static void main(String[] args) {
        check(-1);
    }
}
```

```text
Main.java:4: error: cannot find symbol
            throw IllegalArgumentException("Возраст меньше нуля");
                  ^
  symbol:   method IllegalArgumentException(String)
  location: class Main
1 error
```

Исключение - объект, и его создают оператором `new`, как любой объект: `throw new IllegalArgumentException(...)`. Без `new` компилятор ищет метод с таким именем и не находит.

**5. Пустой catch.**

```java
try {
    sum += Integer.parseInt(token);
} catch (NumberFormatException e) {
}
```

Программа не упадёт, но плохие данные пропадут бесследно: итоговая сумма окажется неверной, а причину никто не увидит. Хотя бы посчитайте пропущенные значения (`bad++`) или сообщите о них.

<!-- section: summary -->
## Итоги

* Исключение прерывает обычный ход программы; без перехвата программа аварийно завершается.
* `try { ... } catch (Тип e) { ... }` перехватывает исключение указанного типа; программа продолжает работу после конструкции.
* `throw new IllegalArgumentException("сообщение")` сообщает о недопустимых данных; `e.getMessage()` возвращает сообщение.
* Для ненадёжного ввода применяют «посчитать и пропустить»: плохое значение учитывается в счётчике и не останавливает остальную обработку.

Исключение - это объект, а Java работает с объектами повсюду. Следующий шаг - научиться создавать собственные: [Первый класс](lesson:java.from-zero.first-class).

<!-- section: programmer -->
## Для тех, кто уже программирует

* `try/catch` аналогичен `try/except` в Python и `try/catch` в JavaScript и C#. Блок `catch` указывает тип; блоков может быть несколько. В Java есть ещё `finally` и `try-with-resources` (в `java-fundamentals`).
* Исключения бывают проверяемыми (`IOException`: компилятор требует перехватить или объявить через `throws`) и непроверяемыми (наследники `RuntimeException`: `NumberFormatException`, `IllegalArgumentException`, `ArithmeticException`, `NullPointerException`). В этом уроке встречаются только непроверяемые.
* Стиль Go (возврат ошибки значением) в Java не принят для обычных ошибок; для «ожидаемого отсутствия» чаще используют `Optional` или возврат `null`, а для нарушений контракта - исключение.
* Идиоматичный способ проверить аргумент - `IllegalArgumentException`; для недопустимого состояния объекта - `IllegalStateException`; для `null` - `Objects.requireNonNull`.
* Исключение дорого создавать (собирается стек вызовов), поэтому не стоит использовать его как обычный `if` в горячем цикле. Для разбора чисел на горячем пути пишут проверку без исключений.
* Деление целых на ноль бросает `ArithmeticException`, а деление `double` на ноль даёт `Infinity` или `NaN` без исключения.
* `catch (Exception e)` слишком широк; ловите конкретный тип. Пустой `catch` считается плохим стилем.
* Мульти-перехват: `catch (NumberFormatException | ArithmeticException e)`.

<!-- section: deep-dive -->
## Что происходит при выбросе исключения

Это необязательный раздел: для заданий он не нужен.

Когда выполняется `throw`, виртуальная машина создаёт объект исключения и записывает в него **стек вызовов** - цепочку методов, которая привела к ошибке. Именно эти строки `at Main.main(Main.java:3)` вы видите в сообщении. Затем JVM ищет подходящий `catch` в текущем методе. Если его нет, текущий метод аварийно завершается, и поиск продолжается в вызвавшем его методе, и так вверх по цепочке. Это называется раскруткой стека. Если подходящего `catch` нет и в `main`, поток завершается, а JVM печатает исключение со стеком.

Поэтому метод `checkedAge` из примера 3 может вообще не знать, что с ошибкой делать: исключение само поднимется до того кода, который готов его обработать. Платой за это является создание стека вызовов: выброс исключения заметно дороже обычного возврата значения.
