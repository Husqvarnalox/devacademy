<!-- section: simple -->
## Что строим

В этом уроке нет новых конструкций: вы соберёте из изученного законченную программу. Она читает **ведомость** - текст из строк вида `имя;балл`, - считает статистику и выводит отчёт. Так устроены многие реальные задачи: данные приходят текстом, часть строк в них испорчена, а программа не должна падать из-за одной плохой строки.

Мини-проект сводит вместе почти весь курс: чтение ввода, `split` и `trim`, преобразование в число, `try/catch`, класс для записи, `ArrayList`, цикл, условия и форматированный вывод.

## Одна идея: конвейер из небольших шагов

Задачу не решают одним длинным `main`. Её делят на шаги, и каждый шаг проверяют отдельно:

1. **Прочитать** все строки до конца ввода.
2. **Разобрать** одну строку: разрезать, очистить, преобразовать, проверить. Если строка неверная - сообщить об этом исключением.
3. **Посчитать и пропустить**: удачно разобранное добавить в список, неудачное - учесть в счётчике.
4. **Свести** данные: сумма, наибольшее, число подходящих.
5. **Вывести** отчёт, не забыв про случай «данных нет».

Разбор одной строки - отдельный метод, который либо возвращает готовую запись, либо бросает `IllegalArgumentException`. Тогда `main` остаётся коротким: читает строки, вызывает метод и считает плохие.

Ниже этот конвейер показан на похожей задаче - **чек**: строки вида `товар;цена`. Вам предстоит сделать то же самое для ведомости оценок: другие поля и другие правила, но та же структура.

<!-- section: examples -->
## Пример 1. Разбор одной строки

```java run
class Item {
    String name;
    int price;

    Item(String name, int price) {
        this.name = name;
        this.price = price;
    }
}

public class Main {
    static Item parse(String line) {
        String[] parts = line.split(";");
        if (parts.length != 2) {
            throw new IllegalArgumentException("нужны два поля");
        }
        String name = parts[0].trim();
        if (name.isEmpty()) {
            throw new IllegalArgumentException("пустое название");
        }
        int price;
        try {
            price = Integer.parseInt(parts[1].trim());
        } catch (NumberFormatException e) {
            throw new IllegalArgumentException("цена не число");
        }
        if (price < 0) {
            throw new IllegalArgumentException("цена отрицательная");
        }
        return new Item(name, price);
    }

    public static void main(String[] args) {
        String[] lines = {"Чай; 120", "Кофе", " ;50", "Хлеб;abc", "Сахар;-5", "Молоко ; 90 "};
        for (String line : lines) {
            try {
                Item item = parse(line);
                System.out.println("OK: " + item.name + " = " + item.price);
            } catch (IllegalArgumentException e) {
                System.out.println("Плохая строка: " + e.getMessage());
            }
        }
    }
}
```
```output
OK: Чай = 120
Плохая строка: нужны два поля
Плохая строка: пустое название
Плохая строка: цена не число
Плохая строка: цена отрицательная
OK: Молоко = 90
```

Разбор:

* Метод проверяет по порядку: сколько полей, не пусто ли название, число ли цена, не отрицательна ли она. Порядок важен: к `parts[1]` можно обращаться только после проверки `parts.length`.
* Преобразование `parseInt` обёрнуто в `try/catch`, и вместо `NumberFormatException` метод бросает собственное `IllegalArgumentException` с понятным сообщением. Для `main` остаётся один вид исключения.
* `trim()` убирает пробелы вокруг названия и цены, поэтому `"Молоко ; 90 "` разбирается нормально.
* Переменная `price` объявлена до `try`, чтобы её было видно после блока. Компилятор допускает такую запись: если `parseInt` не сработала, `catch` завершается броском исключения, и дальше `price` всегда заполнена.

## Пример 2. Счёт плохих строк

```java run
public class Main {
    static int parsePrice(String line) {
        String[] parts = line.split(";");
        if (parts.length != 2) {
            throw new IllegalArgumentException("нужны два поля");
        }
        return Integer.parseInt(parts[1].trim());
    }

    public static void main(String[] args) {
        String[] lines = {"Чай;120", "ошибка", "Кофе;300", "Сахар;много"};
        int sum = 0;
        int count = 0;
        int bad = 0;
        for (String line : lines) {
            try {
                sum += parsePrice(line);
                count++;
            } catch (IllegalArgumentException e) {
                bad++;
            }
        }
        System.out.println(count + " " + sum + " " + bad);
    }
}
```
```output
2 420 2
```

Разбор:

* Здесь `catch (IllegalArgumentException e)` перехватывает и собственное исключение метода, и `NumberFormatException` из `parseInt`: оба относятся к «недопустимым аргументам». Подробно о связи типов исключений - в `java-fundamentals`; для этого урока достаточно знать, что такой `catch` покрывает оба случая.
* Счётчик `count` растёт только при успехе: если исключение случилось в `parsePrice`, строка `count++` не выполняется.
* Сумма и счётчики не затрагиваются плохими строками - они пропущены и только посчитаны.

## Пример 3. Среднее и случай «данных нет»

```java run
import java.util.ArrayList;
import java.util.Locale;

public class Main {
    static void report(ArrayList<Integer> prices) {
        if (prices.isEmpty()) {
            System.out.println("Среднее: нет данных");
            return;
        }
        int sum = 0;
        for (int p : prices) {
            sum += p;
        }
        double average = (double) sum / prices.size();
        System.out.printf(Locale.ROOT, "Среднее: %.2f%n", average);
    }

    public static void main(String[] args) {
        ArrayList<Integer> a = new ArrayList<>();
        a.add(120);
        a.add(300);
        a.add(5);
        report(a);
        report(new ArrayList<>());
    }
}
```
```output
Среднее: 141.67
Среднее: нет данных
```

Разбор:

* `(double) sum / prices.size()` преобразует сумму в `double` до деления, иначе деление целых отбросило бы дробную часть.
* Пустой список обрабатывается отдельно: делить на ноль нечего, да и «среднее» для пустого набора не определено.
* `Locale.ROOT` фиксирует точку как десятичный разделитель независимо от настроек компьютера, `%.2f` оставляет два знака после запятой, `%n` - перевод строки.

## Пример 4. Целиком: чек из текста

```java run
import java.util.ArrayList;
import java.util.Scanner;

class Item {
    String name;
    int price;

    Item(String name, int price) {
        this.name = name;
        this.price = price;
    }
}

public class Main {
    static Item parse(String line) {
        String[] parts = line.split(";");
        if (parts.length != 2) {
            throw new IllegalArgumentException("нужны два поля");
        }
        String name = parts[0].trim();
        if (name.isEmpty()) {
            throw new IllegalArgumentException("пустое название");
        }
        int price;
        try {
            price = Integer.parseInt(parts[1].trim());
        } catch (NumberFormatException e) {
            throw new IllegalArgumentException("цена не число");
        }
        if (price < 0) {
            throw new IllegalArgumentException("цена отрицательная");
        }
        return new Item(name, price);
    }

    static void report(String text) {
        Scanner in = new Scanner(text);
        ArrayList<Item> items = new ArrayList<>();
        int bad = 0;
        while (in.hasNextLine()) {
            String line = in.nextLine();
            if (line.trim().isEmpty()) {
                continue;
            }
            try {
                items.add(parse(line));
            } catch (IllegalArgumentException e) {
                bad++;
            }
        }
        System.out.println("Позиций: " + items.size());
        if (items.isEmpty()) {
            System.out.println("Самая дорогая: нет данных");
        } else {
            Item top = items.get(0);
            int sum = 0;
            for (Item item : items) {
                sum += item.price;
                if (item.price > top.price) {
                    top = item;
                }
            }
            System.out.println("Сумма: " + sum);
            System.out.println("Самая дорогая: " + top.name + " (" + top.price + ")");
        }
        System.out.println("Пропущено строк: " + bad);
    }

    public static void main(String[] args) {
        report("Чай;120\nКофе; 300\n\nплохая строка\nСахар;80\nХлеб;abc\n");
        System.out.println("---");
        report("");
        System.out.println("---");
        report("?;?\n;5\n");
    }
}
```
```output
Позиций: 3
Сумма: 500
Самая дорогая: Кофе (300)
Пропущено строк: 2
---
Позиций: 0
Самая дорогая: нет данных
Пропущено строк: 0
---
Позиций: 0
Самая дорогая: нет данных
Пропущено строк: 2
```

Разбор:

* `report` принимает текст, поэтому её можно проверить на нескольких наборах данных в одной программе. В упражнении тот же код читает `System.in`.
* Пустые строки пропускаются без подсчёта (`continue`), а строки с ошибками учитываются в `bad`.
* Лучший элемент ищется так: первый берётся как начальный, остальные сравниваются строгим `>`; поэтому при равных значениях побеждает тот, кто встретился раньше.
* Ветка «данных нет» выводит запасную строку вместо того, чтобы обратиться к `items.get(0)` у пустого списка.
* Три набора данных - обычный, пустой и полностью неверный - это минимальный набор проверок для программы такого вида.

**Попробуйте.** Добавьте в чек правило «цена не больше 10000» и новый вывод: сколько позиций стоит больше 200. Затем проверьте программу на наборе, где все строки неверные.

<!-- section: precise -->
## Условие проекта

Программа читает ввод **построчно до конца** и обрабатывает записи ведомости. Точные правила:

* Запись - строка вида `имя;балл`: **ровно два** поля, разделённых символом `;`.
* Имя - непустое после удаления пробелов по краям; оно может содержать пробелы внутри (`Анна Петрова`).
* Балл - целое число от 0 до 100 включительно, пробелы вокруг него допустимы.
* Строки, состоящие только из пробелов (или пустые), игнорируются и ошибкой не считаются.
* Любая другая строка, не удовлетворяющая правилам, - **плохая**: её нужно посчитать и пропустить, не прерывая работу.

Отчёт состоит из пяти строк в таком порядке:

```
Студентов: <число корректных записей>
Средний балл: <среднее с двумя знаками после точки или «нет данных»>
Лучший: <имя> (<балл>) или «нет данных»
Отличников (90 и выше): <сколько записей с баллом не ниже 90>
Пропущено строк: <число плохих строк>
```

Дополнительные правила:

* Если корректных записей нет, среднее и лучший выводятся как `нет данных`, а число отличников - `0`.
* Если лучший балл у нескольких студентов, выбирается тот, чья запись встретилась в вводе раньше.
* Среднее выводится с точкой в качестве десятичного разделителя: `Locale.ROOT`.
* Метод разбора должен работать независимо от `main`: так его проще проверять и читать.
* Порядок проверок внутри разбора: число полей, имя, преобразование в число, диапазон. Обращаться к полю можно только после проверки их количества.

<!-- section: mistakes -->
## Типичные ошибки

**1. Среднее целых чисел.**

```java run
public class Main {
    public static void main(String[] args) {
        int sum = 7;
        int count = 2;
        System.out.println(sum / count);
        System.out.println((double) sum / count);
    }
}
```
```output
3
3.5
```

Деление двух `int` отбрасывает дробную часть. Одно из чисел нужно сначала преобразовать в `double`.

**2. Деление на ноль при пустых данных.**

```java run
public class Main {
    public static void main(String[] args) {
        double sum = 0;
        int count = 0;
        System.out.println(sum / count);
    }
}
```
```output
NaN
```

Для чисел с плавающей точкой деление на ноль не бросает исключение: получается `NaN` («не число»). В отчёте окажется бессмысленное значение. Пустой набор данных обрабатывают отдельной веткой.

**3. Первый элемент пустого списка.**

```java
import java.util.ArrayList;

public class Main {
    public static void main(String[] args) {
        ArrayList<Integer> scores = new ArrayList<>();
        int best = scores.get(0);
        System.out.println(best);
    }
}
```

```text
Exception in thread "main" java.lang.IndexOutOfBoundsException: Index 0 out of bounds for length 0
	...
	at Main.main(Main.java:6)
```

Если все строки оказались плохими, список пуст, и `get(0)` падает. Начальное значение для поиска наибольшего берут только после проверки `isEmpty()`.

**4. Число с пробелом без `trim`.**

```java
public class Main {
    public static void main(String[] args) {
        String field = " 85";
        int score = Integer.parseInt(field);
        System.out.println(score);
    }
}
```

```text
Exception in thread "main" java.lang.NumberFormatException: For input string: " 85"
	...
	at Main.main(Main.java:4)
```

`parseInt` не принимает пробелы: в сообщении видно, что строка содержит пробел. Поле нужно очистить: `Integer.parseInt(field.trim())`.

**5. Обращение к полю до проверки их числа.**

```java
public class Main {
    public static void main(String[] args) {
        String[] parts = "Анна".split(";");
        String score = parts[1];
        System.out.println(score);
    }
}
```

```text
Exception in thread "main" java.lang.ArrayIndexOutOfBoundsException: Index 1 out of bounds for length 1
	at Main.main(Main.java:4)
```

Строка без `;` даёт массив из одного элемента. Сначала проверьте `parts.length`, затем читайте `parts[1]`.

**6. Нестрогое сравнение при выборе лучшего.** Условие `score >= best.score` на равных баллах заменит лучшего последним встреченным. Для «первый из равных» нужно строгое `>`. Аналогично граница отличника: «90 и выше» - это `>= 90`, а не `> 90`.

<!-- section: summary -->
## Итоги

* Данные из текста обрабатывают конвейером: прочитать, разобрать, посчитать и пропустить плохое, свести, вывести.
* Разбор записи выносят в отдельный метод, который возвращает объект или бросает `IllegalArgumentException`.
* Пустой набор данных, граничные значения и полностью неверный ввод проверяют отдельно.
* Вы прошли основной курс языка: переменные, ввод и вывод, условия, циклы, методы, массивы, коллекции, строки, исключения и классы.

Дальше курс объясняет, что происходит с программой между сохранением файла и результатом на экране, и подробнее о примитивах и объектах: [Как Java запускает программу](lesson:java.first-steps.how-java-runs).

<!-- section: programmer -->
## Для тех, кто уже программирует

* Шаблон «прочитать строки, разобрать, посчитать плохие» тот же, что в Python (`for line in sys.stdin` и `try/except ValueError`), Go (`bufio.Scanner` и проверка ошибки `strconv.Atoi`) или JavaScript. Отличие Java - исключения вместо возвращаемых ошибок и явные типы.
* В промышленном коде `Scanner` для больших входных данных заменяют на `BufferedReader` (быстрее) или читают файл через `Files.readAllLines` / `Files.lines`.
* Для записи с неизменяемыми данными в современной Java используют `record Student(String name, int score) {}` (Java 16+): конструктор, доступ к полям, `equals` и `toString` создаются автоматически. Курс использует обычный класс, чтобы показать механику.
* Вместо `null` или исключения «не получилось разобрать» часто возвращают `Optional<Student>`; исключения для ожидаемых плохих данных на горячем пути дороги.
* Сведение данных (сумма, наибольшее, количество) на практике записывают потоками: `list.stream().mapToInt(...).average()`, `max(Comparator...)`. Цикл здесь нагляднее для новичка и ничем не хуже по скорости.
* Привычка отделять разбор от ввода-вывода (`parse` не знает про `Scanner`) упрощает автоматические тесты: метод проверяется вызовом с разными строками.
* `Locale.ROOT` нужен потому, что `printf` без локали использует настройки компьютера: на русской системе вместо `3.50` будет `3,50`.

<!-- section: deep-dive -->
## Почему разбор отделён от ввода

Это необязательный раздел: для заданий он не нужен.

Метод `parse(String)` зависит только от своего аргумента: ему не нужны `Scanner`, консоль и файлы. Такие методы называют **чистыми** по отношению к вводу-выводу. Их проверяют обычным вызовом с набором строк, в том числе граничных (`""`, `";"`, `"a;b;c"`), и они дают одинаковый результат при каждом запуске. Весь ввод-вывод собирается в `main`, поэтому именно его труднее проверять, и именно его стараются сделать как можно короче.

Та же идея лежит в основе профессиональных тестов: в проектах пишут отдельные тесты на `parse` с таблицей входов и ожидаемых результатов. Проверки в этом курсе построены так же, только вместо вызова метода они запускают всю программу целиком и сравнивают вывод.
