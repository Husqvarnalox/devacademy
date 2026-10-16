<!-- section: simple -->
## Современный PHP-класс: коротко и неизменяемо

Классы в PHP давно не требуют шаблонного кода. Три возможности PHP 8 меняют стиль:

* **Продвижение свойств конструктора** (constructor promotion, PHP 8.0): параметры конструктора с модификатором видимости сами становятся свойствами. Вместо объявления свойств, параметров и присваиваний - одна строка.
* **`readonly`** (свойства - PHP 8.1, целые классы - PHP 8.2): значение задаётся один раз, дальше изменить его нельзя. Так делают объекты-значения (деньги, координаты, DTO), безопасные для передачи по всей программе.
* **Перечисления** `enum` (PHP 8.1): фиксированный набор вариантов вместо «магических» строк и констант. У варианта есть имя, а у «поддерживаемого» (backed) перечисления - ещё и скалярное значение: `Status::Paid->value === 'paid'`.

К ним добавляется **синтаксис первоклассных вызываемых** (first-class callable syntax, PHP 8.1): `strlen(...)` или `$order->total(...)` превращает функцию или метод в объект `Closure` без строк и массивов вида `[$obj, 'method']`.

<!-- section: precise -->
## Точные правила

**Классы и свойства.** Видимость: `public`, `protected`, `private`. Свойства могут иметь тип (PHP 7.4); типизированное свойство без значения по умолчанию находится в состоянии «не инициализировано» до первого присваивания, и чтение до этого даёт `Error`. Значение по умолчанию свойства - только константное выражение. `static`-свойства и методы относятся к классу; `self::` - класс, где написан код; `static::` - класс вызова (подробнее в [уроке об интерфейсах и позднем статическом связывании](lesson:php.fundamentals.interfaces-traits-lsb)). `new static(...)` внутри метода создаёт объект того же класса, что и вызвавший.

**Создание динамических свойств** (`$obj->unknown = 1` без объявления) с PHP 8.2 помечено как устаревшее (Deprecated); в `readonly class` оно запрещено. Объявляйте свойства или используйте массивы и `stdClass`.

**Продвижение свойств (PHP 8.0).** `public function __construct(private string $name, protected int $age = 0) {}` объявляет свойства и присваивает их. Можно сочетать с обычными параметрами. Значения по умолчанию разрешены; в абстрактных конструкторах и в интерфейсах продвижение запрещено. С PHP 8.1 в значении по умолчанию допустим `new`: `private Logger $log = new NullLogger()`.

**Именованные аргументы (PHP 8.0).** `new Money(amount: 100, currency: 'EUR')` - порядок не важен, пропущенные параметры берут значения по умолчанию.

**`readonly` свойства (PHP 8.1).**

* Только типизированные, без значения по умолчанию, не `static`.
* Инициализируются **один раз** и только из области видимости класса; повторная запись и внешняя запись дают `Error: Cannot modify readonly property`.
* Неизменяем сам указатель: если свойство хранит объект или массив, то изменения *внутри* объекта допустимы; массив же - значение, и его элементы менять нельзя.
* Копия с изменением (приём «wither»): `public function withX(int $x): static { return new static($x, $this->y); }`.
* С PHP 8.3 внутри `__clone` readonly-свойство можно один раз переинициализировать (глубокое клонирование). С PHP 8.5 появилась запись `clone($obj, ['x' => 5])` - клонирование с изменением свойств.

**Readonly-классы (PHP 8.2).** `readonly class Money {...}` делает readonly все объявленные свойства и запрещает динамические; нетипизированные и статические свойства в таком классе запрещены. Наследовать readonly-класс можно только readonly-классом.

**Перечисления (PHP 8.1).**

* `enum Suit { case Hearts; case Spades; }` - «чистое»; `enum Status: string { case Draft = 'draft'; }` - поддерживаемое (значения `int` или `string`, уникальные, константные).
* У варианта есть `->name`; у поддерживаемого - `->value`. Статические методы `cases()` (все варианты по порядку объявления), `from($v)` (бросает `ValueError`, если значения нет) и `tryFrom($v)` (даёт `null`).
* Варианты - **объекты-синглтоны**: сравнивайте через `===`; `clone` и `new` запрещены. Внутри можно объявлять методы, статические методы, константы, `implements` интерфейсов и `use` трейтов без свойств; свойств у перечисления быть не может.
* Перечисление нельзя расширять; оно подходит для `match`, типов параметров (`function f(Status $s)`) и ключей через `->value`.
* Для чтения из внешних данных всегда применяйте `tryFrom`: неизвестное значение - обычная ситуация, а не исключение.

**Типизированные константы классов (PHP 8.3)** и `final`-константы (PHP 8.1): `final public const string NAME = 'x';`.

**Новое в PHP 8.4: хуки свойств и асимметричная видимость.** Хук задаёт код чтения (`get`) и записи (`set`), а `public private(set) int $n` делает свойство читаемым всем, но записываемым только классу. Эти возможности требуют PHP 8.4; пример 5 проверен запуском на PHP 8.5.

**Первоклассные вызываемые (PHP 8.1).** `strlen(...)`, `$obj->method(...)`, `Cls::staticMethod(...)` создают `Closure`. В отличие от `Closure::fromCallable('strlen')`, синтаксис проверяется при компиляции, а для метода сохраняется область видимости места создания. Подходит всюду, где ждут `callable`: `array_map(trim(...), $words)`.

<!-- section: examples -->
## Примеры

**Пример 1. Продвижение свойств, readonly и «wither».**

```php run
<?php
final class Point
{
    public function __construct(
        public readonly int $x,
        public readonly int $y = 0,
        private readonly array $tags = [],
    ) {
    }

    public function withX(int $x): static
    {
        return new static($x, $this->y, $this->tags);
    }

    public function tagCount(): int
    {
        return count($this->tags);
    }
}

$p = new Point(y: 5, x: 1, tags: ['a', 'b']);
$q = $p->withX(10);
echo "$p->x,$p->y -> $q->x,$q->y, tags: {$q->tagCount()}\n";

try {
    $p->x = 99;
} catch (Error $e) {
    echo get_class($e), ': ', $e->getMessage(), "\n";
}

echo (new ReflectionProperty(Point::class, 'x'))->isReadOnly() ? "readonly\n" : "mutable\n";
$clone = clone $p;
echo $clone->x === $p->x ? "clone keeps values\n" : "different\n";
```
```output
1,5 -> 10,5, tags: 2
Error: Cannot modify readonly property Point::$x
readonly
clone keeps values
```

**Пример 2. Readonly-класс как объект-значение.**

```php run
<?php
readonly class Money
{
    public function __construct(public int $cents, public string $currency = 'EUR')
    {
        if ($cents < 0) {
            throw new InvalidArgumentException('negative amount');
        }
    }

    public function plus(self $other): static
    {
        return new static($this->cents + $other->cents, $this->currency);
    }

    public function __toString(): string
    {
        return sprintf('%d.%02d %s', intdiv($this->cents, 100), $this->cents % 100, $this->currency);
    }
}

$a = new Money(1050);
$b = $a->plus(new Money(295));
echo $a, ' + 2.95 EUR = ', $b, "\n";
var_dump($a == new Money(1050), $a === new Money(1050));

try {
    $a->cents = 1;
} catch (Error $e) {
    echo $e->getMessage(), "\n";
}
try {
    $a->note = 'x';
} catch (Error $e) {
    echo $e->getMessage(), "\n";
}
```
```output
10.50 EUR + 2.95 EUR = 13.45 EUR
bool(true)
bool(false)
Cannot modify readonly property Money::$cents
Cannot create dynamic property Money::$note
```

Равенство `==` сравнивает свойства, а `===` - идентичность объектов.

**Пример 3. Поддерживаемое перечисление с методами.**

```php run
<?php
interface HasLabel
{
    public function label(): string;
}

enum Status: string implements HasLabel
{
    case Draft = 'draft';
    case Paid = 'paid';
    case Shipped = 'shipped';

    public const DEFAULT = self::Draft;

    public function label(): string
    {
        return ucfirst($this->value);
    }

    public function next(): ?self
    {
        return match ($this) {
            self::Draft => self::Paid,
            self::Paid => self::Shipped,
            self::Shipped => null,
        };
    }

    public static function parse(string $raw): self
    {
        return self::tryFrom(strtolower(trim($raw))) ?? self::DEFAULT;
    }
}

foreach (Status::cases() as $case) {
    echo $case->name, '=', $case->value, ' -> ', $case->next()?->name ?? 'end', "\n";
}
echo Status::parse(' PAID ')->label(), ' ', Status::parse('???')->label(), "\n";
var_dump(Status::tryFrom('nope'), Status::from('paid') === Status::Paid, Status::Paid instanceof HasLabel);

try {
    Status::from('nope');
} catch (ValueError $e) {
    echo get_class($e), "\n";
}
echo json_encode(['status' => Status::Shipped]), "\n";
```
```output
Draft=draft -> Paid
Paid=paid -> Shipped
Shipped=shipped -> end
Paid Draft
NULL
bool(true)
bool(true)
ValueError
{"status":"shipped"}
```

`match` разобран подробно в [уроке об ошибках и match](lesson:php.fundamentals.errors-and-match): здесь достаточно знать, что он возвращает значение ветки и сравнивает строго. Поддерживаемое перечисление сериализуется в JSON своим значением.

**Пример 4. Синтаксис первоклассных вызываемых.**

```php run
<?php
enum Level: int
{
    case Low = 1;
    case High = 2;
}

class Greeter
{
    public function __construct(private string $greeting)
    {
    }

    public function greet(string $name): string
    {
        return "{$this->greeting}, $name";
    }

    public static function shout(string $s): string
    {
        return strtoupper($s) . '!';
    }
}

$g = new Greeter('Hello');
$names = [' ann ', 'bob '];

echo implode('|', array_map(trim(...), $names)), "\n";
echo implode('|', array_map($g->greet(...), array_map(trim(...), $names))), "\n";
echo implode('|', array_map(Greeter::shout(...), ['a', 'b'])), "\n";
echo implode(',', array_map(fn(Level $l) => $l->name, array_map(Level::from(...), [2, 1]))), "\n";

$length = strlen(...);
echo get_class($length), ' ', $length('héllo'), "\n";
```
```output
ann|bob
Hello, ann|Hello, bob
A!|B!
High,Low
Closure 6
```

**Пример 5. Хуки и асимметричная видимость (PHP 8.4+); `clone` с изменением (PHP 8.5).** Блок не запускается автоматически, результаты получены запуском на PHP 8.5.

```php
class User
{
    public string $name {
        set(string $value) => $this->name = trim($value);
        get => strtoupper($this->name);
    }
    public private(set) int $logins = 0;

    public function login(): void
    {
        $this->logins++;
    }
}

$u = new User();
$u->name = '  bob ';
$u->login();
echo $u->name, ' ', $u->logins;   // BOB 1
$u->logins = 5;                  // Error: Cannot modify private(set) property User::$logins from global scope

final class Color
{
    public function __construct(public readonly int $r = 0, public readonly int $g = 0) {}

    public function withR(int $r): static
    {
        return clone($this, ['r' => $r]);   // PHP 8.5
    }
}
echo (new Color(1, 2))->withR(9)->r;   // 9
```

<!-- section: mistakes -->
## Типичные ошибки

* **Ожидать, что `readonly` делает неизменяемым всё вложенное.** Неизменяем сам указатель на объект: `public readonly array $items` нельзя заменить, а объект внутри `readonly`-свойства можно менять, если он сам изменяем.
* **Объявить `readonly` свойство с значением по умолчанию или без типа.** Это ошибка компиляции: значение задаётся в конструкторе, а тип обязателен.
* **Присваивать `readonly`-свойство не из области видимости класса.** Даже `public readonly` нельзя инициализировать извне.
* **Сравнивать варианты перечисления с их значением.** `Status::Paid == 'paid'` ложно: сравнивайте `Status::Paid === $status` или `$status->value === 'paid'`.
* **Использовать `from()` для пользовательского ввода.** Неизвестное значение бросит `ValueError`; для ввода берите `tryFrom` и обрабатывайте `null`.
* **Пытаться дать перечислению состояние.** В `enum` нельзя объявлять свойства; храните данные в константах или в `match`.
* **Создавать свойства «на лету».** Начиная с PHP 8.2 это Deprecated, а в `readonly class` - ошибка.
* **Путать `self` и `static` в фабриках.** `new self` создаёт объект класса, где написан код, `new static` - класса вызова; для неизменяемых объектов с наследованием выбирайте `static`.
* **Передавать метод строкой `'Class::method'` или массивом.** Такие callable не проверяются при компиляции; используйте `Class::method(...)`.

<!-- section: summary -->
## Итоги

* Продвижение свойств (8.0) и именованные аргументы убирают шаблонный код; `new` разрешён в значениях параметров по умолчанию (8.1).
* `readonly` свойства (8.1) и readonly-классы (8.2) задают неизменяемые объекты; для «изменения» создают копию через `new static` или, с PHP 8.5, через `clone($obj, [...])`.
* Перечисления (8.1) - синглтон-варианты с методами и интерфейсами; поддерживаемые имеют `value`, `from` и `tryFrom`.
* `f(...)` создаёт `Closure` из функции или метода; хуки свойств и `private(set)` - возможности PHP 8.4.

Дальше: [интерфейсы, трейты и позднее статическое связывание](lesson:php.fundamentals.interfaces-traits-lsb).
Назад: [массивы и функции высшего порядка](lesson:php.fundamentals.arrays-and-functions).
Сравните: [классы, записи и перечисления в Java](lesson:java.fundamentals.classes-records-enums), [классы, записи и структуры в C#](lesson:csharp.fundamentals.classes-records-structs), [классы, sealed и when в Kotlin](lesson:kotlin.fundamentals.classes-sealed-and-when).
