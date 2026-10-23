<!-- section: simple -->
## Зачем нужны методы строк

Почти любой ввод приходит текстом: «  Анна ; 5 », «anna@example.com», «кот,пёс,рыба». Чтобы использовать такие данные, их нужно очистить, сравнить, разрезать на части и найти в них нужное. Для этого у строк в Java есть набор методов. Часть из них вы уже видели: `length()`, `charAt(i)`, `toUpperCase()` и `toLowerCase()`. Здесь добавляется то, что нужно для разбора строк из ввода.

## Одна идея: строка неизменяема, метод возвращает новую

Строку нельзя изменить «на месте». Любой метод, который, казалось бы, меняет строку (`trim`, `replace`, `substring`), на самом деле **возвращает новую строку**, а старая остаётся как была. Поэтому результат нужно куда-то записать: в переменную или сразу в вывод.

Вторая идея - **как сравнивать строки**. Для чисел сравнивают знаком `==`. Для строк `==` сравнивает не текст, а то, *один ли это объект в памяти*; результат зависит от того, откуда взялись строки. Текст сравнивают методом `equals`:

```java
String answer = "да";
System.out.println(answer.equals("да"));   // true
```

Основные методы (пусть `s` - строка):

| Запись | Что возвращает |
|---|---|
| `s.equals(t)` | `true`, если строки совпадают по тексту (регистр важен) |
| `s.trim()` | строку без пробелов и переводов строки в начале и конце |
| `s.contains(t)` | `true`, если `t` встречается внутри `s` |
| `s.indexOf(t)` | индекс первого вхождения `t` или `-1`, если нет |
| `s.substring(a)` | часть от индекса `a` до конца |
| `s.substring(a, b)` | часть от индекса `a` до индекса `b`, **не включая** `b` |
| `s.replace(x, y)` | строку, где каждое `x` заменено на `y` |
| `s.split(разделитель)` | **массив** строк - части, разрезанные по разделителю |
| `s.isEmpty()` | `true`, если длина равна 0 |

Индексы, как всегда, идут с нуля.

<!-- section: examples -->
## Пример 1. equals и очистка пробелов

```java run
public class Main {
    public static void main(String[] args) {
        String raw = "  Да  ";
        String clean = raw.trim();
        System.out.println("[" + raw + "]");
        System.out.println("[" + clean + "]");
        System.out.println(clean.equals("Да"));
        System.out.println(clean.equals("да"));
        System.out.println(clean.toLowerCase().equals("да"));
    }
}
```
```output
[  Да  ]
[Да]
true
false
true
```

Разбор:

* `trim()` вернула новую строку без крайних пробелов. Исходная `raw` не изменилась, это видно по первой строке вывода.
* `equals` учитывает регистр: «Да» и «да» - разные строки. Чтобы сравнить без учёта регистра, обе стороны приводят к одному регистру (`toLowerCase`).
* Квадратные скобки в выводе показывают границы строки: так видно пробелы.

## Пример 2. Поиск и вырезание части

```java run
public class Main {
    public static void main(String[] args) {
        String email = "anna@example.com";
        int at = email.indexOf("@");
        System.out.println(at);
        System.out.println(email.substring(0, at));
        System.out.println(email.substring(at + 1));
        System.out.println(email.contains("example"));
        System.out.println(email.indexOf("#"));
    }
}
```
```output
4
anna
example.com
true
-1
```

Разбор:

* `indexOf("@")` нашёл индекс символа `@` - 4. Если бы символа не было, метод вернул бы `-1`.
* `substring(0, at)` берёт символы с индекса 0 до `at` **не включая** сам `@`: получается «anna».
* `substring(at + 1)` берёт всё после `@`.
* Для отсутствующей подстроки `indexOf` даёт `-1`, это стандартный признак «не найдено».

## Пример 3. Замена и разрезание

```java run
public class Main {
    public static void main(String[] args) {
        String phone = "8-900-123-45-67";
        System.out.println(phone.replace("-", ""));

        String line = "кот,пёс,рыба";
        String[] parts = line.split(",");
        System.out.println(parts.length);
        for (String part : parts) {
            System.out.println(part);
        }
        System.out.println(line);
    }
}
```
```output
89001234567
3
кот
пёс
рыба
кот,пёс,рыба
```

Разбор:

* `replace("-", "")` заменяет каждый дефис на пустую строку, то есть удаляет их.
* `split(",")` вернул массив из трёх строк. С ним работают так же, как с любым массивом: `parts.length`, `parts[0]`, цикл for-each.
* Исходная строка `line` после `split` не изменилась.

## Пример 4. Разбор записи «имя; оценка»

```java run
public class Main {
    public static void main(String[] args) {
        String record = "  Анна Петрова ;  5 ";
        String[] parts = record.split(";");
        String name = parts[0].trim();
        int grade = Integer.parseInt(parts[1].trim());
        System.out.println("Имя: " + name);
        System.out.println("Оценка: " + grade);
        System.out.println("Оценка + 1 = " + (grade + 1));
    }
}
```
```output
Имя: Анна Петрова
Оценка: 5
Оценка + 1 = 6
```

Разбор:

* Строка разрезана по `;` на две части, у каждой остались пробелы по краям.
* `trim()` убирает пробелы. Это нужно, чтобы `Integer.parseInt` получила чистое число: с пробелами она бросает `NumberFormatException` (урок о преобразовании типов).
* Так разбирают почти любые текстовые записи: разрезать, очистить, при необходимости преобразовать.

## Пример 5. Чтение строки целиком и проверка слова

```java run
import java.util.Scanner;

public class Main {
    static boolean isYes(String answer) {
        return answer.trim().toLowerCase().equals("да");
    }

    public static void main(String[] args) {
        Scanner in = new Scanner("  ДА \nнет\n");
        String first = in.nextLine();
        String second = in.nextLine();
        System.out.println(isYes(first));
        System.out.println(isYes(second));
    }
}
```
```output
true
false
```

Разбор:

* `nextLine()` читает строку целиком вместе с пробелами. Метод `isYes` нормализует её цепочкой `trim().toLowerCase()` и только потом сравнивает через `equals`.
* Цепочка читается слева направо: каждый метод применяется к строке, которую вернул предыдущий.

**Попробуйте.** В примере 2 получите домен без зоны: подстрока между `@` и последней точкой. Для этого найдите индекс точки через `indexOf(".")` и передайте оба индекса в `substring`.

<!-- section: precise -->
## Точные правила

* Строки неизменяемы: `trim`, `replace`, `substring`, `toUpperCase` и другие возвращают новую строку. Оператор `s.trim();` без присваивания ничего не меняет в `s`.
* `s.equals(t)` сравнивает содержимое с учётом регистра. Есть вариант без учёта регистра - `s.equalsIgnoreCase(t)`.
* `s == t` для строк сравнивает ссылки (один ли это объект). Два одинаковых литерала в коде могут оказаться одним объектом, а строки, прочитанные из ввода или собранные в программе, - разными объектами с одинаковым текстом. Поэтому для текста всегда используют `equals`.
* `s.equals(null)` возвращает `false`, а вызов метода у переменной со значением `null` приводит к `NullPointerException`. Литерал можно ставить слева: `"да".equals(answer)` безопасно даже при `answer == null`.
* `indexOf(t)` возвращает индекс первого вхождения или `-1`. Есть `indexOf(t, начало)` и `lastIndexOf(t)`. Индекс `-1` нельзя использовать как индекс в `substring`.
* `substring(a, b)` берёт символы с `a` по `b - 1`. Нужно `0 <= a <= b <= s.length()`, иначе `StringIndexOutOfBoundsException`. Длина результата равна `b - a`.
* `trim()` убирает пробелы, табуляции и переводы строки по краям, но не внутри строки.
* `replace(x, y)` заменяет **все** вхождения. Аргументы могут быть строками или символами (`replace('-', ' ')`).
* `split(разделитель)` принимает **регулярное выражение**. Для обычных символов вроде `;` `,` и пробела это незаметно, но `.` и `|` в регулярных выражениях имеют особый смысл, и `"a.b".split(".")` возвращает пустой массив. Такие символы пишут с экранированием: `split("\\.")`.
* `split` отбрасывает пустые строки в конце результата: `"a;b;;".split(";")` даёт две части, а не четыре.
* Если разделитель не найден, `split` возвращает массив из одного элемента - всей строки.
* `s.isEmpty()` истинно только при длине 0; строка из одних пробелов не пуста. Для неё `s.trim().isEmpty()` истинно.

<!-- section: mistakes -->
## Типичные ошибки

**1. Сравнение строк через `==`.**

```java run
import java.util.Scanner;

public class Main {
    public static void main(String[] args) {
        Scanner in = new Scanner("да");
        String answer = in.next();
        System.out.println(answer == "да");
        System.out.println(answer.equals("да"));
    }
}
```
```output
false
true
```

Программа компилируется и запускается без ошибок, но `==` сравнивает объекты, а не текст, и здесь даёт `false`. Такая ошибка коварна: на литералах `==` иногда «работает», и ошибка проявляется позже. Для текста всегда `equals`.

**2. Результат метода выброшен.**

```java run
public class Main {
    public static void main(String[] args) {
        String name = "  Анна  ";
        name.trim();
        System.out.println("[" + name + "]");
        name = name.trim();
        System.out.println("[" + name + "]");
    }
}
```
```output
[  Анна  ]
[Анна]
```

`name.trim();` вернула новую строку, но её никто не сохранил, поэтому `name` не изменилась. Результат нужно присвоить: `name = name.trim();`.

**3. Конец подстроки за границей.**

```java
public class Main {
    public static void main(String[] args) {
        String word = "кот";
        System.out.println(word.substring(1, 5));
    }
}
```

```text
Exception in thread "main" java.lang.StringIndexOutOfBoundsException: Range [1, 5) out of bounds for length 3
	...
	at Main.main(Main.java:4)
```

Строка из трёх символов, а второй аргумент `substring` равен 5. В сообщении указаны начало, конец и длина строки: из них видно, какой индекс вышел за границу.

**4. Не хватило частей после `split`.**

```java
public class Main {
    public static void main(String[] args) {
        String line = "Анна";
        String[] parts = line.split(";");
        System.out.println(parts[1]);
    }
}
```

```text
Exception in thread "main" java.lang.ArrayIndexOutOfBoundsException: Index 1 out of bounds for length 1
	at Main.main(Main.java:5)
```

В строке нет разделителя, поэтому `split` вернул массив из одного элемента, а обращение к `parts[1]` вышло за его границы. Перед обращением к частям проверяйте `parts.length`.

**5. Скобки у `length`.**

```java
public class Main {
    public static void main(String[] args) {
        String word = "кот";
        System.out.println(word.length);
    }
}
```

```text
Main.java:4: error: cannot find symbol
        System.out.println(word.length);
                               ^
  symbol:   variable length
  location: variable word of type String
1 error
```

У массива длина - поле `length` без скобок, у строки - метод `length()` со скобками. Компилятор подсказывает, что поля с таким именем нет.

**6. Точка как разделитель.**

```java run
public class Main {
    public static void main(String[] args) {
        String version = "1.2.3";
        System.out.println(version.split(".").length);
        System.out.println(version.split("\\.").length);
    }
}
```
```output
0
3
```

`split` принимает регулярное выражение, а `.` в нём означает «любой символ». Первая строка вернула пустой массив. Чтобы разрезать по самой точке, пишут `"\\."`.

<!-- section: summary -->
## Итоги

* Строки неизменяемы: методы возвращают новую строку, результат нужно сохранить.
* Текст сравнивают через `equals`; `==` сравнивает объекты и для строк не подходит.
* `trim`, `substring`, `indexOf`, `contains` и `replace` позволяют очистить строку и вырезать нужную часть; `indexOf` возвращает `-1`, если подстроки нет.
* `split` режет строку на массив частей; перед обращением к части проверяйте `length`.

Разбор строк почти всегда заканчивается преобразованием чисел, и тогда возможна ошибка в данных. Как её обработать, не уронив программу: [Исключения: try и catch](lesson:java.from-zero.exceptions).

<!-- section: programmer -->
## Для тех, кто уже программирует

* `String` в Java неизменяема, как в Python, JavaScript и Go. Методы возвращают новую строку; сам объект менять нельзя.
* `==` на строках сравнивает ссылки, в отличие от Python и JavaScript, где `==` сравнивает значения. В C# оператор `==` для `string` перегружен и сравнивает текст, в Java нет перегрузки операторов. Правило: `equals` для содержимого. Безопасная от `null` форма - `"литерал".equals(x)` или `Objects.equals(a, b)`.
* Литералы и результаты вычислений в константных выражениях попадают в пул строк, поэтому `==` на литералах нередко даёт `true`. Это деталь реализации, на неё нельзя полагаться.
* `split` принимает регулярное выражение (в отличие от `split` в Python и Go, которые режут по подстроке). Специальные символы нужно экранировать: `"\\."`, `"\\|"`. Пустые хвостовые элементы отбрасываются; `split(sep, -1)` оставляет их.
* `indexOf` возвращает `-1`, а не бросает исключение, как `index` в Python.
* `substring(a, b)` использует полуинтервал `[a, b)`, как срезы в Python, но выход за границы бросает `StringIndexOutOfBoundsException`, а не усекается.
* Строки Java хранятся как UTF-16: `length()` и `charAt` работают с кодовыми единицами UTF-16. Для редких символов вне базовой плоскости (например, некоторых эмодзи) одна «буква» занимает две единицы.
* Многократная склейка строк в цикле создаёт много временных объектов; для этого есть `StringBuilder` (в `java-fundamentals`). Метод `String.join(", ", список)` и `strip()` (учитывает пробелы Юникода) - современные дополнения.

<!-- section: deep-dive -->
## Пул строк и почему == ненадёжен

Это необязательный раздел: для заданий он не нужен.

Компилятор помещает одинаковые строковые литералы в общую таблицу - **пул строк** (string pool), и две записи `"да"` в разных местах программы указывают на один и тот же объект. Поэтому `"да" == "да"` истинно. А строка, прочитанная из `Scanner`, собранная конкатенацией во время работы или полученная методом `substring`, создаётся как новый объект в куче: текст тот же, объект другой, и `==` даёт `false`. Метод `intern()` возвращает объект из пула, но он нужен только в специальных случаях.

`equals` у `String` сначала проверяет, не один ли это объект, затем сравнивает длины и содержимое посимвольно. Поэтому он корректен независимо от того, откуда взялись строки.

Неизменяемость даёт гарантии: строку безопасно передавать между частями программы и потоками, использовать как ключ в `HashMap` (её хеш-код не меняется, а в самой строке он кэшируется), а виртуальная машина может свободно разделять одинаковые строки.
