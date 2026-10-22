<!-- section: simple -->
## Зачем это нужно

В прошлом уроке плохие строки отсеивались прямо в цикле. Но что делать, если проверку выполняет функция? Она получила неподходящие данные и не может вернуть осмысленный результат. Вернуть `0` или `-1` значит смешать «ошибку» с «настоящим ответом»: вызывающий код легко забудет проверить.

Для таких случаев в PHP есть **исключения** (exceptions). Функция сообщает «так работать нельзя», а тот, кто её вызвал, решает, что делать дальше.

## Одна новая идея: бросить исключение и поймать его

* **`throw new InvalidArgumentException("сообщение")`** - бросает исключение. Выполнение функции немедленно прекращается, как при `return`, но результат не возвращается: вместо него «летит» объект ошибки.
* **`try { ... } catch (InvalidArgumentException $e) { ... }`** - ловит. Код в `try` выполняется как обычно; если внутри брошено исключение нужного типа, управление переходит в `catch`, а остаток `try` пропускается.
* **`$e->getMessage()`** - возвращает текст сообщения, заданный при `throw`.

Слово `new` создаёт объект - подробно оно разбирается в уроке про классы. Пока достаточно записи `throw new Тип("сообщение")` целиком.

`InvalidArgumentException` - стандартный тип для «функцию вызвали с недопустимым значением». Исключение, которое никто не поймал, останавливает программу с сообщением `Fatal error: Uncaught ...`.

<!-- section: examples -->
## Пример 1. Бросить и поймать

```php run
<?php
declare(strict_types=1);

function percentOf(int $part, int $whole): int
{
    if ($whole === 0) {
        throw new InvalidArgumentException("целое не может быть нулём");
    }
    return intdiv($part * 100, $whole);
}

try {
    echo percentOf(1, 4), "\n";
    echo percentOf(1, 0), "\n";
    echo "сюда не дойдём\n";
} catch (InvalidArgumentException $e) {
    echo "Ошибка: ", $e->getMessage(), "\n";
}
echo "Программа продолжает работу\n";
```
```output
25
Ошибка: целое не может быть нулём
Программа продолжает работу
```

Разбор: первый вызов вернул 25 и был напечатан. Второй встретил `throw`, функция прервалась, а `echo` с ним не выполнился. Строка «сюда не дойдём» пропущена целиком, управление ушло в `catch`. После блока `catch` программа продолжает работу как обычно.

## Пример 2. Исключения в цикле

```php run
<?php
declare(strict_types=1);

function check(int $n): void
{
    if ($n < 0) {
        throw new InvalidArgumentException("отрицательное: $n");
    }
    echo "ok $n\n";
}

foreach ([3, -1, 5] as $n) {
    try {
        check($n);
    } catch (InvalidArgumentException $e) {
        echo "пропуск (", $e->getMessage(), ")\n";
    }
}
```
```output
ok 3
пропуск (отрицательное: -1)
ok 5
```

Разбор: `try`/`catch` стоит внутри цикла, поэтому одна плохая запись не останавливает остальные. Если бы `try` окружал весь цикл, первое же исключение прервало бы его.

## Пример 3. Ошибки самого языка

```php run
<?php
declare(strict_types=1);

try {
    echo intdiv(10, 0);
} catch (DivisionByZeroError $e) {
    echo get_class($e), ": ", $e->getMessage(), "\n";
}

try {
    throw new Exception("общая");
} catch (InvalidArgumentException $e) {
    echo "не сюда\n";
} catch (Exception $e) {
    echo "сюда: ", $e->getMessage(), "\n";
}
```
```output
DivisionByZeroError: Division by zero
сюда: общая
```

Разбор: PHP сам бросает исключения при некоторых ошибках, например при делении на ноль в `intdiv`. Блоков `catch` может быть несколько: PHP выбирает первый подходящий по типу, поэтому более частные типы пишут раньше общих. `InvalidArgumentException` - частный случай `Exception`, но обычное `Exception` не является `InvalidArgumentException`, поэтому первый блок не сработал.

## Пример 4. Функция, которая проверяет и возвращает

```php
<?php
declare(strict_types=1);

function parseAge(string $text): int
{
    $age = filter_var($text, FILTER_VALIDATE_INT);
    if ($age === false) {
        throw new InvalidArgumentException("не целое число: " . $text);
    }
    return $age;
}

try {
    echo parseAge("34") + 1, "\n";
    echo parseAge("abc") + 1, "\n";
} catch (InvalidArgumentException $e) {
    echo "Ошибка: ", $e->getMessage(), "\n";
}
```

Эта программа напечатает

```
35
Ошибка: не целое число: abc
```

Разбор: функция либо возвращает настоящее число, либо бросает исключение, и «ложного» значения, которое можно спутать с ответом, не существует. Именно этим исключения удобнее специальных значений вроде `-1`.

**Попробуйте.** Добавьте в `parseAge` вторую проверку: возраст меньше нуля тоже должен быть ошибкой с понятным сообщением.

<!-- section: precise -->
## Точные правила

* `throw` - выражение, прерывающее текущую функцию; исключение поднимается вверх по цепочке вызовов, пока не найдёт подходящий `catch`. Если такого нет, программа завершается с `Fatal error: Uncaught ...`.
* Бросать можно только объекты, реализующие `Throwable`; `throw "текст"` вызывает `Error: Can only throw objects`.
* `catch (Тип $e)` ловит исключения этого типа и всех его потомков. Блоки проверяются сверху вниз. Переменную можно не писать: `catch (InvalidArgumentException)`.
* Один `catch` может ловить несколько типов: `catch (TypeError | ValueError $e)`.
* Основные методы: `getMessage()` - текст, `getCode()` - числовой код (по умолчанию 0), `getLine()` - строка в файле.
* Иерархия: `Throwable` делится на `Exception` (ошибки, которые программа ожидает и обрабатывает) и `Error` (ошибки языка: `TypeError`, `DivisionByZeroError`, `ValueError`). Блок `catch (Exception $e)` **не** ловит `Error`.
* `InvalidArgumentException` и `RuntimeException` - стандартные потомки `Exception`; первый подходит для неверных входных данных.
* После `catch` программа продолжается со следующей инструкции. Если нужен код, который выполняется всегда, есть необязательный блок `finally`.
* Сообщение об ошибке пишется для человека: что не так и с каким значением.

<!-- section: mistakes -->
## Типичные ошибки

**1. Исключение не поймано.**

```php
<?php
declare(strict_types=1);

function f(int $n): int
{
    if ($n < 0) {
        throw new InvalidArgumentException("минус");
    }
    return $n;
}
echo f(-1);
```

```text
Fatal error: Uncaught InvalidArgumentException: минус in main.php:7
Stack trace:
#0 main.php(11): f(-1)
#1 {main}
  thrown in main.php on line 7
```

В трассировке читайте снизу вверх: `#1 {main}` - основной код, `#0` - вызов `f(-1)` в строке 11, а `thrown in` указывает, где исключение родилось.

**2. Бросили не объект.**

```php
<?php
throw "ошибка";
```

```text
Fatal error: Uncaught Error: Can only throw objects in main.php:2
```

Нужно писать `throw new InvalidArgumentException("ошибка");`.

**3. `catch` не того типа.** Если брошено `InvalidArgumentException`, а ловится `DivisionByZeroError`, блок не сработает и исключение останется непойманным. Тип в `catch` должен подходить к тому, что бросают.

**4. `Exception` не ловит `Error`.** `intdiv(1, 0)` бросает `DivisionByZeroError`, а он принадлежит ветке `Error`: `catch (Exception $e)` его пропустит. Либо ловите конкретный тип, либо общий `Throwable`.

**5. Пустое сообщение.** `throw new Exception;` создаёт исключение с пустым `getMessage()`, и в отчёте остаётся строка `Ошибка: `. Описывайте проблему и подставляйте значение.

**6. Отлов всего подряд ради тишины.** Блок `catch (Throwable $e) {}` с пустым телом прячет настоящие ошибки программирования. Ловите то, что умеете обработать.

**7. `try` вокруг цикла вместо тела цикла.** Первое же исключение прерывает цикл. Если нужно пропустить плохую запись и продолжить, `try` ставят внутрь цикла.

<!-- section: summary -->
## Итоги

* `throw new Тип("сообщение")` прерывает функцию и сообщает об ошибке, а `try`/`catch` обрабатывает её там, где известно, что делать.
* `$e->getMessage()` возвращает текст, заданный при `throw`.
* Исключение не поймано - программа падает с `Uncaught`; первый подходящий `catch` сверху вниз перехватывает его.
* Функция либо возвращает корректное значение, либо бросает исключение - без «ложных» ответов.

В [следующем уроке](lesson:php.from-zero.classes-intro) вы увидите, как собрать данные и действия над ними в одно целое - в класс.

<!-- section: programmer -->
## Для тех, кто уже программирует

* Синтаксис `try`/`catch`/`finally`/`throw` привычен по Java и JavaScript. Различие: в PHP нет checked exceptions, а функции не объявляют, что бросают.
* Иерархия: `Throwable` - `Exception` и `Error`. Ошибки движка (`TypeError`, `ValueError`, `DivisionByZeroError`, `ArithmeticError`) - потомки `Error`; с PHP 7 их можно ловить.
* Предупреждения и уведомления (`Warning`, `Deprecated`) исключениями не являются. Превратить их в исключения можно через `set_error_handler`.
* `catch (A | B $e)` - несколько типов; переменную в `catch` с PHP 8.0 можно опускать.
* `throw` с PHP 8.0 - выражение: `$x = $y ?? throw new InvalidArgumentException("нет значения");`.
* Свои исключения - классы-наследники `Exception`; цепочки причин создаются третьим аргументом конструктора (`previous`).
* Неперехваченное исключение завершает скрипт с кодом 255.
