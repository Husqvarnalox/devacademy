<!-- section: simple -->
## Зачем нужен список

Массив имеет фиксированный размер. Если оценки вводятся «пока не закончатся» или товары добавляются в корзину и удаляются из неё, количество элементов заранее не известно и меняется. Для таких данных в Java есть **список** - `ArrayList`: он хранит элементы по порядку, как массив, но сам растёт при добавлении и сжимается при удалении.

## Одна новая идея: ArrayList<тип> - растущий список

Подключение и создание:

```java
import java.util.ArrayList;

ArrayList<Integer> grades = new ArrayList<>();
```

* `import java.util.ArrayList;` - строка в начале файла, как `import` для `Scanner`: сообщает, где искать `ArrayList`.
* В **угловых скобках** `<Integer>` записывают **тип элементов** списка: «список целых чисел». Для текстов `ArrayList<String>`. В угловых скобках нельзя писать `int`, `double`, `boolean`: для них пишут `Integer`, `Double`, `Boolean`. Это те же значения; Java сама превращает число в `Integer` и обратно.
* `new ArrayList<>()` создаёт пустой список. Пустые угловые скобки `<>` компилятор заполняет сам по типу слева.

Основные действия (список целых `grades`):

| Запись | Что делает |
|---|---|
| `grades.add(5)` | добавляет элемент в конец |
| `grades.get(0)` | возвращает элемент с индексом 0 (индексы с нуля, как у массива) |
| `grades.set(0, 4)` | заменяет элемент с индексом 0 |
| `grades.size()` | число элементов (со скобками, это метод) |
| `grades.contains(5)` | есть ли в списке значение 5 (`true` или `false`) |
| `grades.remove(0)` | удаляет элемент с индексом 0; следующие сдвигаются влево |
| `grades.isEmpty()` | пуст ли список |

Список можно напечатать целиком: `System.out.println(grades)` показывает элементы в квадратных скобках через запятую, например `[5, 4, 5]`. Обходят список циклом for-each (`for (int g : grades)`) или по индексу (`for (int i = 0; i < grades.size(); i++)` и `grades.get(i)`).

<!-- section: examples -->
## Пример 1. Создание и основные действия

```java run
import java.util.ArrayList;

public class Main {
    public static void main(String[] args) {
        ArrayList<Integer> grades = new ArrayList<>();
        System.out.println("Размер: " + grades.size());
        grades.add(5);
        grades.add(4);
        grades.add(5);
        System.out.println(grades);
        System.out.println("Размер: " + grades.size());
        System.out.println("Первая: " + grades.get(0));
        grades.set(1, 3);
        System.out.println(grades);
    }
}
```
```output
Размер: 0
[5, 4, 5]
Размер: 3
Первая: 5
[5, 3, 5]
```

Разбор:

* Пустой список имеет размер 0. После каждого `add` размер растёт на 1.
* `get(0)` возвращает первый элемент, `set(1, 3)` заменяет второй.
* `println(grades)` печатает список в виде `[5, 3, 5]` - для массива так не получится без `Arrays.toString`.

## Пример 2. Поиск и удаление

```java run
import java.util.ArrayList;

public class Main {
    public static void main(String[] args) {
        ArrayList<String> cart = new ArrayList<>();
        cart.add("хлеб");
        cart.add("молоко");
        cart.add("сыр");
        System.out.println("Есть молоко: " + cart.contains("молоко"));
        System.out.println("Есть чай: " + cart.contains("чай"));
        cart.remove(0);
        System.out.println(cart);
        System.out.println("Пуст: " + cart.isEmpty());
    }
}
```
```output
Есть молоко: true
Есть чай: false
[молоко, сыр]
Пуст: false
```

Разбор:

* Список строк: в `<String>` записан тип элементов. `contains` ищет значение по содержимому.
* `remove(0)` удаляет «хлеб» (индекс 0); «молоко» и «сыр» сдвигаются на одну позицию влево.
* Список может хранить любые типы; в одном списке все элементы одного типа, указанного в скобках.

## Пример 3. Обход и накопление

```java run
import java.util.ArrayList;

public class Main {
    public static void main(String[] args) {
        ArrayList<Integer> prices = new ArrayList<>();
        prices.add(120);
        prices.add(80);
        prices.add(300);

        int sum = 0;
        for (int p : prices) {
            sum += p;
        }
        System.out.println("Сумма: " + sum);

        for (int i = 0; i < prices.size(); i++) {
            System.out.println((i + 1) + ") " + prices.get(i));
        }
    }
}
```
```output
Сумма: 500
1) 120
2) 80
3) 300
```

Разбор:

* For-each работает со списком так же, как с массивом; элемент типа `Integer` автоматически становится `int`.
* В цикле по индексу размер берут вызовом `prices.size()` (со скобками - у списка это метод, а не поле, как `length` у массива).

## Пример 4. Метод, который строит новый список

```java run
import java.util.ArrayList;

public class Main {
    static ArrayList<Integer> onlyPositive(ArrayList<Integer> numbers) {
        ArrayList<Integer> result = new ArrayList<>();
        for (int x : numbers) {
            if (x > 0) {
                result.add(x);
            }
        }
        return result;
    }

    public static void main(String[] args) {
        ArrayList<Integer> data = new ArrayList<>();
        data.add(5);
        data.add(-2);
        data.add(0);
        data.add(7);
        System.out.println(onlyPositive(data));
        System.out.println(data);
    }
}
```
```output
[5, 7]
[5, -2, 0, 7]
```

Разбор:

* Тип списка записывают и в параметре, и в результате: `ArrayList<Integer>`.
* Метод создаёт новый список `result`, добавляет подходящие элементы и возвращает его. Исходный список `data` остаётся прежним.
* Пустой результат выглядит как `[]`. Это нормальное значение, а не ошибка.

## Пример 5. Чтение списка до конца ввода

Программа читает ввод, который в окне примеров пуст, поэтому текст для чтения задан в программе:

```java run
import java.util.ArrayList;
import java.util.Scanner;

public class Main {
    public static void main(String[] args) {
        Scanner in = new Scanner("7 3 9 1");
        ArrayList<Integer> list = new ArrayList<>();
        while (in.hasNextInt()) {
            list.add(in.nextInt());
        }
        System.out.println("Прочитано: " + list.size());
        System.out.println(list);
    }
}
```
```output
Прочитано: 4
[7, 3, 9, 1]
```

Разбор:

* Здесь число элементов заранее не известно, и именно для такой задачи нужен список: в массив пришлось бы сначала читать количество.

**Попробуйте.** Добавьте в пример 3 вызов `prices.remove(0)` перед вычислением суммы. Предскажите сумму и порядок строк в нумерованном списке. Затем напишите метод, который возвращает наибольший элемент списка целых.

<!-- section: precise -->
## Точные правила

* `ArrayList<T>` хранит элементы типа `T` (ссылочного: `Integer`, `Double`, `Boolean`, `Character`, `String`). Примитивные имена в угловых скобках недопустимы.
* Обёртки превращаются в примитивы и обратно автоматически: `int x = list.get(0);` и `list.add(5);` работают без дополнительных действий.
* Индексы от `0` до `size() - 1`. `get`, `set` и `remove` с другим индексом бросают `IndexOutOfBoundsException`. Метод `add(x)` добавляет в конец.
* У списка число элементов - метод `size()`; у массива - поле `length`.
* `remove(int)` удаляет по **индексу**. Чтобы удалить по значению у `ArrayList<Integer>`, пишут `remove(Integer.valueOf(5))`: иначе число считается индексом.
* `contains` и `indexOf` сравнивают содержимое (через `equals`); `indexOf` возвращает индекс первого совпадения или `-1`.
* Менять список во время обхода for-each (добавлять и удалять элементы) нельзя: это приводит к `ConcurrentModificationException`. Для удаления используют цикл по индексу (с учётом сдвига) или собирают результат в новый список, как в примере 4.
* Присваивание `ArrayList<Integer> b = a;` не копирует список: обе переменные ссылаются на один список (как у массивов). Копию создаёт `new ArrayList<>(a)`.
* Список выводится как `[a, b, c]`; пустой - `[]`.
* Передача списка в метод, как и массива, передаёт ссылку: метод может изменить элементы и размер.

<!-- section: mistakes -->
## Типичные ошибки

**1. Нет `import`.**

```java
public class Main {
    public static void main(String[] args) {
        ArrayList<Integer> list = new ArrayList<>();
    }
}
```

```text
Main.java:3: error: cannot find symbol
        ArrayList<Integer> list = new ArrayList<>();
        ^
  symbol:   class ArrayList
  location: class Main
Main.java:3: error: cannot find symbol
        ArrayList<Integer> list = new ArrayList<>();
                                      ^
  symbol:   class ArrayList
  location: class Main
2 errors
```

Как и с `Scanner`: сообщений два на одну ошибку, имя встречается дважды. Добавьте `import java.util.ArrayList;`.

**2. Примитив в угловых скобках.**

```java
import java.util.ArrayList;

public class Main {
    public static void main(String[] args) {
        ArrayList<int> list = new ArrayList<>();
    }
}
```

```text
Main.java:5: error: unexpected type
        ArrayList<int> list = new ArrayList<>();
                  ^
  required: reference
  found:    int
1 error
```

В `<>` нужен ссылочный тип: пишите `Integer`. Для дробных `Double`, для логических `Boolean`.

**3. Квадратные скобки вместо `get`.**

```java
import java.util.ArrayList;

public class Main {
    public static void main(String[] args) {
        ArrayList<Integer> list = new ArrayList<>();
        list.add(5);
        System.out.println(list[0]);
    }
}
```

```text
Main.java:7: error: array required, but ArrayList<Integer> found
        System.out.println(list[0]);
                               ^
1 error
```

Квадратные скобки есть только у массивов. Элемент списка получают методом: `list.get(0)`.

**4. `length` вместо `size()`.**

```java
import java.util.ArrayList;

public class Main {
    public static void main(String[] args) {
        ArrayList<Integer> list = new ArrayList<>();
        System.out.println(list.length);
    }
}
```

```text
Main.java:6: error: cannot find symbol
        System.out.println(list.length);
                               ^
  symbol:   variable length
  location: variable list of type ArrayList<Integer>
1 error
```

У списка нет поля `length`: размер возвращает метод `size()`.

**5. Элемент не того типа.**

```java
import java.util.ArrayList;

public class Main {
    public static void main(String[] args) {
        ArrayList<String> names = new ArrayList<>();
        names.add(42);
    }
}
```

```text
Main.java:6: error: incompatible types: int cannot be converted to String
        names.add(42);
                  ^
Note: Some messages have been simplified; recompile with -Xdiags:verbose to get full output
1 error
```

Список строк принимает только строки. Число придётся превратить в текст самому: `String.valueOf(42)` или `"" + 42`.

**6. Индекс за границей.**

```java
import java.util.ArrayList;

public class Main {
    public static void main(String[] args) {
        ArrayList<Integer> list = new ArrayList<>();
        list.add(10);
        list.add(20);
        System.out.println(list.get(2));
    }
}
```

```text
Exception in thread "main" java.lang.IndexOutOfBoundsException: Index 2 out of bounds for length 2
	...
	at Main.main(Main.java:8)
```

Сообщение называет индекс и размер: в списке два элемента, допустимы индексы 0 и 1.

**7. Удаление во время for-each.**

```java
import java.util.ArrayList;

public class Main {
    public static void main(String[] args) {
        ArrayList<Integer> list = new ArrayList<>();
        list.add(1);
        list.add(2);
        list.add(3);
        list.add(4);
        for (int x : list) {
            if (x == 2) {
                list.remove(0);
            }
        }
    }
}
```

```text
Exception in thread "main" java.util.ConcurrentModificationException
	...
	at Main.main(Main.java:10)
```

Нельзя изменять размер списка, пока он обходится for-each. Соберите нужные элементы в новый список или обходите по индексу.

**8. Удаление по индексу вместо значения.**

```java run
import java.util.ArrayList;

public class Main {
    public static void main(String[] args) {
        ArrayList<Integer> list = new ArrayList<>();
        list.add(10);
        list.add(20);
        list.add(1);
        list.remove(1);
        System.out.println(list);
    }
}
```
```output
[10, 1]
```

Программа хотела удалить число `1`, но `remove(1)` удалил элемент с индексом 1 (то есть 20). Удаление значения: `list.remove(Integer.valueOf(1))`.

<!-- section: summary -->
## Итоги

* `ArrayList<T>` - список переменной длины; тип элементов записывается в угловых скобках, для чисел это `Integer`, `Double`.
* Основные действия: `add`, `get`, `set`, `size`, `contains`, `remove(индекс)`, `isEmpty`.
* Индексы с нуля; выход за границы - `IndexOutOfBoundsException`. Список можно напечатать целиком и обойти через for-each.
* Во время for-each список менять нельзя; новый список из подходящих элементов собирает метод и возвращает его.

Теперь можно хранить много значений; следующая структура связывает значения не с номерами, а с ключами: [HashMap](lesson:java.from-zero.hashmap).

<!-- section: programmer -->
## Для тех, кто уже программирует

* `ArrayList<T>` - обобщённый динамический массив, аналог `list` в Python, `vector` в C++, `slice` в Go и `Array` в JavaScript. Хранит только ссылочные типы: `List<int>` невозможен, элементы упаковываются в `Integer` (дороже по памяти; для больших массивов чисел берут `int[]`).
* Обобщения в Java стираются при компиляции (type erasure): во время работы `ArrayList<Integer>` и `ArrayList<String>` - один и тот же класс.
* `remove(int)` и `remove(Object)` - две разные перегрузки: для `ArrayList<Integer>` `remove(1)` удаляет по индексу, а `remove(Integer.valueOf(1))` - по значению.
* Рекомендуется объявлять переменную интерфейсным типом `List<Integer> list = new ArrayList<>();`; в курсе для простоты используется сам класс.
* Обход во время изменения даёт `ConcurrentModificationException` (fail-fast итератор); безопасное удаление - `Iterator.remove` или `removeIf(x -> x < 0)`.
* `contains` и `indexOf` линейны (O(n)); для частого поиска используют `HashSet` или `HashMap` (следующий урок).
* `equals` сравнивает списки по элементам, в отличие от массивов; `==` по-прежнему сравнивает ссылки.
* Неизменяемый список создают через `List.of(...)`; его `add` бросает `UnsupportedOperationException`.

<!-- section: deep-dive -->
## Как устроен ArrayList

Это необязательный раздел: для заданий он не нужен.

Внутри `ArrayList` хранит обычный массив ссылок и счётчик занятых мест. Когда место заканчивается, список создаёт новый массив примерно в полтора раза больше, копирует в него элементы и продолжает. Поэтому `add` в конец «в среднем» выполняется за постоянное время, хотя отдельное добавление иногда оказывается дорогим. Удаление из начала или середины сдвигает все следующие элементы - это линейная работа.

`Integer` в списке - объект в куче, а не число внутри массива: список из миллиона целых хранит миллион ссылок на миллион объектов (кроме малых значений -128..127, которые Java берёт из общего кеша). Это одна из причин, почему для больших числовых наборов предпочитают `int[]`. Подробнее об упаковке - в курсе `java-first-steps`.
