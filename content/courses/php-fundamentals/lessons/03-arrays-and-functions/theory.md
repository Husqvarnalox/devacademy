<!-- section: simple -->
## Массив - это упорядоченная хеш-таблица

Единственная составная структура PHP - `array`: набор пар «ключ -> значение», который помнит порядок вставки. «Список» - частный случай, когда ключи `0, 1, 2, ...` идут подряд. Поэтому одни и те же функции работают и со списками, и со словарями, но есть нюансы: одни функции **сохраняют ключи**, другие **перенумеровывают** их, и это определяет, получится ли после `array_filter` список или «дырявый» массив.

Вторая половина урока - работа с массивом как с потоком данных. Вместо ручных циклов вы комбинируете функции высшего порядка:

* `array_map` - преобразовать каждый элемент;
* `array_filter` - оставить подходящие;
* `array_reduce` - свернуть в одно значение;
* `usort` и родственные - отсортировать по своему правилу.

А **spread** (`...`) и **деструктуризация** (`[$a, $b] = ...`) позволяют раскладывать и собирать массивы без индексов.

<!-- section: precise -->
## Точные правила

**Ключи.** Ключ - `int` или `string`. Строка, которая выглядит как десятичное целое без ведущих нулей и плюса (`"8"`, `"-3"`), превращается в `int`; `"08"` и `"1.5"` остаются строками. `true`/`false` становятся `1`/`0`, `null` - `""` (ключ `null` объявлен устаревшим в PHP 8.5), дробные числа усекаются (с PHP 8.1 с предупреждением Deprecated). Следствие для строк-имён: имя `"10"` лежит под целочисленным ключом, и `foreach` вернёт его как `int`.

**Порядок и «список».** `foreach` идёт в порядке вставки. `$a[] = x` берёт ключ «максимальный целый + 1». `unset` не перенумеровывает. `array_is_list($a)` (PHP 8.1) проверяет, что ключи - ровно `0..n-1` по порядку. Внутри PHP хранит «упакованные» массивы (ключи 0..n-1) компактнее, чем общие хеш-таблицы, но это деталь реализации, видимая лишь по памяти.

**Сохранение ключей.**

* `array_filter` **сохраняет** ключи; для списка вызовите `array_values`. Режимы: `ARRAY_FILTER_USE_KEY` и `ARRAY_FILTER_USE_BOTH` передают колбэку ключ.
* `array_map` с **одним** массивом сохраняет ключи (в том числе строковые); с двумя и более массивами возвращает список. `array_map(null, $a, $b)` «склеивает» массивы в список пар (транспонирование).
* `array_reduce($arr, fn($carry, $item) => ..., $initial)` - свёртка слева направо; без `$initial` начальное значение `null`.
* `array_column($rows, 'name', 'id')` - извлечь столбец из массива строк; `array_combine`, `array_flip`, `array_unique`, `array_slice`, `array_merge` и `array_key_first`/`array_key_last` (PHP 7.3) работают как ожидаете. `array_merge` перенумеровывает целые ключи и перезаписывает строковые; оператор `+` оставляет значения слева.

**Сортировка.** С PHP 8.0 сортировка **устойчивая**: равные элементы остаются в исходном порядке, поэтому сортировку по нескольким полям можно строить цепочкой. `sort` и `usort` переиндексируют массив, `asort`/`uasort` сохраняют пары, `ksort`/`uksort` сортируют по ключу. Колбэк `usort` возвращает отрицательное число, `0` или положительное; оператор `<=>` («космический корабль») подходит идеально. Сравнение массивов через `<=>` идёт поэлементно, поэтому ключ из нескольких полей пишут как `[$a->dept, -$a->salary] <=> [$b->dept, -$b->salary]`. Осторожно: если в ключе есть строки, похожие на числа (`"10"` и `"9"`), `<=>` сравнит их как числа; для побайтового сравнения используйте `strcmp`. Для `sort`/`ksort` флаги `SORT_STRING`, `SORT_NUMERIC`, `SORT_NATURAL` задают способ сравнения; без `SORT_STRING` ключи-числа и строки-числа сравниваются как числа.

**Колбэки.** `fn($x) => $x * 2` (стрелочная функция, PHP 7.4) автоматически захватывает внешние переменные **по значению**. В обычной `function () use ($k)` захват явный; `use (&$k)` захватывает по ссылке. Передавать можно имя функции строкой, массив `[$obj, 'method']` или синтаксис `strtoupper(...)` (PHP 8.1, подробнее в [уроке о классах](lesson:php.fundamentals.classes-readonly-enums)).

**Spread `...`.**

* В параметрах: `function sum(int ...$nums)` собирает остаток аргументов в массив.
* В вызове: `sum(...$list)` разворачивает массив в аргументы. Строковые ключи развёртываются в *именованные* аргументы (PHP 8.1 разрешает такие ключи и в литералах).
* В литерале: `[...$a, ...$b]` (PHP 7.4) - как `array_merge`, но для строковых ключей работает только с PHP 8.1; целые ключи перенумеровываются.

**Деструктуризация.** `[$a, $b] = $pair;` берёт элементы по позиции, `['id' => $id, 'name' => $name] = $row;` - по ключу; можно вкладывать `[[$x, $y], $z] = ...`, пропускать `[, $second] = ...`, использовать в `foreach ($rows as ['id' => $id])`. Обмен значений: `[$a, $b] = [$b, $a];`. Отсутствующий ключ даёт `null` и предупреждение.

**Новое в PHP 8.4 и 8.5.** `array_find`, `array_find_key`, `array_any`, `array_all` появились в PHP 8.4, а `array_first` и `array_last` - в PHP 8.5 (в курсе проверено запуском на PHP 8.5; для более ранних версий используйте `array_key_first` и `$a[array_key_first($a)]`). Оператор конвейера `|>` также добавлен в 8.5.

<!-- section: examples -->
## Примеры

**Пример 1. Ключи, порядок и `array_is_list`.**

```php run
<?php
$a = ['x' => 1, '8' => 'eight', '08' => 'str', true => 'one', '' => 'empty'];
var_dump($a);

$list = ['a', 'b', 'c'];
unset($list[1]);
var_dump(array_is_list($list));
$list[] = 'd';                         // ключ 3: после unset максимум остался 2
echo implode(',', array_keys($list)), "\n";
echo implode(',', array_keys(array_values($list))), "\n";
```
```output
array(5) {
  ["x"]=>
  int(1)
  [8]=>
  string(5) "eight"
  ["08"]=>
  string(3) "str"
  [1]=>
  string(3) "one"
  [""]=>
  string(5) "empty"
}
bool(false)
0,2,3
0,1,2
```

**Пример 2. `map`, `filter`, `reduce` и что они делают с ключами.**

```php run
<?php
$prices = ['apple' => 120, 'pear' => 90, 'plum' => 150, 'fig' => 200];

$doubled = array_map(fn(int $p) => $p * 2, $prices);   // ключи сохранены
echo json_encode($doubled), "\n";

$cheap = array_filter($prices, fn(int $p) => $p < 150); // ключи сохранены
echo json_encode($cheap), "\n";

$evens = array_filter([1, 2, 3, 4, 5, 6], fn(int $n) => $n % 2 === 0);
echo json_encode($evens), ' ', json_encode(array_values($evens)), "\n"; // «дырявый» массив и список

$shortNames = array_filter(
    array_keys($prices),
    fn(string $name) => strlen($name) <= 4,
);
echo json_encode(array_values($shortNames)), "\n";

$byKey = array_filter($prices, fn(string $k) => str_starts_with($k, 'p'), ARRAY_FILTER_USE_KEY);
echo json_encode($byKey), "\n";

$total = array_reduce($prices, fn(int $carry, int $p) => $carry + $p, 0);
echo $total, "\n";

echo json_encode(array_map(fn($a, $b) => "$a=$b", ['x', 'y'], [1, 2])), "\n";
echo json_encode(array_map(null, ['x', 'y'], [1, 2])), "\n";
```
```output
{"apple":240,"pear":180,"plum":300,"fig":400}
{"apple":120,"pear":90}
{"1":2,"3":4,"5":6} [2,4,6]
["pear","plum","fig"]
{"pear":90,"plum":150}
560
["x=1","y=2"]
[["x",1],["y",2]]
```

**Пример 3. Устойчивая сортировка и сортировка по нескольким полям.**

```php run
<?php
$people = [
    ['name' => 'Ann',  'dept' => 'dev',  'age' => 31],
    ['name' => 'Bob',  'dept' => 'ops',  'age' => 28],
    ['name' => 'Cleo', 'dept' => 'dev',  'age' => 25],
    ['name' => 'Dan',  'dept' => 'ops',  'age' => 28],
    ['name' => 'Eve',  'dept' => 'dev',  'age' => 31],
];

usort($people, fn(array $a, array $b) => $a['age'] <=> $b['age']);   // устойчиво: Bob перед Dan
echo implode(' ', array_column($people, 'name')), "\n";

usort($people, fn(array $a, array $b) => [$a['dept'], -$a['age'], $a['name']] <=> [$b['dept'], -$b['age'], $b['name']]);
echo implode(' ', array_column($people, 'name')), "\n";

$ages = array_column($people, 'age', 'name');
arsort($ages);                                                      // по значению, пары сохраняются
echo json_encode($ages), "\n";

$names = ['10', '9', '100'];
sort($names);                                                       // числовые строки: как числа
echo implode(',', $names), ' ';
sort($names, SORT_STRING);
echo implode(',', $names), "\n";
```
```output
Cleo Bob Dan Ann Eve
Ann Eve Cleo Bob Dan
{"Ann":31,"Eve":31,"Bob":28,"Dan":28,"Cleo":25}
9,10,100 10,100,9
```

**Пример 4. Spread, деструктуризация и именованные аргументы.**

```php run
<?php
function sum(int ...$nums): int
{
    return array_sum($nums);
}

function point(int $x, int $y, int $z = 0): string
{
    return "($x, $y, $z)";
}

$parts = [1, 2, 3];
echo sum(...$parts), ' ', sum(...$parts, ...[10, 20]), ' ', sum(), "\n";
echo point(...['y' => 5, 'x' => 4]), ' ', point(...[7, 8]), "\n";

$left = ['a' => 1, 'b' => 2];
$right = ['b' => 20, 'c' => 30];
echo json_encode([...$left, ...$right]), ' ', json_encode($left + $right), ' ', json_encode(array_merge([5, 6], [7])), "\n";

[$first, , $third] = ['x', 'y', 'z'];
['id' => $id, 'tags' => [$mainTag]] = ['id' => 7, 'tags' => ['php', 'web']];
echo "$first $third $id $mainTag\n";

$a = 1;
$b = 2;
[$a, $b] = [$b, $a];
echo "$a $b\n";

foreach ([['n' => 'Ann', 'score' => 9], ['n' => 'Bob', 'score' => 7]] as ['n' => $n, 'score' => $s]) {
    echo "$n:$s ";
}
echo "\n";
```
```output
6 36 0
(4, 5, 0) (7, 8, 0)
{"a":1,"b":20,"c":30} {"a":1,"b":2,"c":30} [5,6,7]
x z 7 php
2 1
Ann:9 Bob:7 
```

<!-- section: mistakes -->
## Типичные ошибки

* **Считать, что `array_filter` возвращает список.** Ключи сохраняются; для `json_encode` и индексации оберните в `array_values`, иначе получится JSON-объект.
* **Забыть `SORT_STRING` для имён-чисел.** Ключ `"10"` становится `int`, и `ksort` по умолчанию упорядочит `9 < 10`, а не как строки.
* **Использовать `array_merge` для числовых ключей, ожидая сохранения.** Он перенумеровывает целые ключи; для сохранения ключей применяйте `+` или `array_replace`.
* **Менять массив внутри `foreach` и ждать эффекта.** Цикл идёт по копии (copy-on-write); чтобы изменить исходный массив, запишите по ключу `$a[$k] = ...` или итерируйте по ссылке с `unset($v)` после цикла.
* **Писать `usort` с колбэком, возвращающим `bool`.** Результат `$a > $b` приводится к `0` и `1`, а значение `-1` не возвращается; с PHP 8 это даёт Deprecated. Возвращайте `<=>`.
* **Сортировать по нескольким полям вложенными `usort`.** Хватает одного `usort` с ключом-массивом `[$a->x, $a->y] <=> [$b->x, $b->y]`.
* **Забывать про `$initial` в `array_reduce`.** Для пустого массива без начального значения вы получите `null`.
* **Использовать `array_map` ради побочных эффектов.** Для действий над элементами лучше `foreach`: результат `array_map` будет не нужен.

<!-- section: summary -->
## Итоги

* Массив - упорядоченная хеш-таблица; ключи `"8"` превращаются в `int`, `unset` не перенумеровывает, `array_is_list` проверяет «списочность».
* `array_filter` и одномассивный `array_map` сохраняют ключи, `array_values` возвращает список; `array_reduce` сворачивает слева направо.
* Сортировка устойчива с PHP 8.0; колбэк возвращает `<=>`, ключом из нескольких полей служит массив.
* Spread разворачивает массив в аргументы и литералы, деструктуризация раскладывает массивы по позициям и по ключам.

Дальше: [классы, readonly и перечисления](lesson:php.fundamentals.classes-readonly-enums).
Назад: [типы и приведение](lesson:php.fundamentals.types-and-juggling).
Сравните: [массивы и итерация в JavaScript](lesson:javascript.fundamentals.arrays-and-iteration), [Streams в Java](lesson:java.fundamentals.streams-lambdas-optional), [LINQ в C#](lesson:csharp.fundamentals.linq).
