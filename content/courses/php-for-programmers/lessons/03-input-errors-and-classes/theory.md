<!-- section: simple -->
## Зачем этот урок

Как только программа читает чужие данные, она сталкивается с мусором: пустые строки, `\r\n`, число с точкой вместо запятой, забытое поле. В PHP к этому добавляются свои особенности: `fgets` возвращает строку вместе с переводом строки и `false` в конце, предупреждения не прерывают программу, а исключения делятся на `Exception` и `Error`. Классы выглядят знакомо, но объект хранится как идентификатор, а типизированные свойства бывают «неинициализированными». Успешное прохождение (упражнение и квиз) засчитывает уроки 21-23 курса «PHP с нуля».

## Главная мысль

Данные из ввода - это не доверенные значения. Их читают построчно, обрезают (`trim`), проверяют (`filter_var`, `is_numeric`, длина) и при несоответствии бросают исключение с понятным сообщением. Ошибку данных ловят и считают (`InvalidArgumentException`), ошибку программиста (`TypeError`, `DivisionByZeroError`) не прячут. Класс отвечает за свою целостность: проверка в конструкторе, `readonly` там, где значение не должно меняться.

<!-- section: examples -->
## Пример 1. Чтение до конца ввода

```php run
<?php
declare(strict_types=1);

function lines(string $content, bool $strict): array
{
    $stream = fopen('php://memory', 'w+');
    fwrite($stream, $content);
    rewind($stream);
    $result = [];
    if ($strict) {
        while (($line = fgets($stream)) !== false) {
            $result[] = $line;
        }
    } else {
        while ($line = fgets($stream)) {
            $result[] = $line;
        }
    }
    return $result;
}

echo json_encode(lines("5\r\nabc\n0", true)), "\n";
echo json_encode(lines("5\r\nabc\n0", false)), "\n";
echo json_encode(lines("", true)), json_encode(explode(",", "")), json_encode(array_map('trim', lines("5\r\nabc\n0", true))), "\n";
```
```output
["5\r\n","abc\n","0"]
["5\r\n","abc\n"]
[][""]["5","abc","0"]
```

Для наглядности ввод здесь идёт из памяти (`php://memory`), а в программе тот же цикл читает `STDIN`. `fgets` возвращает строку **вместе** с `\n` (и `\r`, если файл в формате Windows), а в конце ввода - `false`. Последняя строка без перевода строки - это `"0"`, и она ложна, поэтому цикл `while ($line = fgets(...))` без `!== false` теряет её. `trim` убирает пробелы, `\r` и `\n` с обоих краёв. Пустая строка при `explode` даёт массив из одной пустой строки, а не пустой массив: перед разбором проверяйте `$line === ""`.

## Пример 2. Что бросается само, а что нет

```php run
<?php
declare(strict_types=1);

$tests = [
    'деление' => fn() => 1 / 0,
    'остаток' => fn() => 1 % 0,
    'intdiv' => fn() => intdiv(1, 0),
    'fdiv' => fn() => fdiv(1, 0),
    'приведение' => fn() => (int) "abc",
    'json_decode' => fn() => json_decode("{bad"),
    'json THROW' => fn() => json_decode("{bad", flags: JSON_THROW_ON_ERROR),
    'str_repeat' => fn() => str_repeat("x", -1),
];
foreach ($tests as $name => $test) {
    try {
        $value = var_export($test(), true);
        echo $name, ": ", $value, "\n";
    } catch (Throwable $e) {
        echo $name, ": ", get_class($e), " - ", $e->getMessage(), "\n";
    }
}
```
```output
деление: DivisionByZeroError - Division by zero
остаток: DivisionByZeroError - Modulo by zero
intdiv: DivisionByZeroError - Division by zero
fdiv: INF
приведение: 0
json_decode: NULL
json THROW: JsonException - Syntax error
str_repeat: ValueError - str_repeat(): Argument #2 ($times) must be greater than or equal to 0
```

Деление на ноль и неверные аргументы встроенных функций в PHP 8 бросают исключения (`DivisionByZeroError`, `ValueError`, `TypeError`). `fdiv` возвращает `INF` без исключения. Приведение `(int) "abc"` ничего не проверяет и даёт `0`. Функции, которые исторически возвращают значение-признак, молчат: `json_decode` при ошибке возвращает `null`, а исключение даёт только флаг `JSON_THROW_ON_ERROR`. Предупреждения (`Warning`, `Deprecated`) исключениями не являются и не прерывают выполнение.

## Пример 3. Exception, Error и порядок finally

```php run
<?php
declare(strict_types=1);

final class QuantityException extends InvalidArgumentException
{
    public function __construct(public readonly string $text)
    {
        parent::__construct("неверное количество: {$text}");
    }
}

function quantity(string $text): int
{
    $n = filter_var($text, FILTER_VALIDATE_INT);
    if ($n === false || $n < 0) {
        throw new QuantityException($text);
    }
    return $n;
}

function attempt(callable $f): string
{
    try {
        return "ok " . $f();
    } catch (QuantityException $e) {
        return "quantity " . $e->text;
    } catch (InvalidArgumentException | DivisionByZeroError $e) {
        return "other " . get_class($e);
    } catch (Exception $e) {
        return "exception " . $e->getMessage();
    } catch (Error $e) {
        return "error " . get_class($e);
    } finally {
        echo "[finally] ";
    }
}

echo attempt(fn() => quantity("5")), "\n";
echo attempt(fn() => quantity("x")), "\n";
echo attempt(fn() => intdiv(1, 0)), "\n";
echo attempt(fn() => throw new RuntimeException("r")), "\n";
echo attempt(fn() => strlen([])), "\n";
```
```output
[finally] ok 5
[finally] quantity x
[finally] other DivisionByZeroError
[finally] exception r
[finally] error TypeError
```

У `Throwable` два наследника: `Exception` (ошибки, которые программа может ожидать: неверные данные, недоступный файл) и `Error` (`TypeError`, `DivisionByZeroError`, `ValueError` - ошибки программиста). `catch (Exception $e)` `TypeError` **не ловит**. Блоки `catch` проверяются сверху вниз, поэтому сначала идут частные типы, затем общие; через `|` перечисляют несколько типов. `finally` выполняется всегда, даже если в `try` уже стоит `return`: поэтому `[finally]` напечатано раньше результата, который вернула функция. Собственное исключение наследуют от подходящего стандартного класса (`InvalidArgumentException`, `RuntimeException`) и передают причину последним аргументом (`previous`), если оборачивают чужое.

## Пример 4. Объекты: идентификатор, clone и readonly

```php run
<?php
declare(strict_types=1);

class Point
{
    public function __construct(public int $x = 0, public int $y = 0)
    {
    }
}

final class Money
{
    public function __construct(public readonly int $amount)
    {
    }
}

class Holder
{
    public int $value;
    public ?string $note = null;
}

$a = new Point(1, 2);
$b = $a;
$b->x = 10;
$c = clone $a;
$c->x = 20;
echo $a->x, " ", $b->x, " ", $c->x, "\n";
echo json_encode([$a == new Point(10, 2), $a === new Point(10, 2), $a === $b]), "\n";

$money = new Money(5);
try {
    $money->amount = 6;
} catch (Error $e) {
    echo $e->getMessage(), "\n";
}
try {
    echo (new Holder())->value;
} catch (Error $e) {
    echo $e->getMessage(), "\n";
}
try {
    new Point("5");
} catch (TypeError $e) {
    echo strstr($e->getMessage(), ", called", true), "\n";
}
```
```output
10 10 20
[true,false,true]
Cannot modify readonly property Money::$amount
Typed property Holder::$value must not be accessed before initialization
Point::__construct(): Argument #1 ($x) must be of type int, string given
```

Переменная с объектом хранит **идентификатор**, а не копию: `$b = $a` указывает на тот же объект, и изменение через `$b` видно в `$a` (в отличие от массивов). Независимая копия - `clone`. Оператор `==` сравнивает класс и значения свойств, `===` - идентичность (один и тот же объект). Конструктор с **продвижением** (`public int $x`) сразу объявляет свойство и присваивает ему аргумент. Свойство `readonly` можно записать один раз, затем попытка изменить даёт `Error`. Типизированное свойство без значения по умолчанию остаётся **неинициализированным**: чтение до записи - `Error`, а не `null`. Типы свойств и параметров конструктора проверяются так же, как у функций.

<!-- section: precise -->
* `fgets(STDIN)` возвращает строку с `\n` или `false` в конце ввода. Цикл: `while (($line = fgets(STDIN)) !== false)`. `trim` убирает пробелы, `\n`, `\r`, `\t`. Для всего ввода сразу есть `stream_get_contents(STDIN)`.
* Проверка числа: `filter_var($s, FILTER_VALIDATE_INT)` (целое или `false`), `is_numeric` (десятичное число, экспонента, пробелы по краям). Приведения `(int)`, `intval` ничего не проверяют.
* Иерархия: `Throwable` -> `Exception` (`InvalidArgumentException`, `RuntimeException`, ...) и `Error` (`TypeError`, `ValueError`, `ArithmeticError`, `DivisionByZeroError`, `ArgumentCountError`).
* `try` требует хотя бы один `catch` или `finally`. Несколько типов: `catch (A | B $e)`. `finally` выполняется всегда, в том числе после `return`; если `finally` сам делает `return`, он подменяет результат.
* Встроенные функции могут сообщать об ошибке значением (`false`, `null`) или предупреждением вместо исключения; проверяйте результат явно.
* Класс: свойства с типами, `__construct` с продвижением (`public int $x`), методы, `$this`, `new`, `->`. Видимость: `public`, `protected`, `private`.
* `readonly`-свойство инициализируется один раз изнутри класса; изменение даёт `Error`. Неинициализированное типизированное свойство нельзя читать.
* Объекты присваиваются и передаются как идентификатор; `clone` делает поверхностную копию. `==` сравнивает свойства, `===` - тождество.
* Исключения, которые никто не поймал, завершают программу сообщением `Fatal error: Uncaught ...` в stderr.

<!-- section: mistakes -->
**1. `while ($line = fgets(STDIN))` без сравнения с `false`.** Цикл остановится на последней строке `0`, если она записана без перевода строки (`"0"` ложна). Строка `0` в середине ввода сохраняется, потому что `"0\n"` истинна, поэтому ошибка проявляется редко и неожиданно. Пишите `!== false`.

**2. Ловить `Exception`, ожидая `TypeError`.**

```php
<?php
declare(strict_types=1);
function parse(int $x): int
{
    return $x;
}
try {
    parse("a");
} catch (Exception $e) {
    echo "поймали\n";
}
```

```text
Fatal error: Uncaught TypeError: parse(): Argument #1 ($x) must be of type int, string given, called in main.php on line 8 and defined in main.php:3
```

`TypeError` - это `Error`, а не `Exception`. Если нужно поймать оба вида, пишите `catch (Throwable $e)` на самой внешней границе программы.

**3. Чтение неинициализированного свойства.**

```php
<?php
declare(strict_types=1);
class Holder
{
    public int $value;
}
$holder = new Holder();
echo $holder->value;
```

```text
Fatal error: Uncaught Error: Typed property Holder::$value must not be accessed before initialization in main.php:8
```

Задавайте значение в конструкторе или значение по умолчанию.

**4. Запись в `readonly`-свойство.**

```php
<?php
declare(strict_types=1);
final class Money
{
    public function __construct(public readonly int $amount)
    {
    }
}
$money = new Money(5);
$money->amount = 6;
```

```text
Fatal error: Uncaught Error: Cannot modify readonly property Money::$amount in main.php:10
```

**5. Забытое `new`.**

```php
<?php
declare(strict_types=1);
class Point
{
    public function __construct(public int $x = 0)
    {
    }
}
$p = Point(1);
```

```text
Fatal error: Uncaught Error: Call to undefined function Point() in main.php:9
```

**6. Молчаливое проглатывание ошибок.** Пустой `catch (Throwable $e) {}` скрывает и ошибки данных, и ошибки программиста. Ловите конкретный тип, считайте или логируйте ошибку и продолжайте только если это осмысленно. Функции вроде `json_decode` при плохих данных возвращают `null`: используйте `JSON_THROW_ON_ERROR` или проверяйте `json_last_error()`.

<!-- section: summary -->
Теперь вы знаете, как читать ввод до конца и не потерять строку `0`, чем `Exception` отличается от `Error`, как работают `finally` и `catch` с несколькими типами, что объект хранится как идентификатор, и как классы с продвижением свойств и `readonly` защищают свои данные. Дальше - [Как выполняется PHP](lesson:php.first-steps.how-php-runs): путь файла через Zend Engine, опкоды и ввод-вывод. Для подробностей: [Чтение всего ввода](lesson:php.from-zero.reading-all-input), [Исключения](lesson:php.from-zero.exceptions), [Первый класс](lesson:php.from-zero.classes-intro).

<!-- section: programmer -->
## Главное для тех, кто уже программирует

* Ввод в CLI: `fgets(STDIN)` возвращает строку с `\n` и `false` в конце. Всегда `!== false`, иначе потеряется строка `"0"`. Конец строки может быть `\r\n`: `trim`.
* Проверка ввода - явная: `filter_var` или `is_numeric`; `(int)` не проверяет. Результат `filter_var` сравнивайте с `false` строго (`0` - корректное число).
* Иерархия `Throwable` -> `Exception` / `Error`. `TypeError` и `DivisionByZeroError` - это `Error`; `catch (Exception)` их не ловит. Многотиповой catch - `A | B`.
* `finally` выполняется и после `return`; ошибки встроенных функций бывают предупреждением или значением `false`/`null`, а не исключением.
* Классы: продвижение свойств в конструкторе, `readonly`, типизированные свойства (может быть «не инициализировано»), `final`.
* Объекты - ссылочные идентификаторы, массивы - значения. `clone` поверхностный; `==` сравнивает поля, `===` - тождество объектов.
* Неперехваченное исключение завершает программу сообщением в stderr; stdout при этом уже мог быть частично напечатан.
