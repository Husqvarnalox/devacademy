<!-- section: simple -->
## Зачем этот урок

Функции в PHP выглядят привычно: параметры, типы, `return`. Различия прячутся в деталях: строгий режим касается только вызовов из того же файла, массивы передаются по значению (но дёшево), а сам массив - не список и не словарь, а **упорядоченная карта** с правилами нормализации ключей. Урок собирает те места, где опыт другого языка подсказывает неверный ответ. Успешное прохождение (упражнение и квиз) засчитывает уроки 16-20 курса «PHP с нуля».

## Главная мысль

Тип `array` один на все случаи: список, словарь, очередь и стек. Он хранит пары «ключ - значение» в порядке вставки, ключ - это `int` или `string`, а присваивание и передача в функцию **копируют** значение (физическое копирование откладывается до первой записи). Из этого вытекает большинство особенностей: `sort` меняет массив на месте и перенумеровывает ключи, `unset` дыр не закрывает, `array_unique` и `array_filter` ключи сохраняют, а `json_encode` по ключам решает, список перед ним или объект.

<!-- section: examples -->
## Пример 1. Типы, значения по умолчанию и граница strict_types

```php run
<?php
declare(strict_types=1);

function double(int $n): int
{
    return $n * 2;
}

function half(float $x): float
{
    return $x / 2;
}

function describe(int|string $v, ?string $unit = null, string ...$tags): string
{
    return get_debug_type($v) . ($unit ?? "-") . count($tags);
}

echo double(4), " ", half(3), " ", describe(5), " ", describe("a", "kg", "x", "y"), " ", describe(unit: "m", v: 1), " ", double(...[3]), "\n";
echo implode(",", array_map('double', ["4", "5"])), "\n";
foreach (["4", 4.5] as $bad) {
    try {
        echo double($bad);
    } catch (TypeError $e) {
        echo strstr($e->getMessage(), ", called", true), "\n";
    }
}
```
```output
8 1.5 int-0 stringkg2 intm0 6
8,10
double(): Argument #1 ($n) must be of type int, string given
double(): Argument #1 ($n) must be of type int, float given
```

Типы параметров и результата проверяются при вызове. В строгом режиме допустимо единственное расширение: `int` можно передать туда, где ждут `float` (`half(3)`). Строка `"4"` и число `4.5` для `int` отвергаются с `TypeError`. Но проверка зависит от того, **кто вызывает**: `array_map` - встроенная функция, и она вызывает `double` в нестрогом режиме, поэтому `"4"` превращается в `4`. Строгий режим действует на вызовы, написанные в файле с `declare`; результат, который функция возвращает, проверяется по режиму файла, где функция объявлена. Остальное из примера: `?string` - строка или `null`; `int|string` - объединённый тип; `string ...$tags` собирает остаток аргументов в массив; именованные аргументы позволяют пропустить необязательные; `...[3]` раскладывает массив в аргументы.

## Пример 2. Массив передаётся по значению

```php run
<?php
declare(strict_types=1);

function withExtra(array $items): array
{
    $items[] = 99;
    return $items;
}

function addExtra(array &$items): void
{
    $items[] = 99;
}

$a = [1, 2, 3];
$b = $a;
$b[] = 4;
echo count($a), count($b), "\n";

$c = withExtra($a);
echo count($a), count($c), "\n";

addExtra($a);
echo count($a), "\n";

$nested = ["inner" => [1, 2]];
$copy = $nested;
$copy["inner"][] = 3;
echo json_encode($nested), json_encode($copy), "\n";
```
```output
34
34
4
{"inner":[1,2]}{"inner":[1,2,3]}
```

После `$b = $a` это независимые массивы: запись в `$b` не затрагивает `$a`. Так же ведёт себя аргумент функции - внутри неё изменяется копия. Реальное копирование происходит лениво, при первой записи (copy-on-write), поэтому передача большого массива в функцию, которая его только читает, ничего не стоит. Чтобы функция изменила массив вызывающего кода, нужна ссылка `&` в параметре; такая функция обычно возвращает `void`. Вложенные массивы копируются так же, как внешний. Объекты ведут себя иначе: переменная хранит идентификатор объекта, поэтому при присваивании копируется именно он (подробнее - в следующем уроке).

## Пример 3. Ключи: нормализация и дыры

```php run
<?php
declare(strict_types=1);

$mixed = ["1" => "a", 1 => "b", true => "c", "01" => "d", "x" => "e"];
echo json_encode($mixed), " ", count($mixed), "\n";

$list = [10, 20, 30];
unset($list[1]);
echo json_encode($list), " ", json_encode(array_values($list)), " ", var_export(array_is_list($list), true), "\n";
$list[] = 40;
echo json_encode($list), "\n";

echo json_encode([]), json_encode(["a" => 1]), json_encode([1 => "x", 2 => "y"]), json_encode([0 => "x", 1 => "y"]), "\n";
```
```output
{"1":"c","01":"d","x":"e"} 3
{"0":10,"2":30} [10,30] false
{"0":10,"2":30,"3":40}
[]{"a":1}{"1":"x","2":"y"}["x","y"]
```

Ключ `"1"` (десятичная запись целого числа) становится `int` `1`, `true` тоже становится `1`, поэтому три первые записи пишут в один элемент и остаётся последнее значение. Строка `"01"` целым не считается и остаётся строкой. После `unset($list[1])` массив содержит ключи `0` и `2`; новый элемент через `[]` получает ключ «максимальный целый ключ + 1». `json_encode` печатает **список** (`[...]`), только если ключи идут как `0, 1, 2...`; иначе получается объект `{...}`. Поэтому проверка `array_is_list` и `array_values` важны на границе с JSON.

## Пример 4. Сортировка и фильтры

```php run
<?php
declare(strict_types=1);

$n = [3, 1, 2, 3];
echo var_export(sort($n), true), json_encode($n), "\n";

$unique = array_unique([3, 1, 3, 2]);
echo json_encode($unique), json_encode(array_values($unique)), "\n";
echo json_encode(array_filter([1, 2, 3, 4], fn(int $x): bool => $x % 2 === 0)), "\n";

$prices = ["b" => 2, "a" => 3, "c" => 1];
asort($prices);
echo json_encode($prices);
ksort($prices);
echo json_encode($prices), "\n";

$people = [["name" => "Ян", "age" => 30], ["name" => "Ада", "age" => 30], ["name" => "Лев", "age" => 25]];
usort($people, fn(array $x, array $y): int => [$y["age"], $x["name"]] <=> [$x["age"], $y["name"]]);
echo implode(",", array_column($people, "name")), "\n";
```
```output
true[1,2,3,3]
{"0":3,"1":1,"3":2}[3,1,2]
{"1":2,"3":4}
{"c":1,"b":2,"a":3}{"a":3,"b":2,"c":1}
Ада,Ян,Лев
```

`sort` сортирует **на месте**, возвращает `true` и перенумеровывает ключи; `asort`/`ksort` сортируют по значениям и по ключам, сохраняя пары. Результат `array_unique` и `array_filter` сохраняет исходные ключи: после них индексов может не хватать, и `json_encode` напечатает объект. Для списка добавьте `array_values`. `usort` принимает функцию сравнения, возвращающую отрицательное число, ноль или положительное; удобно сравнивать массивы через `<=>`, чтобы упорядочить по нескольким полям.

## Пример 5. Нестрогий поиск и ссылка после foreach

```php run
<?php
declare(strict_types=1);

var_dump(in_array("1e1", ["10"]), in_array("1e1", ["10"], true), in_array(0, ["a"]));
var_dump(array_search("a", ["a", "b"]), array_search("z", ["a", "b"]));

$found = array_search("a", ["a", "b"]);
echo $found ? "нашли" : "не нашли", "\n";

$list = [1, 2, 3];
foreach ($list as &$x) {
    $x *= 2;
}
foreach ($list as $x) {
}
echo json_encode($list), "\n";
unset($x);

$items = [1, 2];
foreach ($items as $v) {
    if ($v === 1) {
        $items[] = 3;
    }
}
echo count($items), "\n";
```
```output
bool(true)
bool(false)
bool(false)
int(0)
bool(false)
не нашли
[2,4,4]
3
```

`in_array` и `array_search` по умолчанию сравнивают через `==`: `"1e1"` «находится» среди `"10"`. Третий аргумент `true` делает сравнение строгим. `array_search` возвращает **ключ** или `false`; для первого элемента ключ равен `0`, поэтому `if ($found)` ошибается, а надо сравнивать `$found === false`. Цикл `foreach ($list as &$x)` оставляет `$x` ссылкой на последний элемент; следующий `foreach ($list as $x)` записывает в эту ссылку значения по очереди, и последний элемент превращается в предыдущий (`[2,4,4]`). После цикла по ссылке пишите `unset($x)`. Обычный `foreach` по значению обходит снимок массива: добавленный элемент в этом проходе не виден.

<!-- section: precise -->
* Параметры и результат: `int`, `float`, `string`, `bool`, `array`, `?T`, `A|B`, `mixed`, `void`. Параметры по умолчанию должны идти после обязательных. Остаточный параметр `...$rest` собирает аргументы в массив; `...$array` в вызове раскладывает массив. Именованные аргументы: `f(unit: "m")`.
* `declare(strict_types=1);` действует на вызовы функций и возвраты в том файле, где объявлено. Исключение: `int` принимается как `float`. Вызовы из встроенных функций (`array_map`, `usort`) идут в нестрогом режиме.
* Область видимости: функция не видит переменных снаружи (нужен параметр, `global` или `use` у замыкания); локальные переменные исчезают после возврата.
* Массив - упорядоченная карта; ключи `int` или `string`. Строки вида `"12"`, булевы значения и `null` (`""`) приводятся к ключам, `"012"` и `"1.5"` остаются строками. Новый ключ через `[]` - максимальный целый ключ плюс один.
* Массивы присваиваются и передаются по значению (copy-on-write); `&` создаёт ссылку. Вложенные массивы копируются вместе с внешним.
* `sort`, `rsort`, `usort`, `shuffle` изменяют массив и перенумеровывают ключи; `asort`, `arsort`, `ksort`, `uasort` ключи сохраняют. `array_unique`, `array_filter`, `array_slice` (для строковых ключей) сохраняют ключи, `array_values` их обнуляет. Сортировка устойчива с PHP 8.0.
* `count` принимает только массив или `Countable`; `isset($a["k"])` ложно и для ключа со значением `null`, `array_key_exists` отвечает на вопрос о ключе.
* `in_array($x, $a)` и `array_search($x, $a)` используют `==`; строгий режим сравнения - третий аргумент `true`.

<!-- section: mistakes -->
**1. Чтение несуществующего ключа.**

```php
<?php
declare(strict_types=1);
$stock = ["apple" => 3];
echo $stock["pear"];
echo "готово\n";
```

```text
Warning: Undefined array key "pear" in main.php on line 4
```

Это не исключение, а предупреждение: программа продолжает работу с `null`. Безопасно: `$stock["pear"] ?? 0` или `array_key_exists`.

**2. Не хватает аргументов.**

```php
<?php
declare(strict_types=1);
function total(int $price, int $qty): int
{
    return $price * $qty;
}
echo total(5);
```

```text
Fatal error: Uncaught ArgumentCountError: Too few arguments to function total(), 1 passed in main.php on line 7 and exactly 2 expected in main.php:3
```

**3. Тип аргумента в строгом режиме.**

```php
<?php
declare(strict_types=1);
function total(int $price, int $qty): int
{
    return $price * $qty;
}
echo total("5", 2);
```

```text
Fatal error: Uncaught TypeError: total(): Argument #1 ($price) must be of type int, string given, called in main.php on line 7 and defined in main.php:3
```

Данные из ввода и ключи массива (`"7"` становится `int`, но `"07"` нет) приходят в неожиданном типе: приводите значения явно или проверяйте их до вызова.

**4. Внешняя переменная внутри функции.**

```php
<?php
declare(strict_types=1);
$rate = 10;
function withRate(int $x): int
{
    return $x * $rate;
}
echo withRate(5);
```

```text
Warning: Undefined variable $rate in main.php on line 6
```

Предупреждение `Undefined variable` и результат `0`: функция не видит `$rate`. Передайте значение параметром.

**5. `count` от не-массива.**

```php
<?php
declare(strict_types=1);
function firstWord(string $text): ?string
{
    return $text === "" ? null : explode(" ", $text)[0];
}
echo count(firstWord("hello world"));
```

```text
Fatal error: Uncaught TypeError: count(): Argument #1 ($value) must be of type Countable|array, string given in main.php:7
```

Начиная с PHP 8 `count()` для не-массива бросает `TypeError`.

**6. Забытые `array_values` и `unset($x)`.** После `array_filter`/`array_unique` индексы не идут подряд и `json_encode` печатает объект; после `foreach ($list as &$x)` последний элемент массива остаётся связанным с `$x` (разобрано в примере 5).

<!-- section: summary -->
Теперь вы знаете, как работают типы параметров и граница `strict_types`, чем массив-значение отличается от ссылки, как нормализуются ключи и почему `sort`, `unset`, `array_unique` и `array_filter` ведут себя по-разному с ключами. Следующий шаг - ввод, исключения и классы: [Ввод, ошибки и классы](lesson:php.programmers.input-errors-and-classes). Для подробностей: [Функции](lesson:php.from-zero.functions), [Массивы](lesson:php.from-zero.arrays), [Функции массивов](lesson:php.from-zero.array-functions), [Ассоциативные массивы](lesson:php.from-zero.associative-arrays), [Функции строк](lesson:php.from-zero.string-functions).

<!-- section: programmer -->
## Главное для тех, кто уже программирует

* Типы параметров проверяются при вызове; `strict_types=1` включает строгие проверки только для вызовов из файла с директивой. Встроенные функции вызывают ваши колбэки в нестрогом режиме.
* Единственный «массив» - упорядоченная карта. Нет отдельных списков и словарей; список - это ключи `0..n-1` (`array_is_list`).
* Присваивание и аргумент функции копируют массив (лениво); ссылка - только явная `&`. Объекты, в отличие от массивов, передаются как идентификатор.
* Ключи нормализуются: `"1"` -> `1`, `true` -> `1`, `null` -> `""`; `"01"` и `"1.5"` остаются строками. Смешанные ключи легко склеить случайно.
* `sort` на месте и перенумеровывает; `unset` не сдвигает; `array_filter`, `array_unique` сохраняют ключи. Для JSON-списка нужен `array_values`.
* `in_array` и `array_search` по умолчанию нестрогие; `array_search` возвращает `0` для первого элемента и `false`, если не найдено.
* `foreach` по значению работает со снимком; `foreach` по ссылке оставляет ссылку - делайте `unset`.
* Обращение к отсутствующему ключу - `Warning`, а не исключение; используйте `??`.
