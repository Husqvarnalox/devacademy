<!-- section: simple -->
## Три способа описать данные

В C# для описания «вещей» есть три основных вида типов, и выбор между ними - это выбор **семантики**.

* **`class`** - объект со своей **идентичностью**. Две переменные могут ссылаться на один и тот же объект; равенство по умолчанию - «это один и тот же объект». Подходит для сущностей, у которых есть жизненный цикл и изменяемое состояние (подключение, счёт, сервис).
* **`record`** (ссылочный) - класс, у которого компилятор сам пишет **равенство по значению**, `ToString`, `GetHashCode`, деконструкцию и поддержку `with`. Подходит для данных: «точка», «заказ», «ответ сервера».
* **`struct`** / **`record struct`** - тип-значение. Присваивание копирует содержимое. Подходит для маленьких значений (координата, деньги, идентификатор).

Свойства (`get`/`set`/`init`) - способ контролировать доступ к состоянию. Они выглядят как поля, но являются методами; поэтому в них можно проверять значения. **Неизменяемость** (иммутабельность) - ключевая привычка: объект, который нельзя изменить после создания, нельзя испортить, его безопасно передавать между потоками, а «изменение» - это создание нового объекта (`with`).

<!-- section: precise -->
## Точные правила

**Классы и свойства.**

* Автосвойство `public string Name { get; set; }` создаёт скрытое поле. `get; init;` разрешает присваивание только в конструкторе и инициализаторе объекта (`new T { Name = "x" }`); попытка присвоить позже - ошибка CS8852. `init` появился в C# 9.
* `required` (C# 11) заставляет вызывающего задать свойство или поле при создании: пропуск - ошибка CS9035.
* Ключевое слово `field` (C# 14) даёт доступ к скрытому полю автосвойства внутри аксессора: `set { ...проверка...; field = value; }` - не нужно объявлять поле вручную. До C# 14 такой код не компилируется (проверено запуском с `LangVersion=13`: имя `field` не существует).
* Поля и автосвойства, которым ничего не присвоили, получают значения по умолчанию (`0`, `null`, `false`).

**Первичные конструкторы (C# 12).** `class Counter(int start)` объявляет параметры прямо в заголовке типа. В **классе и структуре** параметры - это захваченные значения, видимые во всём теле, но **не свойства**; если нужно свойство, объявите его и присвойте: `public int Start { get; } = start;`. В **record** первичный конструктор создаёт ещё и открытые свойства (позиционные записи).

**Записи (`record`, C# 9).**

* `record Person(string Name, int Age);` - класс со свойствами `Name`, `Age` (`init`), конструктором, `Deconstruct`, `ToString` формата `Person { Name = Ann, Age = 30 }`, `Equals`, `GetHashCode`, операторами `==`/`!=`.
* Равенство - **по значению** для всех объявленных свойств/полей (каждое сравнивается через `EqualityComparer<T>.Default`) **и по точному типу**: `Employee` и `Person` с одинаковыми `Name` и `Age` не равны (сравнивается скрытое свойство `EqualityContract`).
* `with`-выражение создаёт **копию** с изменёнными свойствами: `p with { Age = 31 }`. Исходный объект не меняется. Копия поверхностная: вложенные ссылочные значения (массив, список) остаются общими.
* Запись не делает ваши данные глубоко неизменяемыми: свойство типа `string[]` сравнивается **по ссылке** на массив, а в `ToString` печатается имя типа массива.

**Структуры.**

* `struct` - тип-значение. Копируется при присваивании и передаче. Значение по умолчанию (`default(T)`, элементы нового массива) - все поля обнулены, поэтому структура **всегда имеет допустимое «нулевое» состояние**, и инвариант вроде «не пустая строка» защитить нельзя.
* `readonly struct` - все поля только для чтения; компилятор избегает защитных копий. `record struct` (C# 10) добавляет те же члены, что и запись, но позиционные свойства **изменяемые** (`get; set;`); `readonly record struct` делает их `init`.
* Изменяемые структуры опасны: `list[0].V = 1` не компилируется (CS1612), потому что индексатор возвращает копию; присвоив элемент переменной, вы изменяете копию.
* Размер: структура копируется целиком. Практическое правило (см. рекомендации по проектированию .NET): небольшие (порядка 16 байт), неизменяемые, логически одно значение.

**Что выбрать.**

| Нужно | Выбор |
| --- | --- |
| Сущность с идентичностью и изменяемым состоянием | `class` |
| Данные, сравниваемые по содержимому, ссылочная семантика | `record` |
| Маленькое неизменяемое значение, много экземпляров | `readonly record struct` / `readonly struct` |
| Результат/параметры, которые не должны меняться | неизменяемые типы + `with` |

**Предупреждение о версиях.** В этом курсе использованы: `init` и `record` (C# 9), `record struct` (C# 10), `required` (C# 11), первичные конструкторы и выражения коллекций (C# 12), `field` и расширенные возможности (C# 14). Среда запуска курса компилирует по умолчанию C# 14.

<!-- section: examples -->
## Примеры

**Пример 1. Record: значение, а не идентичность.**

```csharp run
using System;
using System.Collections.Generic;

var p = new Person("Ann", 30);
var q = new Person("Ann", 30);
Console.WriteLine(p);
Console.WriteLine($"== {p == q}, ReferenceEquals {ReferenceEquals(p, q)}");
Console.WriteLine($"hash equal: {p.GetHashCode() == q.GetHashCode()}");

var older = p with { Age = 31 };     // копия с изменением
Console.WriteLine($"{older} / исходный: {p}");

var (name, age) = p;                 // деконструкция
Console.WriteLine($"{name} {age}");

var set = new HashSet<Person> { p, q, older };
Console.WriteLine(set.Count);        // p и q - один элемент

var c1 = new Plain { Name = "x" };
var c2 = new Plain { Name = "x" };
Console.WriteLine(c1 == c2);         // класс: сравнение ссылок

var e = new Employee("Ann", 30, "dev");
Person asPerson = e;
Console.WriteLine(asPerson == new Person("Ann", 30));  // разные типы - не равны

record Person(string Name, int Age);
record Employee(string Name, int Age, string Role) : Person(Name, Age);
class Plain { public string Name { get; init; } = ""; }
```
```output
Person { Name = Ann, Age = 30 }
== True, ReferenceEquals False
hash equal: True
Person { Name = Ann, Age = 31 } / исходный: Person { Name = Ann, Age = 30 }
Ann 30
2
False
False
```

Запись сравнивает содержимое, поэтому `p` и `q` равны и дают один элемент в `HashSet`. Класс `Plain` сравнивается по ссылке. `Employee` и `Person` с одинаковыми именем и возрастом не равны: учитывается точный тип.

**Пример 2. Свойства: `init`, `required`, `field`.**

```csharp run
using System;

var t = new Temperature { Name = "kitchen" };
t.Celsius = 21;
Console.WriteLine($"{t.Name} {t.Celsius}");
try
{
    t.Celsius = -400;
}
catch (ArgumentOutOfRangeException e)
{
    Console.WriteLine($"отклонено: {e.ParamName}");
}
Console.WriteLine(t.Celsius);        // прежнее значение сохранилось

class Temperature
{
    public required string Name { get; init; }   // C# 11 + C# 9

    public double Celsius
    {
        get => field;                             // C# 14: скрытое поле автосвойства
        set
        {
            if (value < -273.15) throw new ArgumentOutOfRangeException(nameof(value));
            field = value;
        }
    }
}
```
```output
kitchen 21
отклонено: value
21
```

`Name` можно задать только при создании; `Celsius` проверяет значение в аксессоре, не требуя ручного поля.

**Пример 3. Структуры: копия, изменяемость и `default`.**

```csharp run
using System;
using System.Collections.Generic;

var a = new Pt(1, 2);
var b = a;                 // копия
b.X = 100;                 // record struct: свойства изменяемые
Console.WriteLine($"{a} {b}");

var r = new RPt(1, 2);
var r2 = r with { X = 7 };   // у readonly record struct меняют только через with
Console.WriteLine($"{r} {r2}");

var list = new List<Pt> { new(1, 1) };
var copy = list[0];
copy.X = 9;                  // изменилась копия, не список
Console.WriteLine(list[0]);
list[0] = copy;              // записываем копию обратно
Console.WriteLine(list[0]);

var arr = new Pt[2];
Console.WriteLine(arr[1]);   // default: все поля нули
Console.WriteLine(default(RPt) == new RPt(0, 0));

record struct Pt(int X, int Y);
readonly record struct RPt(int X, int Y);
```
```output
Pt { X = 1, Y = 2 } Pt { X = 100, Y = 2 }
RPt { X = 1, Y = 2 } RPt { X = 7, Y = 2 }
Pt { X = 1, Y = 1 }
Pt { X = 9, Y = 1 }
Pt { X = 0, Y = 0 }
True
```

**Пример 4. Первичный конструктор класса и вложенные изменяемые данные.**

```csharp run
using System;
using System.Collections.Generic;

var inv = new Inventory(["apple", "pear"]);
inv.Add("plum");
Console.WriteLine(inv.Describe());

var tag = new Tag(["a", "b"]);
var same = new Tag(["a", "b"]);
Console.WriteLine(tag == same);          // массивы сравниваются по ссылке
Console.WriteLine(tag);

var shared = new Tag(tag.Items);
Console.WriteLine(tag == shared);        // тот же массив - равны

class Inventory(IEnumerable<string> items)    // C# 12: первичный конструктор
{
    private readonly List<string> list = [.. items];     // C# 12: выражение коллекции
    public void Add(string item) => list.Add(item);
    public string Describe() => $"{list.Count}: {string.Join(", ", list)}";
}

record Tag(string[] Items);
```
```output
3: apple, pear, plum
False
Tag { Items = System.String[] }
True
```

Параметр `items` захвачен классом `Inventory`, но свойства `Items` у него нет. У записи `Tag` свойство `Items` есть, однако равенство массивов - по ссылке, а не по элементам.

**Пример 5. Ошибки компиляции, которые стоит узнавать (без запуска).**

```csharp
var u = new User { Name = "a" };
u.Name = "b";                    // CS8852: init-свойство нельзя менять после создания

var u2 = new User2();            // CS9035: не задано required-свойство

var list = new List<MutS> { new() };
list[0].V = 1;                   // CS1612: индексатор вернул копию структуры

class User { public string Name { get; init; } = ""; }
class User2 { public required string Name { get; init; } }
struct MutS { public int V; }
```

<!-- section: mistakes -->
## Типичные ошибки

* **Использовать `record` для сущности с изменяемым состоянием.** Две «разные» записи с одинаковыми данными окажутся равными, а изменяемая запись в `HashSet` теряется после изменения ключевых свойств.
* **Ожидать глубокого равенства.** Свойства-массивы и списки сравниваются по ссылке; `with` копирует их ссылку, а не содержимое.
* **Забыть, что запись сравнивает точный тип.** Наследник и базовая запись с одинаковыми данными не равны.
* **Делать изменяемые структуры.** `list[0].X = 1` не скомпилируется, а временные копии теряют изменения. Если структура маленькая, делайте её `readonly`.
* **Крупные структуры.** Передача по значению копирует всё; для больших данных нужен класс (или `in`-параметры).
* **Считать параметры первичного конструктора класса свойствами.** Они не доступны снаружи; свойство нужно объявить отдельно. Захват параметра в нескольких методах создаёт скрытое поле - изменение параметра внутри класса может удивить читателя.
* **Инвариант, который структура не может защитить.** Через `default` и массивы можно получить значение, не прошедшее конструктор.
* **Публичные поля вместо свойств.** Позже не получится добавить проверку без изменения бинарного контракта.
* **Использовать `field` в проекте на более ранней версии языка.** До C# 14 имя `field` не определено.

<!-- section: summary -->
## Итоги

* `class` - идентичность и ссылочная семантика; `record` - данные с равенством по значению и `with`; `struct` - маленькое значение, которое копируется.
* `init` и `required` позволяют задавать состояние при создании и не менять его потом; `with` создаёт изменённую копию.
* Равенство записи - по значению свойств и точному типу; вложенные массивы и списки сравниваются по ссылке.
* Изменяемые структуры порождают потерянные изменения: делайте их `readonly`.
* Версии: `init`/`record` - C# 9, `record struct` - C# 10, `required` - C# 11, первичные конструкторы и выражения коллекций - C# 12, `field` - C# 14.

Сравните с [`val` и неизменяемостью в Kotlin](lesson:kotlin.first-steps.null-safety-and-val-var) и [равенством объектов в Java](lesson:java.first-steps.primitives-objects-and-equality). Дальше: [наследование и интерфейсы](lesson:csharp.fundamentals.interfaces-inheritance-polymorphism).

<!-- section: facts -->
## Факты

* Для `record` компилятор синтезирует `Equals`, `GetHashCode`, `ToString`, `Deconstruct` (для позиционных), копирующий конструктор, на котором строится `with`.
* `record struct` и `readonly record struct` появились в C# 10; позиционные свойства обычного `record struct` изменяемы.
