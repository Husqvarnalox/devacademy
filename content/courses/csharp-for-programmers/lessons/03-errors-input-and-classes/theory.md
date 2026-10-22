<!-- section: simple -->
## Зачем этот урок

Ввод нельзя считать верным: пользователь присылает пустые строки, буквы вместо чисел и числа, которые не помещаются в `int`. В C# для этого есть два пути - исключения (`int.Parse` бросает `FormatException`) и методы `TryParse`, которые возвращают `bool`. Вторая тема урока - классы: объект класса живёт по ссылке, свойства скрывают поле и проверяют значение, а ссылка может быть `null`, и компилятор это отслеживает. Успешное прохождение (упражнение и квиз) засчитывает уроки 08, 09, 22 и 23 курса «C# с нуля».

## Главная мысль

Данные снаружи программы нужно проверять там, где они входят, и сообщать об ошибках способом, который вызывающий может обработать. Если ошибка в данных ожидаема (ввод пользователя), используйте `TryParse` и `bool`. Если нарушено правило, которое вызывающий обязан был соблюсти (отрицательная сумма, недостаточно средств), бросайте исключение, у класса которого понятный смысл.

<!-- section: examples -->
## Пример 1. Чтение ввода и разбор чисел

Чтение стандартного ввода: `Console.ReadLine()` возвращает `string?` - `null`, когда ввод закончился. Чтобы не получать предупреждения nullable, пишут `Console.ReadLine() ?? ""` либо проверяют `null` явно.

```csharp
string? line = Console.ReadLine();
while (line != null)
{
    string trimmed = line.Trim();
    if (trimmed.Length > 0)
    {
        Console.WriteLine($"строка: {trimmed}");
    }
    line = Console.ReadLine();
}
```

Весь ввод целиком читает `Console.In.ReadToEnd()`. Теперь разбор чисел:

```csharp run
using System.Globalization;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

Console.WriteLine(int.Parse(" 12 ") + 1);
Console.WriteLine(decimal.Parse("1,000"));
Console.WriteLine(decimal.TryParse("1,000", out decimal loose));
Console.WriteLine(decimal.TryParse("1,000", NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal strict));
Console.WriteLine(strict);
Console.WriteLine(int.TryParse("+5", out int plus));
Console.WriteLine(int.TryParse("", out int empty));
Console.WriteLine(int.TryParse("٣", out int arabic));

string[] inputs = { "abc", "", "2147483648", "7" };
foreach (string input in inputs)
{
    try
    {
        Console.WriteLine(int.Parse(input));
    }
    catch (FormatException e)
    {
        Console.WriteLine("FormatException: " + e.Message);
    }
    catch (OverflowException e)
    {
        Console.WriteLine("OverflowException: " + e.Message);
    }
}
```
```output
13
1000
True
False
0
True
False
False
FormatException: The input string 'abc' was not in a correct format.
FormatException: The input string '' was not in a correct format.
OverflowException: Value was either too large or too small for an Int32.
7
```

Встроенный разбор **снисходителен**: `int.Parse` по умолчанию принимает пробелы по краям и знак, а `decimal.Parse` - ещё и разделитель тысяч (`"1,000"` равно `1000`). Если нужен строгий формат, передают `NumberStyles` и культуру: с `AllowDecimalPoint` запятая уже недопустима, и `TryParse` возвращает `false`, оставляя в `out`-переменной `0`. `FormatException` означает неверную запись, `OverflowException` - число вне диапазона типа. Для ввода пользователя удобнее `TryParse`: ошибка - обычный случай, а не исключительный.

**Попробуйте.** Прочитайте строку «3 4 x 5», разделите её через `Split(' ')` и сложите только разбираемые числа, посчитав пропущенные.

## Пример 2. Исключения: try, catch, finally, throw

```csharp run
static int Load(string text)
{
    try
    {
        return int.Parse(text);
    }
    catch (FormatException)
    {
        Console.WriteLine("плохой формат");
        return -1;
    }
    finally
    {
        Console.WriteLine("конец Load");
    }
}

static void Check(int age)
{
    if (age < 0)
    {
        throw new ArgumentOutOfRangeException(nameof(age), "возраст не может быть отрицательным");
    }
}

static int F()
{
    try
    {
        return 1;
    }
    finally
    {
        Console.Write("f ");
    }
}

Console.WriteLine(Load("5"));
Console.WriteLine(Load("x"));

try
{
    Check(-1);
}
catch (ArgumentException e) when (e.ParamName == "age")
{
    Console.WriteLine(e.GetType().Name);
    Console.WriteLine(e.Message);
}

try
{
    try
    {
        int.Parse("zz");
    }
    catch (FormatException e)
    {
        throw new InvalidOperationException("не удалось прочитать заказ", e);
    }
}
catch (InvalidOperationException e)
{
    Console.WriteLine($"{e.Message} <- {e.InnerException?.GetType().Name}");
}

try
{
    object o = "text";
    Console.WriteLine((int)o);
}
catch (InvalidCastException)
{
    Console.WriteLine("InvalidCastException");
}
Console.WriteLine(F());
```
```output
конец Load
5
плохой формат
конец Load
-1
ArgumentOutOfRangeException
возраст не может быть отрицательным (Parameter 'age')
не удалось прочитать заказ <- FormatException
InvalidCastException
f 1
```

Блок `finally` выполняется всегда, даже при `return` (поэтому «конец Load» печатается раньше результата). Блоков `catch` может быть несколько, они проверяются сверху вниз, и более общий тип должен идти **после** частного. Условие `when` уточняет перехват. Исключение, пойманное и обёрнутое в другое, сохраняет причину в `InnerException`. Для проверки аргументов у `System` есть готовые типы: `ArgumentException`, `ArgumentNullException`, `ArgumentOutOfRangeException` (все наследуют `ArgumentException`), а для неверного состояния объекта - `InvalidOperationException`. В C# нет проверяемых исключений: сигнатура метода не перечисляет, что он бросает.

## Пример 3. Класс, ссылка и null

```csharp run
var a = new Item("Хлеб", 45m);
var b = a;
b.Price = 50m;
var c = new Item("Хлеб", 50m);
Console.WriteLine(a);
Console.WriteLine($"{a == b} {a == c} {a.Equals(c)}");

var cart = new List<Item> { a, c };
foreach (Item item in cart)
{
    item.Price += 10m;
}
Console.WriteLine(string.Join("; ", cart));

Item? missing = FindByName(cart, "Сыр");
Console.WriteLine(missing is null);
Console.WriteLine(missing?.Name ?? "нет такого товара");
Console.WriteLine(missing?.Price.ToString() ?? "-");

Account acc = new Account("Анна");
acc.Deposit(100m);
try
{
    acc.Withdraw(150m);
}
catch (InvalidOperationException e)
{
    Console.WriteLine(e.Message);
}
Console.WriteLine(acc.Balance);

static Item? FindByName(List<Item> items, string name)
{
    foreach (Item item in items)
    {
        if (item.Name == name)
        {
            return item;
        }
    }
    return null;
}

class Item
{
    public string Name { get; }
    public decimal Price { get; set; }

    public Item(string name, decimal price)
    {
        Name = name;
        Price = price;
    }

    public override string ToString() => $"{Name} за {Price}";
}

class Account
{
    public string Owner { get; }
    public decimal Balance { get; private set; }

    public Account(string owner)
    {
        Owner = owner;
    }

    public void Deposit(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("сумма должна быть положительной", nameof(amount));
        }
        Balance += amount;
    }

    public void Withdraw(decimal amount)
    {
        if (amount > Balance)
        {
            throw new InvalidOperationException("недостаточно средств");
        }
        Balance -= amount;
    }
}
```
```output
Хлеб за 50
True False False
Хлеб за 60; Хлеб за 60
True
нет такого товара
-
недостаточно средств
100
```

Описания классов пишут **после** инструкций верхнего уровня. Объекты класса - ссылочный тип: `var b = a` создаёт второе имя того же объекта, поэтому изменение через `b` видно через `a`, а `==` без переопределения сравнивает ссылки (`a` и `c` с равными полями - разные объекты). Свойство `{ get; }` задаётся только в конструкторе, `{ get; private set; }` меняется только внутри класса, поэтому баланс нельзя испортить снаружи: нужно пройти через `Deposit` или `Withdraw`, где стоят проверки. `Item?` означает «может быть `null`»: компилятор предупредит, если обратиться к такому значению без проверки. Операторы `?.` (вызвать, если не `null`) и `??` (подставить значение по умолчанию) делают проверку короткой. Метод `ToString` переопределяют, чтобы объект красиво печатался.

<!-- section: precise -->
## Точные правила

* `Console.ReadLine()` возвращает `null` в конце ввода. Тип `string?` и операторы `?.`, `??`, `??=`, `is null` отражают это; аннотации nullable дают предупреждения компилятора, но не меняют поведение во время выполнения.
* `T.Parse(text)` бросает `FormatException`, `OverflowException` или `ArgumentNullException`; `T.TryParse(text, out value)` возвращает `bool` и при неудаче кладёт в `value` значение по умолчанию.
* `Parse` без стиля принимает пробелы по краям и знак; у `decimal`/`double` ещё и разделитель тысяч. Строгий разбор - `NumberStyles` и `CultureInfo.InvariantCulture`.
* Порядок `catch` важен: более общий тип перед частным не компилируется (`CS0160`). `catch (Exception)` ловит всё и скрывает баги, ловите конкретные типы.
* `throw;` пробрасывает текущее исключение вместе с трассировкой; `throw e;` обнуляет трассировку. Новое исключение с причиной: `new X("...", inner)`.
* `finally` выполняется при нормальном завершении, `return` и исключении; `using` (подробно - в «C#: основы языка») закрывает ресурсы через него.
* Свойство: `{ get; set; }`, `{ get; }` (только в конструкторе), `{ get; private set; }`, `{ get; init; }`. Поле класса по умолчанию `private`, члены - `private`, если не указано иное.
* Класс - ссылочный тип; `==` по умолчанию сравнивает ссылки, `Equals` можно переопределить. Для значений, сравниваемых по содержимому, подойдёт `record` или `struct` (подробно - в курсе «C#: первые шаги»).
* Исключения нужны для исключительных ситуаций, а не для обычного потока управления: бросок и перехват заметно дороже условия.

<!-- section: mistakes -->
## Типичные ошибки

**1. `int.Parse` на пользовательском вводе.** Одно неверное значение завершает программу с `FormatException`. Используйте `TryParse` и подсчёт неверных строк.

**2. Нестрогий разбор.** `decimal.Parse("1,000")` даёт `1000`, а `"12 "` с пробелом разбирается. Если формат важен, задайте `NumberStyles` и `CultureInfo.InvariantCulture`.

**3. Культура по умолчанию.** На русской системе `decimal.Parse("1.5")` бросит `FormatException`, потому что разделитель - запятая. Укажите `InvariantCulture`.

**4. Общий `catch` перед частным.** Код

```
try { int.Parse("x"); }
catch (Exception) { }
catch (FormatException) { }
```

не собирается:

```
Program.cs(3,8): error CS0160: A previous catch clause already catches all exceptions of this or of a super type ('Exception')
```

**5. Пустой `catch`.** Он молча скрывает настоящие ошибки. Ловите нужный тип и сообщайте о проблеме или пробрасывайте дальше.

**6. `throw e;` вместо `throw;`.** Теряется исходная трассировка стека. Для пробрасывания в блоке `catch` пишите просто `throw;`.

**7. Публичное изменяемое поле.** Любой код меняет баланс в обход проверок. Делайте свойство с `private set` и методы с проверками.

**8. `==` для объектов класса.** Сравнивает ссылки, поэтому два объекта с равными полями не равны. Переопределите `Equals` и `GetHashCode` либо используйте `record`.

**9. Игнорирование предупреждения nullable.** Значение `string?` без проверки приводит к `NullReferenceException` во время выполнения. Не подавляйте предупреждение оператором `!` без причины; проверьте `null` или подставьте `?? ""`.

**10. Исключение как способ ветвления.** Бросок и перехват в цикле по большим данным медленны; ожидаемую ошибку проверяйте условием или `TryParse`.

<!-- section: summary -->
## Итог

Теперь вы знаете, как читать ввод до конца, как разбирать числа строго и без исключений, как устроены `try/catch/finally` и когда бросать `ArgumentException` и `InvalidOperationException`, как работает класс со свойствами и почему `null` отслеживает компилятор. Следующий шаг - курс «C#: первые шаги», где объясняется, как C# компилируется в IL и выполняется, и разница между значимыми и ссылочными типами: [Как C# собирается и запускается](lesson:csharp.first-steps.how-csharp-runs). Для подробностей: [Ввод с клавиатуры](lesson:csharp.from-zero.reading-input), [Числа из ввода: Parse](lesson:csharp.from-zero.parsing-numbers), [Исключения и TryParse](lesson:csharp.from-zero.exceptions), [Классы и объекты](lesson:csharp.from-zero.classes-basics).

<!-- section: programmer -->
## Главное для тех, кто уже программирует

* Исключения устроены как в Java: иерархия от `Exception`, `try/catch/finally`, но без проверяемых исключений и без обязательного `throws`. Условия `when` в `catch` - особенность C#.
* `TryParse` с `out`-параметром - идиома C# вместо `Optional` из Java и пары `(значение, ok)` из Go. Python бросает `ValueError`, JavaScript возвращает `NaN`.
* Разбор чисел зависит от культуры по умолчанию (в Java, Go, Python и JavaScript - нет). Для воспроизводимости используйте `InvariantCulture`.
* Свойства (`get`/`set`) заменяют пары геттер-сеттер из Java; автосвойство `{ get; private set; }` - привычный способ сделать поле доступным только на чтение извне.
* Классы - ссылочные типы с `null`, как в Java; но включённые nullable-аннотации превращают `string` и `string?` в разные типы для анализатора, и это ближе к Kotlin и TypeScript.
* `==` для объектов без переопределения - ссылки, как в Java (`==`) и JavaScript; в Python `==` вызывает `__eq__`.
* `record` даёт неизменяемые данные с равенством по значению (аналог `data class` из Kotlin и `record` из Java 16); `struct` - значимый тип (как `struct` в Go).
* Нет деструкторов в привычном смысле: ресурсы освобождают через `using` и `IDisposable`; память убирает сборщик мусора.
