<!-- section: simple -->
## Имена без конфликтов, классы по требованию, значения по одному

Урок объединяет три темы, которые вместе определяют устройство любого PHP-проекта:

* **Пространства имён** (namespaces) - префиксы к именам классов, функций и констант: `App\Models\User` и `Vendor\Lib\User` не конфликтуют. Оператор `use` вводит короткое имя.
* **Автозагрузка** (autoloading) - интерпретатор не читает все файлы заранее. Когда код впервые упоминает неизвестный класс, PHP вызывает зарегистрированную функцию, которая должна подключить нужный файл. Договорённость **PSR-4** связывает пространство имён с каталогом: класс `App\Models\User` лежит в `src/Models/User.php`, если префикс `App\` отображён на `src/`. Менеджер пакетов Composer реализует эту схему за вас, но сам механизм - обычная функция `spl_autoload_register`.
* **Генераторы** - функции с `yield`, которые отдают значения по одному и приостанавливаются между ними. Они позволяют обходить огромные или бесконечные последовательности с постоянной памятью.

<!-- section: precise -->
## Точные правила

**Пространства имён.**

* `namespace App\Models;` - первая инструкция файла (кроме `declare`). Все классы, функции и константы файла получают этот префикс.
* Имя может быть неполным (`User`, `Sub\User`), полным (`\App\Models\User`) или псевдонимом: `use App\Models\User;`, `use App\Models\User as Person;`, группа `use App\Models\{User, Post};`.
* Для функций и констант: `use function App\helper;`, `use const App\LIMIT;`. Для функций и констант действует **откат** в глобальное пространство: `strlen('x')` в пространстве имён найдётся, если нет `App\strlen`. Для классов отката нет: `\DateTime` без префикса `\` или `use` будет искаться как `App\DateTime`.
* `Foo::class` даёт полное имя строкой, не загружая класс; `static::class` и `get_class($o)` возвращают реальное имя. Магическая константа `__NAMESPACE__` содержит текущее пространство.
* Пространство имён не связано с каталогами само по себе: соответствие создаёт автозагрузчик.

**Автозагрузка и PSR-4.**

* `spl_autoload_register(callable $fn)` добавляет загрузчик в очередь; загрузчики вызываются по порядку, пока класс не появится. Вызывается при `new`, обращении к статическому члену, `instanceof` не вызывает (проверка идёт без загрузки), `class_exists($name)` вызывает по умолчанию.
* PSR-4: префикс пространства имён -> базовый каталог. Для `App\Models\User` и отображения `App\` -> `src/` путь равен `src/` + остаток имени с `\` заменёнными на `/` + `.php`, то есть `src/Models/User.php`. Допустимо несколько каталогов на префикс и несколько префиксов; самый длинный подходящий префикс проверяют первым.
* Composer читает секцию `autoload.psr-4` файла `composer.json`, генерирует `vendor/autoload.php`, и в скрипте достаточно `require 'vendor/autoload.php';`. В упражнениях этого курса Composer не нужен: достаточно `spl_autoload_register`.

**Генераторы.**

* Функция с `yield` возвращает объект `Generator` (реализует `Iterator`) и не выполняет тело до первого обращения. `foreach` последовательно получает значения; тело выполняется до очередного `yield`.
* `yield $value;` отдаёт значение с ключами 0, 1, 2...; `yield $key => $value;` задаёт ключ. Ключи могут повторяться.
* `return` внутри генератора сохраняет итоговое значение, которое читается через `$gen->getReturn()` после завершения.
* `yield from $iterable` делегирует другому генератору, массиву или `Traversable`; ключи внутреннего источника сохраняются, поэтому `iterator_to_array($gen, true)` может перезаписать значения. Передайте `false` вторым аргументом, чтобы отбросить ключи.
* `$x = yield $v;` и `$gen->send($data)` позволяют передавать значения в генератор (корутины); для обычных обходов не нужны.
* Генератор можно пройти один раз: повторный `foreach` по завершённому генератору бросает исключение.
* Память: генератор хранит только текущее состояние, поэтому массив из `range(1, 10_000_000)` занимает память пропорционально размеру (и может упереться в `memory_limit`), а генератор на тех же значениях - небольшой постоянный объём.

<!-- section: examples -->
## Примеры

**Пример 1. Пространства имён, `use`, откат для функций.**

```php run
<?php
namespace App\Models {
    class User
    {
        public function __construct(public string $name) {}
    }
    function helper(): string { return __FUNCTION__; }
    const LIMIT = 3;
}

namespace Main {
    use App\Models\User;
    use App\Models\User as Person;
    use function App\Models\helper;
    use const App\Models\LIMIT;

    $u = new User('ann');
    echo $u::class, PHP_EOL;
    echo Person::class, PHP_EOL;
    echo helper(), PHP_EOL;
    echo LIMIT, PHP_EOL;
    echo strlen('abc'), ' ', \strlen('abcd'), PHP_EOL;
    echo namespace\Local::class, ' ', __NAMESPACE__, PHP_EOL;
    var_dump(class_exists('User'), class_exists('App\Models\User'));
}
```
```output
App\Models\User
App\Models\User
App\Models\helper
3
3 4
Main\Local Main
bool(false)
bool(true)
```

**Пример 2. Автозагрузчик PSR-4 на настоящих файлах.** Пример создаёт каталог `src` рядом со скриптом, регистрирует загрузчик и удаляет файлы после работы.

```php run
<?php
declare(strict_types=1);

@mkdir('src/Models', 0777, true);
file_put_contents('src/Models/User.php', "<?php\nnamespace App\\Models;\nclass User { public function hi(): string { return 'hi from ' . self::class; } }\n");

$loaded = [];
spl_autoload_register(function (string $class) use (&$loaded): void {
    $prefix = 'App\\';
    if (!str_starts_with($class, $prefix)) {
        return;
    }
    $path = 'src/' . str_replace('\\', '/', substr($class, strlen($prefix))) . '.php';
    $loaded[] = $path;
    if (is_file($path)) {
        require $path;
    }
});

$u = new App\Models\User();
echo $u->hi(), PHP_EOL;
var_dump(class_exists('App\Models\Missing'));
var_dump(class_exists('Other\Thing'));
echo implode(', ', $loaded), PHP_EOL;

unlink('src/Models/User.php');
rmdir('src/Models');
rmdir('src');
```
```output
hi from App\Models\User
bool(false)
bool(false)
src/Models/User.php, src/Models/Missing.php
```

**Пример 3. Ленивые генераторы: ключи, return и память.**

```php run
<?php
function countdown(int $from) {
    echo "[start]";
    while ($from > 0) {
        yield $from--;
    }
    return 'done';
}

$g = countdown(3);
echo "created", PHP_EOL;
foreach ($g as $k => $v) {
    echo "$k=>$v ";
}
echo $g->getReturn(), PHP_EOL;

function kv() {
    yield 'a' => 1;
    yield 'a' => 2;
    yield 'b' => 3;
}
var_dump(iterator_to_array(kv()));
var_dump(iterator_to_array(kv(), false));

function naturals(): Generator {
    $n = 1;
    while (true) {
        yield $n++;
    }
}
function take(iterable $it, int $n): Generator {
    foreach ($it as $v) {
        if ($n-- <= 0) { return; }
        yield $v;
    }
}
echo implode(',', iterator_to_array(take(naturals(), 5), false)), PHP_EOL;
```
```output
created
[start]0=>3 1=>2 2=>1 done
array(2) {
  ["a"]=>
  int(2)
  ["b"]=>
  int(3)
}
array(3) {
  [0]=>
  int(1)
  [1]=>
  int(2)
  [2]=>
  int(3)
}
1,2,3,4,5
```

**Пример 4. `yield from` и конвейер.**

```php run
<?php
function inner() {
    yield 1;
    yield 2;
    return 'inner-result';
}
function outer() {
    $r = yield from inner();
    yield 3;
    yield from [10, 20];
    return $r;
}

foreach (outer() as $k => $v) {
    echo "$k:$v ";
}
echo PHP_EOL;
var_dump(iterator_to_array(outer(), true));

function filterEven(iterable $xs) {
    foreach ($xs as $x) {
        if ($x % 2 === 0) { yield $x; }
    }
}
function squares(iterable $xs) {
    foreach ($xs as $x) { yield $x * $x; }
}
echo implode(' ', iterator_to_array(squares(filterEven(range(1, 10))), false)), PHP_EOL;

$g = squares([1, 2]);
foreach ($g as $_) {}
try {
    foreach ($g as $_) {}
} catch (Exception $e) {
    echo get_class($e), PHP_EOL;
}
```
```output
0:1 1:2 0:3 0:10 1:20 
array(2) {
  [0]=>
  int(10)
  [1]=>
  int(20)
}
4 16 36 64 100
Exception
```

<!-- section: mistakes -->
## Типичные ошибки

* **Ждать, что пространство имён само находит файлы.** Это делает только автозагрузчик; `namespace` лишь задаёт имя.
* **Забыть `\` у глобальных классов.** В файле с `namespace App;` выражение `new DateTime()` ищет `App\DateTime`; пишите `\DateTime` или `use DateTime;`.
* **Путать пути PSR-4 с регистром.** На Linux файловая система чувствительна к регистру, поэтому `App\Models\user` не найдёт `User.php`; имя файла совпадает с именем класса точно.
* **Выводить результат автозагрузчика в `echo`.** Загрузчик должен подключать файл, а не печатать; он вызывается в непредсказуемый момент.
* **Подключать файлы через `require` в каждом месте.** Один автозагрузчик или Composer заменяют цепочки `require`.
* **Использовать `iterator_to_array` с повторяющимися ключами.** Значения перезаписываются; передайте `false` вторым аргументом.
* **Обходить генератор дважды.** Завершённый генератор нельзя перемотать; создайте новый.
* **Собирать генератор обратно в массив без необходимости.** Это возвращает расход памяти; обходите через `foreach`.

<!-- section: summary -->
## Итоги

* `namespace` задаёт префикс, `use` вводит короткие имена; для функций и констант есть откат в глобальное пространство, для классов - нет.
* PSR-4 отображает префикс пространства имён в каталог, а `spl_autoload_register` подключает файл при первом обращении к классу; Composer лишь генерирует такой загрузчик.
* Генераторы (`yield`, `yield from`, `getReturn`) выполняются лениво и однократно, храня только текущее состояние.
* Для итераторов бесконечной длины используйте `take`-подобные генераторы, а не массивы.

Дальше: [строки и ввод-вывод](lesson:php.fundamentals.strings-and-io).
Назад: [ошибки, исключения и match](lesson:php.fundamentals.errors-and-match).
Сравните: [итераторы и генераторы в Python](lesson:python.advanced.iterators-and-generators), [пакеты и модули в Go](lesson:go.fundamentals.packages-modules-testing), [модули в JavaScript](lesson:javascript.fundamentals.modules-and-testing).
