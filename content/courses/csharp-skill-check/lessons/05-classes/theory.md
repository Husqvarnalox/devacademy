<!-- section: simple -->
## Что проверяет эта проверка

Классы с конструкторами и свойствами, интерфейсы, наследование, `virtual` и `override`, абстрактные классы, порядок вызова конструкторов, `record` и выражение `with`.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Классы и объекты](lesson:csharp.from-zero.classes-basics), [Классы, записи и структуры](lesson:csharp.fundamentals.classes-records-structs), [Интерфейсы, наследование и полиморфизм](lesson:csharp.fundamentals.interfaces-inheritance-polymorphism).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```csharp run
using System.Globalization;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

Animal pet = new Dog("Рекс");
Console.WriteLine(pet.Speak());
Console.WriteLine(pet.Describe());

Point p1 = new Point(1, 2);
Point p2 = new Point(1, 2);
Point p3 = p1 with { X = 5 };
Console.WriteLine($"{p1 == p2} {p1.X} {p3.X}");

Plain a = new Plain { Id = 1 };
Plain b = new Plain { Id = 1 };
Console.WriteLine(a == b);
Console.WriteLine(p1);

class Animal
{
    protected string Name { get; }

    public Animal(string name)
    {
        Name = name;
    }

    public virtual string Speak()
    {
        return "...";
    }

    public string Describe()
    {
        return $"{Name}: {Speak()}";
    }
}

class Dog : Animal
{
    public Dog(string name) : base(name)
    {
    }

    public override string Speak()
    {
        return "гав";
    }
}

record Point(int X, int Y);

class Plain
{
    public int Id { get; set; }
}
```
```output
гав
Рекс: гав
True 1 5
False
Point { X = 1, Y = 2 }
```

Переменная типа `Animal` хранит объект `Dog`: вызов `Speak()` идёт в переопределённый метод, потому что он `virtual`. Метод `Describe` из базового класса вызывает `Speak()` и получает версию наследника. Запись `Point` сравнивается по значениям свойств, `with` создаёт изменённую копию, а обычный класс `Plain` сравнивается по ссылке.

<!-- section: precise -->
## Что нужно уметь

* реализовывать интерфейс и наследовать класс, вызывая базовый конструктор через `: base(...)`;
* помечать метод или свойство `virtual` в базовом классе и `override` в наследнике;
* понимать, что вызов виртуального члена выбирается по фактическому типу объекта;
* знать, что абстрактный класс нельзя создать напрямую;
* знать порядок конструкторов: сначала базовый, потом производный;
* отличать `record` (равенство по значениям, `with`) от обычного класса (равенство по ссылке).

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* Забытое `virtual`: переопределить метод нельзя, а скрытие через `new` не даёт полиморфизма.
* Попытка создать экземпляр абстрактного класса или интерфейса: `CS0144`.
* Ожидание, что `==` для двух объектов класса сравнит их содержимое.
* Обращение к `list[0]` у пустого списка: `ArgumentOutOfRangeException`.
* Публичное изменяемое поле вместо свойства: класс не может защитить свои данные.
* Неверный порядок инициализации: использование поля базового класса до вызова базового конструктора.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Классы и объекты](lesson:csharp.from-zero.classes-basics), [Классы, записи и структуры](lesson:csharp.fundamentals.classes-records-structs), [Интерфейсы, наследование и полиморфизм](lesson:csharp.fundamentals.interfaces-inheritance-polymorphism). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
