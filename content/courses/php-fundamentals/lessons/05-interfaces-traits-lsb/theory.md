<!-- section: simple -->
## Общий контракт, общий код и «кто вызвал»

PHP даёт три способа сказать «эти классы похожи», и каждый решает свою задачу:

* **Интерфейс** (`interface`) - только контракт: имена и сигнатуры методов, без кода. Класс может реализовать сколько угодно интерфейсов. Это основной способ связать части программы без зависимости от конкретных классов.
* **Абстрактный класс** (`abstract class`) - частично готовый класс: часть методов написана, часть объявлена `abstract` и обязана быть реализована потомком. Создать объект абстрактного класса нельзя, а наследовать можно только от одного класса.
* **Трейт** (`trait`) - набор методов и свойств, который копируется в класс оператором `use`. Это горизонтальное повторное использование кода без наследования: трейт не создаёт тип и не участвует в `instanceof`.

Ещё одна идея - **позднее статическое связывание** (late static binding): `static::` ссылается на класс, у которого *вызвали* метод, а не на класс, где метод написан. Благодаря ей фабрика в базовом классе, `new static`, создаёт объект потомка.

<!-- section: precise -->
## Точные правила

**Интерфейсы.**

* Методы интерфейса всегда `public`, без тела. Константы допустимы (с PHP 8.1 их можно переопределять в реализации, если интерфейс не объявил их `final`). Свойств у интерфейса нет; с PHP 8.4 интерфейс может требовать свойство с хуками.
* `class A implements I, J` - сколько угодно интерфейсов; интерфейс может расширять несколько других (`interface K extends I, J`).
* Проверка типа: `$x instanceof I`, тип параметра `I $x`. Уже встроенные: `Countable`, `Stringable`, `IteratorAggregate`, `ArrayAccess`, `JsonSerializable`. Класс с методом `__toString()` автоматически реализует `Stringable`.

**Абстрактные классы.**

* `abstract class` может иметь свойства, конструктор, обычные и абстрактные методы. Если в классе есть хоть один абстрактный метод, класс обязан быть `abstract`.
* Потомок обязан реализовать все абстрактные методы, иначе он сам должен быть абстрактным. Видимость в реализации можно только расширять (protected -> public), параметры - расширять (контравариантность), возвращаемый тип - сужать (ковариантность).
* `final class` запрещает наследование, `final` метод - переопределение. Модификатор `final` на константе класса доступен с PHP 8.1.

**Трейты.**

* `use T;` внутри класса. Методы трейта становятся методами класса; у класса приоритет выше трейта, а у трейта выше унаследованного метода.
* Несколько трейтов: если у двух одинаковое имя метода, будет фатальная ошибка, пока конфликт не разрешён: `use A, B { A::hello insteadof B; B::hello as helloB; }`. Через `as` можно ещё сменить видимость: `hello as protected`.
* В трейте допустимы свойства, статические свойства (каждый использующий класс получает **свою** копию), статические методы, абстрактные методы (класс обязан их реализовать) и константы (с PHP 8.2).
* Трейт сам не является типом: `instanceof T` и параметр типа `T` недопустимы. Чтобы дать тип, объявите интерфейс и реализуйте его в классе, использующем трейт.
* Внутри трейта `self` - это использующий класс, `__CLASS__` - его имя, `static` - класс вызова.

**Позднее статическое связывание.**

* `self::` связывается в месте написания кода, `static::` - в момент вызова: `static::class`, `new static`, `static::CONST`, `static::method()`.
* `parent::` и `self::` *передают* информацию о вызывающем классе дальше; `Child::method()` - нет.
* Возвращаемый тип `static` (PHP 8.0) выражает «объект того же класса, что и вызвавший», что удобно для fluent-интерфейсов.
* `get_called_class()` равнозначна `static::class`.

<!-- section: examples -->
## Примеры

**Пример 1. Интерфейс, абстрактный класс и шаблонный метод.** Абстрактный класс реализует общий алгоритм, а потомки подставляют детали.

```php run
<?php
interface Shape
{
    public function area(): float;
}

abstract class Base implements Shape, Stringable
{
    abstract protected function label(): string;

    public function __toString(): string
    {
        return sprintf('%s %.2f', $this->label(), $this->area());
    }
}

final class Rect extends Base
{
    public function __construct(private float $w, private float $h) {}
    public function area(): float { return $this->w * $this->h; }
    protected function label(): string { return 'rect'; }
}

final class Circle extends Base
{
    public function __construct(private float $r) {}
    public function area(): float { return M_PI * $this->r ** 2; }
    protected function label(): string { return 'circle'; }
}

$shapes = [new Rect(3, 4), new Circle(1)];
foreach ($shapes as $s) {
    echo $s, PHP_EOL;
}
echo $shapes[0] instanceof Shape ? 'is Shape' : 'not Shape', PHP_EOL;
echo $shapes[1] instanceof Stringable ? 'is Stringable' : 'not Stringable', PHP_EOL;

try {
    $x = new Base();
} catch (Error $e) {
    echo $e->getMessage(), PHP_EOL;
}
```
```output
rect 12.00
circle 3.14
is Shape
is Stringable
Cannot instantiate abstract class Base
```


**Пример 2. Трейты: конфликт и статические свойства.**

```php run
<?php
trait Hello
{
    public function say(): string { return 'hello'; }
}
trait World
{
    public function say(): string { return 'world'; }
}

trait Counter
{
    private static int $count = 0;
    public static function bump(): int { return ++self::$count; }
}

class Greeter
{
    use Hello, World {
        Hello::say insteadof World;
        World::say as sayWorld;
    }
}

class A { use Counter; }
class B { use Counter; }

$g = new Greeter();
echo $g->say(), ' ', $g->sayWorld(), PHP_EOL;
A::bump();
A::bump();
echo A::bump(), ' ', B::bump(), PHP_EOL;
var_dump($g instanceof Hello);
var_dump(in_array('Hello', class_uses($g), true));
```
```output
hello world
3 1
bool(false)
bool(true)
```


**Пример 3. `self` против `static`.**

```php run
<?php
class Model
{
    public static function createSelf(): self
    {
        return new self();
    }

    public static function create(): static
    {
        return new static();
    }

    public static function who(): string
    {
        return static::class . '/' . self::class;
    }
}

class User extends Model {}

echo get_class(User::createSelf()), PHP_EOL;
echo get_class(User::create()), PHP_EOL;
echo User::who(), PHP_EOL;
echo Model::who(), PHP_EOL;
```
```output
Model
User
User/Model
Model/Model
```


**Пример 4. Позднее связывание с константой и «пересылка».**

```php run
<?php
class Repo
{
    protected const TABLE = 'base';

    public static function table(): string
    {
        return static::TABLE . '|' . self::TABLE;
    }

    public static function viaParent(): string
    {
        return static::class;
    }
}

class UserRepo extends Repo
{
    protected const TABLE = 'users';

    public static function test(): string
    {
        return parent::viaParent() . ' ' . Repo::viaParent();
    }
}

echo UserRepo::table(), PHP_EOL;
echo Repo::table(), PHP_EOL;
echo UserRepo::test(), PHP_EOL;
```
```output
users|base
base|base
UserRepo Repo
```


<!-- section: mistakes -->
## Типичные ошибки

* **Использовать наследование ради повторного использования кода.** Если классы не «являются» друг другом, возьмите трейт или композицию (объект внутри объекта).
* **Ждать типа от трейта.** `instanceof Trait` всегда ложно, а параметр типа `Trait` невозможен: опишите интерфейс.
* **Не разрешить конфликт имён в трейтах.** Два трейта с методом `say` без `insteadof` дают фатальную ошибку при загрузке класса.
* **Считать статическое свойство трейта общим для классов.** Каждый класс получает собственную копию, как показывает пример со счётчиком.
* **Писать `new self` в фабрике базового класса.** Потомок получит объект родителя. Для фабрик и fluent-методов нужен `new static` и тип `static`.
* **Смешивать `static` и `self` в константах.** `self::TABLE` фиксируется на месте написания, `static::TABLE` читает переопределённое значение потомка.
* **Делать интерфейс «толстым».** Один интерфейс из десяти методов трудно реализовать; делите контракты по ролям.
* **Вызывать абстрактный метод через `parent::`.** Реализации у него нет, это ошибка.

<!-- section: summary -->
## Итоги

* Интерфейс описывает контракт, абстрактный класс - частичную реализацию, трейт - копируемый набор методов без типа.
* Реализуйте сколько угодно интерфейсов, но наследуйте от одного класса; трейты комбинируйте с явным разрешением конфликтов (`insteadof`, `as`).
* `static::` и `new static` связываются в момент вызова, а `self::` - в месте написания; возвращаемый тип `static` выражает «тот же класс».
* Статические свойства трейта отдельные у каждого использующего класса.

Дальше: [ошибки, исключения и match](lesson:php.fundamentals.errors-and-match).
Назад: [классы, readonly и перечисления](lesson:php.fundamentals.classes-readonly-enums).
Сравните: [интерфейсы и sealed-типы в Java](lesson:java.fundamentals.interfaces-sealed-patterns), [интерфейсы и наследование в C#](lesson:csharp.fundamentals.interfaces-inheritance-polymorphism), [интерфейсы в Go](lesson:go.fundamentals.interfaces), [трейты и обобщения в Rust](lesson:rust.fundamentals.traits-and-generics).
