<!-- section: simple -->
## Один вызов - разное поведение

**Полиморфизм** - способность вызывать один и тот же метод у разных объектов и получать поведение, зависящее от **реального** типа объекта, а не от типа переменной. В C# он строится на двух механизмах:

* **наследование**: класс-наследник расширяет базовый и переопределяет `virtual`/`abstract` методы;
* **интерфейсы**: контракт «умею вот это», который реализуют несвязанные между собой классы.

Главное практическое правило: **предпочитайте композицию наследованию**. Наследование жёстко связывает наследника с устройством родителя; композиция («у меня есть помощник, и я делегирую ему работу») позволяет менять части независимо. Наследуйте, когда наследник - действительно разновидность базового типа и всегда может заменить его (принцип подстановки Лисков), а не ради повторного использования кода.

Третья тема урока - **контракты равенства**. Если объекты сравниваются по содержимому (ключ словаря, элемент `HashSet`), нужно согласованно реализовать `Equals`, `GetHashCode` и обычно `IEquatable<T>`. Забыв одно из них, вы получите «потерянные» ключи.

<!-- section: precise -->
## Точные правила

**`virtual`, `override`, `abstract`, `sealed`, `new`.**

* Метод базового класса, который можно переопределить, помечается `virtual`; наследник пишет `override`. Вызов диспетчеризуется по **динамическому** типу объекта.
* `abstract` - член без реализации; класс с абстрактными членами сам `abstract` и не создаётся через `new`. Наследник обязан переопределить все абстрактные члены (иначе CS0534).
* `override` невиртуального члена - ошибка CS0506. `sealed override` запрещает дальнейшее переопределение, `sealed class` запрещает наследование (CS0509). Запечатывание позволяет JIT вызывать методы напрямую, но главное - фиксирует контракт.
* `new` на члене наследника **скрывает** (hides) член базового типа, а не переопределяет: вызов через переменную базового типа попадёт в базовый метод. Это почти всегда ошибка; компилятор предупреждает (CS0108), если вы не написали `new` явно.
* Порядок создания: сначала выполняются **инициализаторы полей** наследника, затем конструктор базового класса, затем тело конструктора наследника. Поэтому виртуальный вызов из конструктора базового класса видит наследника, у которого тело конструктора ещё не выполнено (значения, присвоенные в теле, равны `null`/`0`).

**Интерфейсы.**

* Интерфейс описывает члены без состояния; класс реализует любое число интерфейсов, но наследует один базовый класс.
* **Члены по умолчанию** (C# 8): метод или свойство интерфейса может иметь реализацию. Она доступна **только через тип интерфейса**: `((IShape)sq).Describe()`, но не `sq.Describe()` (CS1061), если класс сам её не реализует. Это позволяет добавлять член в опубликованный интерфейс без поломки реализаций.
* **Явная реализация** `void IDisposable.Dispose()` скрывает член от открытого API класса и помогает при конфликте имён.
* Обобщённые интерфейсы `IComparable<T>`, `IEquatable<T>`, `IEnumerable<T>` позволяют избежать упаковки структур и приведений.
* Если реализация отсутствует, ошибка CS0535.

**Композиция вместо наследования.** Вместо `class LoggingRepo : Repo` пишут `class LoggingRepo(IRepo inner) : IRepo` и делегируют вызовы. Так поведение (кэш, лог, повтор) добавляется декоратором, зависимости подменяются в тестах, а иерархия не разрастается.

**Контракт равенства.**

* `Equals(object?)` - рефлексивно, симметрично, транзитивно, не бросает исключений; `Equals(null)` равно `false`.
* **Если `a.Equals(b)`, то `a.GetHashCode() == b.GetHashCode()`.** Обратное не требуется. Словари и `HashSet` сначала сравнивают хеш-коды, и только потом вызывают `Equals`. Переопределили `Equals` без `GetHashCode` - предупреждение CS0659 и неработающий поиск.
* `IEquatable<T>.Equals(T?)` - типизированная версия: быстрее, нет приведения, для структур нет упаковки. `Dictionary<TKey,...>` и `HashSet<T>` пользуются ею через `EqualityComparer<T>.Default`.
* Хеш-код должен быть **стабильным, пока объект лежит в коллекции**. Изменяемый ключ, у которого хеш зависит от изменяемого поля, «теряется» после изменения. Используйте неизменяемые ключи и `HashCode.Combine(a, b)`.
* Операторы `==`/`!=` переопределяйте согласованно с `Equals` или не переопределяйте вовсе; записи и `record struct` делают всё это сами (см. [классы, записи и структуры](lesson:csharp.fundamentals.classes-records-structs)).
* `string` и записи сравниваются по значению, обычные классы - по ссылке, пока вы не переопределили.

<!-- section: examples -->
## Примеры

**Пример 1. `override` и `new`: что вызывается через переменную базового типа.**

```csharp run
using System;
using System.Globalization;

Base b = new Derived();
b.Greet();                 // override: динамический тип
b.Hello();                 // new: статический тип
((Derived)b).Hello();

Shape[] shapes = [new Circle(1), new Rect(2, 3)];
foreach (Shape s in shapes)
    Console.WriteLine($"{s.GetType().Name}: {s.Area().ToString("F2", CultureInfo.InvariantCulture)}");

class Base
{
    public virtual void Greet() => Console.WriteLine("Base.Greet");
    public void Hello() => Console.WriteLine("Base.Hello");
}

class Derived : Base
{
    public override void Greet() => Console.WriteLine("Derived.Greet");
    public new void Hello() => Console.WriteLine("Derived.Hello");
}

abstract class Shape { public abstract double Area(); }
sealed class Circle(double r) : Shape { public override double Area() => Math.PI * r * r; }
sealed class Rect(double w, double h) : Shape { public override double Area() => w * h; }
```
```output
Derived.Greet
Base.Hello
Derived.Hello
Circle: 3.14
Rect: 6.00
```

Через `Base b` вызов `Greet()` попал в переопределение, а `Hello()` - в метод базового класса, потому что `new` только скрывает его.

**Пример 2. Порядок создания и виртуальный вызов из конструктора.**

```csharp run
using System;

new Child("ready");

class Parent
{
    public Parent()
    {
        Console.WriteLine("Parent ctor");
        Init();                       // виртуальный вызов из конструктора
    }
    public virtual void Init() => Console.WriteLine("Parent.Init");
}

class Child : Parent
{
    private readonly string initializer = "from initializer";
    private readonly string fromCtor;

    public Child(string text)
    {
        fromCtor = text;
        Console.WriteLine("Child ctor");
    }

    public override void Init() =>
        Console.WriteLine($"Child.Init: initializer={initializer ?? "null"}, fromCtor={fromCtor ?? "null"}");
}
```
```output
Parent ctor
Child.Init: initializer=from initializer, fromCtor=null
Child ctor
```

Инициализатор поля уже выполнен, а значение из тела конструктора - ещё нет. Поэтому виртуальные вызовы из конструкторов избегают.

**Пример 3. Интерфейсы: член по умолчанию и композиция.**

```csharp run
using System;
using System.Collections.Generic;

IShape sq = new Square(2);
Console.WriteLine(sq.Describe());           // реализация по умолчанию из интерфейса
Square concrete = new Square(3);
Console.WriteLine(((IShape)concrete).Describe());
// concrete.Describe();                    // CS1061: член по умолчанию виден только через интерфейс

IStore store = new CountingStore(new MemoryStore());   // композиция: декоратор
store.Put("a", "1");
store.Put("b", "2");
Console.WriteLine(store.Get("a") + store.Get("zzz"));
Console.WriteLine(((CountingStore)store).Writes);

interface IShape
{
    double Area { get; }
    string Describe() => $"Shape with area {Area}";
}

class Square(double side) : IShape { public double Area => side * side; }

interface IStore
{
    void Put(string key, string value);
    string Get(string key);
}

class MemoryStore : IStore
{
    private readonly Dictionary<string, string> data = new();
    public void Put(string key, string value) => data[key] = value;
    public string Get(string key) => data.TryGetValue(key, out var v) ? v : "?";
}

class CountingStore(IStore inner) : IStore
{
    public int Writes { get; private set; }
    public void Put(string key, string value) { Writes++; inner.Put(key, value); }
    public string Get(string key) => inner.Get(key);
}
```
```output
Shape with area 4
Shape with area 9
1?
2
```

`CountingStore` не наследует `MemoryStore`: он оборачивает любой `IStore`. Так же можно добавить кэш или повторные попытки, не меняя исходный класс.

**Пример 4. Контракт равенства: `Equals`, `GetHashCode`, `IEquatable<T>`.**

```csharp run
using System;
using System.Collections.Generic;

var bad = new HashSet<NoHash> { new NoHash(1) };
Console.WriteLine($"без GetHashCode: {bad.Contains(new NoHash(1))}");

var good = new HashSet<Id> { new Id(1) };
Console.WriteLine($"с GetHashCode: {good.Contains(new Id(1))}");
Console.WriteLine(new Id(1) == new Id(1));
Console.WriteLine(new Id(1).Equals(null));

var key = new MutableKey { V = 1 };
var set = new HashSet<MutableKey> { key };
key.V = 2;                                // хеш-код изменился, пока объект лежит в наборе
Console.WriteLine($"{set.Contains(key)} {set.Count}");

class NoHash(int id)
{
    public int Id { get; } = id;
    public override bool Equals(object? o) => o is NoHash n && n.Id == Id;   // предупреждение CS0659
}

sealed class Id(int value) : IEquatable<Id>
{
    private int Value { get; } = value;
    public bool Equals(Id? other) => other is not null && other.Value == Value;
    public override bool Equals(object? o) => Equals(o as Id);
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(Id? a, Id? b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(Id? a, Id? b) => !(a == b);
}

class MutableKey
{
    public int V;
    public override int GetHashCode() => V;
    public override bool Equals(object? o) => o is MutableKey k && k.V == V;
}
```
```output
без GetHashCode: False
с GetHashCode: True
True
False
False 1
```

Без `GetHashCode` равные объекты попадают в разные корзины и `Contains` возвращает `False`. Изменяемый ключ «потерялся»: он по-прежнему в наборе (`Count` равен 1), но найти его нельзя.

**Пример 5. Ошибки компиляции, которые описывают правила (без запуска).**

```csharp
class B { public void M() { } }
class D : B { public override void M() { } }     // CS0506: M не virtual

abstract class Sh { public abstract double Area(); }
class Ci : Sh { }                               // CS0534: не реализован абстрактный член

sealed class Final { }
class Sub : Final { }                           // CS0509: нельзя наследовать sealed

interface IRun { void Run(); }
class Impl : IRun { }                           // CS0535: интерфейс не реализован
```

<!-- section: mistakes -->
## Типичные ошибки

* **`new` вместо `override`.** Вызов через базовый тип «не срабатывает». Если метод должен переопределяться, объявите его `virtual`.
* **Виртуальные вызовы в конструкторе.** Наследник видит себя до выполнения своего конструктора.
* **Наследование ради повторного использования кода.** Из-за хрупкого базового класса любое изменение родителя ломает наследников; берите композицию.
* **`Equals` без `GetHashCode`.** Предупреждение CS0659 - не формальность: словарь и `HashSet` перестанут находить равные объекты.
* **Изменяемые ключи.** Изменение поля, влияющего на хеш, после добавления в коллекцию.
* **Нарушение симметрии равенства.** `a.Equals(b)` и `!b.Equals(a)` при сравнении объектов разных типов.
* **Оператор `==` без `Equals`** (или наоборот): код ведёт себя по-разному в зависимости от записи.
* **Интерфейс с реализацией по умолчанию, вызванный через класс.** CS1061: приведите к интерфейсу.
* **Слишком «толстые» интерфейсы.** Небольшие интерфейсы проще реализовать и подменять в тестах.
* **Забыть `sealed`.** Класс, не рассчитанный на наследование, лучше запечатать.

<!-- section: summary -->
## Итоги

* Полиморфизм: вызов `virtual`/`abstract` метода выбирает реализацию по динамическому типу; `new` лишь скрывает член.
* Интерфейс - контракт без состояния; члены по умолчанию доступны через тип интерфейса. Предпочитайте композицию и небольшие интерфейсы.
* Если `a.Equals(b)`, то хеш-коды обязаны совпадать. Реализуйте `IEquatable<T>`, `Equals(object?)` и `GetHashCode` вместе и делайте ключи неизменяемыми.
* Порядок создания: инициализаторы полей наследника, конструктор базового класса, тело конструктора наследника.

Сравните с [интерфейсами в Go](lesson:go.fundamentals.interfaces) и [`equals`/`hashCode` в Java](lesson:java.first-steps.primitives-objects-and-equality). Дальше: [обобщения и коллекции](lesson:csharp.fundamentals.generics-and-collections).

<!-- section: facts -->
## Факты

* Члены интерфейса с реализацией по умолчанию появились в C# 8.0 (нужна поддержка среды выполнения, которая есть в .NET Core 3.0 и новее).
* В отличие от Java, инициализаторы полей C# выполняются **до** вызова конструктора базового класса.
