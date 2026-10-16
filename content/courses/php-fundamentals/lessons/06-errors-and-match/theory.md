<!-- section: simple -->
## Ошибки как объекты

В PHP 7 и 8 почти всё, что может пойти не так, - объект, который можно поймать. Общий интерфейс называется `Throwable`, у него два семейства:

* `Exception` - «ожидаемые» проблемы окружения и данных: файл не открылся, ввод некорректен, не найден заказ. Это ваш основной инструмент для собственных ошибок.
* `Error` - ошибки программы: неверный тип аргумента (`TypeError`), деление на ноль (`DivisionByZeroError`), вызов несуществующего метода. Обычно их не ловят «на месте», а исправляют код.

Блок `try` / `catch` / `finally` перехватывает исключение, а `finally` выполняется всегда. Рядом стоит выражение `match`: это строгий `switch`, который возвращает значение и сам бросает `UnhandledMatchError`, если подходящей ветки нет.

<!-- section: precise -->
## Точные правила

**Иерархия.**

* `Throwable` (интерфейс) -> `Exception` и `Error`. Реализовать `Throwable` напрямую нельзя, наследуйтесь от `Exception` или `Error`.
* `Exception`: `RuntimeException`, `LogicException` и их потомки (`InvalidArgumentException`, `OutOfRangeException`, `LengthException`, `DomainException`, `UnexpectedValueException`, `JsonException`).
* `Error`: `TypeError` (и его потомок `ArgumentCountError`), `ValueError`, `ArithmeticError` (потомок `DivisionByZeroError`), `UnhandledMatchError`, `AssertionError`.
* Предупреждения (`E_WARNING`, `E_DEPRECATED`) исключениями не являются: их выводит интерпретатор, а их обработчик задаётся через `set_error_handler`. Многие внутренние функции в PHP 8 вместо предупреждения бросают `TypeError` и `ValueError`.

**Сообщение, код, цепочка.** Конструктор `Exception`: `($message = '', $code = 0, ?Throwable $previous = null)`. Методы `getMessage()`, `getCode()`, `getPrevious()`, `getTraceAsString()`, `getFile()`, `getLine()`. Передавайте исходное исключение третьим аргументом, чтобы не потерять причину.

**try / catch / finally.**

* Блоки `catch` проверяются сверху вниз; выполняется первый подходящий. Несколько типов: `catch (TypeError | ValueError $e)`. Специфичные типы ставят выше общих.
* С PHP 8.0 переменную можно опустить: `catch (Exception)`.
* `finally` выполняется при нормальном завершении, при `return`, при выброшенном и при перехваченном исключении. Если `finally` сам делает `return` или `throw`, он **заменяет** результат и исключение из `try`; поэтому `return` внутри `finally` практически всегда ошибка.
* `throw` с PHP 8.0 - выражение: `$x = $v ?? throw new InvalidArgumentException('no value');`.
* Не пойманное исключение завершает скрипт фатальной ошибкой `Uncaught ...`; фатальные ошибки времени компиляции (например, неверное объявление класса) перехватить нельзя.

**Собственные исключения.** Наследуйтесь от подходящего базового класса, добавляйте поля и именованные конструкторы:
`final class NotFound extends RuntimeException { public static function id(int $id): self { return new self("order $id not found"); } }`. Не ловите `Throwable` без причины, а `Exception` только на границе (верхний уровень, обработчик запроса).

**`match`.**

* `match ($x) { 1, 2 => 'low', 3 => 'mid', default => 'other' }` сравнивает **строго** (`===`), в отличие от `switch`.
* Нет «проваливания» между ветками и не нужен `break`; ветка - одно выражение. Ветки проверяются по порядку.
* Если ни одна ветка не подошла и нет `default`, бросается `UnhandledMatchError` с описанием значения.
* `match (true) { $n < 0 => 'neg', $n === 0 => 'zero', default => 'pos' }` заменяет цепочку `if / elseif`.

<!-- section: examples -->
## Примеры

**Пример 1. Иерархия, порядок catch и цепочка.**

```php run
<?php
class AppException extends RuntimeException {}
final class NotFound extends AppException
{
    public static function id(int $id): self
    {
        return new self("order $id not found", 404);
    }
}

function find(int $id): array
{
    $orders = [1 => ['total' => 10]];
    return $orders[$id] ?? throw NotFound::id($id);
}

foreach ([1, 2] as $id) {
    try {
        $o = find($id);
        echo "found ", $o['total'], PHP_EOL;
    } catch (NotFound $e) {
        echo get_class($e), ' ', $e->getCode(), ' ', $e->getMessage(), PHP_EOL;
    } catch (AppException | LogicException $e) {
        echo 'other', PHP_EOL;
    }
}

try {
    try {
        intdiv(1, 0);
    } catch (DivisionByZeroError $e) {
        throw new AppException('wrapped', 0, $e);
    }
} catch (AppException $e) {
    echo $e->getMessage(), ' <- ', get_class($e->getPrevious()), ': ', $e->getPrevious()->getMessage(), PHP_EOL;
}

var_dump(new TypeError() instanceof Exception);
var_dump(new TypeError() instanceof Throwable);
var_dump(new DivisionByZeroError() instanceof ArithmeticError);
var_dump(new InvalidArgumentException() instanceof LogicException);
```
```output
found 10
NotFound 404 order 2 not found
wrapped <- DivisionByZeroError: Division by zero
bool(false)
bool(true)
bool(true)
bool(true)
```

**Пример 2. Что бросает PHP 8 вместо предупреждений.**

```php run
<?php
declare(strict_types=1);

function half(int $n): int { return intdiv($n, 2); }

$cases = [
    'type'    => fn () => half('4'),
    'zero'    => fn () => 10 % 0,
    'divide'  => fn () => 1 / 0,
    'value'   => fn () => array_chunk([1, 2], 0),
    'args'    => fn () => half(),
    'json'    => fn () => json_decode('{bad', false, 512, JSON_THROW_ON_ERROR),
    'match'   => fn () => match (3) { 1 => 'a', 2 => 'b' },
];

foreach ($cases as $name => $fn) {
    try {
        $fn();
    } catch (Throwable $e) {
        echo str_pad($name, 7), get_class($e), PHP_EOL;
    }
}
```
```output
type   TypeError
zero   DivisionByZeroError
divide DivisionByZeroError
value  ValueError
args   ArgumentCountError
json   JsonException
match  UnhandledMatchError
```

**Пример 3. finally: порядок выполнения и подмена результата.**

```php run
<?php
function a(): string
{
    try {
        return 'try';
    } finally {
        echo '[finally a]';
    }
}

function b(): string
{
    try {
        throw new Exception('lost');
    } catch (Exception) {
        return 'catch';
    } finally {
        echo '[finally b]';
    }
}

function c(): string
{
    try {
        return 'try';
    } finally {
        return 'finally';
    }
}

function d(): string
{
    try {
        throw new Exception('first');
    } finally {
        echo '[finally d]';
    }
}

echo a(), PHP_EOL;
echo b(), PHP_EOL;
echo c(), PHP_EOL;
try {
    d();
} catch (Exception $e) {
    echo $e->getMessage(), PHP_EOL;
}
```
```output
[finally a]try
[finally b]catch
finally
[finally d]first
```

**Пример 4. match: строгое сравнение и ошибка без default.**

```php run
<?php
function kind(mixed $v): string
{
    return match (true) {
        is_int($v), is_float($v) => 'number',
        is_string($v)            => 'string',
        is_array($v)             => 'array',
        default                  => 'other',
    };
}

foreach ([1, 1.5, 'x', [], null] as $v) {
    echo kind($v), ' ';
}
echo PHP_EOL;

$code = '1';
echo match ($code) {
    1       => 'int one',
    '1'     => 'string one',
    default => 'unknown',
}, PHP_EOL;

try {
    echo match (5) { 1, 2 => 'low', 3 => 'mid' };
} catch (\UnhandledMatchError $e) {
    echo get_class($e), ': ', $e->getMessage(), PHP_EOL;
}
```
```output
number number string array other 
string one
UnhandledMatchError: Unhandled match case 5
```

<!-- section: mistakes -->
## Типичные ошибки

* **Ловить `Exception` и ожидать `TypeError` или `DivisionByZeroError`.** Это потомки `Error`, а не `Exception`. Чтобы поймать всё, нужен `Throwable`, но делайте это только на верхнем уровне.
* **Пустой `catch`.** Проглоченное исключение прячет баг; как минимум запишите в журнал или перебросьте.
* **Терять причину.** `throw new AppException('failed')` внутри `catch` без `previous` стирает исходную трассировку; передавайте `$e` третьим аргументом.
* **`return` или `throw` в `finally`.** Они молча заменяют результат или исключение из `try`.
* **Ставить общий `catch` выше специфичного.** Специфичный блок никогда не выполнится.
* **Использовать `match` как `switch` со слабым сравнением.** `match ('1')` не совпадёт с веткой `1`: сравнение строгое.
* **Забыть `default` в `match`.** Если значение не покрыто, получите `UnhandledMatchError`; это удобно для перечислений (компилятор не проверяет полноту, но ошибка видна сразу).
* **Использовать исключения для обычного потока управления.** Отсутствие значения в словаре - не исключение: используйте `??` или `array_key_exists`.

<!-- section: summary -->
## Итоги

* `Throwable` объединяет `Exception` (ваши ошибки) и `Error` (ошибки программы: `TypeError`, `ValueError`, `DivisionByZeroError`, `UnhandledMatchError`).
* Блоки `catch` сверху вниз; можно перечислять типы через `|` и опускать переменную; `finally` выполняется всегда и может заменить результат.
* Передавайте `previous`, чтобы сохранять цепочку причин; `throw` - выражение с PHP 8.0.
* `match` сравнивает строго, возвращает значение и без совпадения бросает `UnhandledMatchError`.

Дальше: [пространства имён, автозагрузка и генераторы](lesson:php.fundamentals.namespaces-autoload-generators).
Назад: [интерфейсы, трейты и позднее статическое связывание](lesson:php.fundamentals.interfaces-traits-lsb).
Сравните: [исключения в Java](lesson:java.fundamentals.exceptions-try-with-resources), [исключения в C#](lesson:csharp.fundamentals.exceptions-and-resources), [ошибки в JavaScript](lesson:javascript.fundamentals.errors-and-exceptions), [обработка ошибок в Kotlin](lesson:kotlin.fundamentals.error-handling), [ошибки и файлы в Python](lesson:python.fundamentals.errors-and-files).
