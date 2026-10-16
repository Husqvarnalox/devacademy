<!-- section: simple -->
## Когда что-то пошло не так

**Исключение** - объект, который описывает ошибку и «прерывает» обычный ход программы: управление уходит вверх по стеку вызовов к ближайшему подходящему `catch`. Если такого нет, программа аварийно завершается. Исключения нужны для **исключительных** ситуаций (нет файла, повреждённые данные, нарушенный контракт), а не для обычного управления потоком: ввод пользователя «abc» вместо числа - ожидаемая ситуация, для неё есть `int.TryParse`.

Три конструкции работают вместе:

* `try / catch` - попробовать и обработать;
* `finally` - выполнить код очистки **в любом случае**;
* `using` - гарантировать вызов `Dispose()` для ресурса (файл, соединение, блокировка); это `try/finally`, написанный за вас.

Ресурсы, которые нельзя оставить «висеть» (файловые дескрипторы, сокеты), освобождаются явно, а не ждут сборщика мусора. Об этом правило: **кто открыл - тот закрывает**, и закрывает через `using`.

<!-- section: precise -->
## Точные правила

**Иерархия.** Все исключения наследуют `System.Exception`. Основные ветки: `SystemException` (ошибки среды выполнения), `ArgumentException` (`ArgumentNullException`, `ArgumentOutOfRangeException`), `InvalidOperationException` (недопустимое состояние объекта, в том числе изменение коллекции во время `foreach`), `FormatException`, `ArithmeticException` (`DivideByZeroException`), `IOException`, `OperationCanceledException` (и её наследник `TaskCanceledException`), `KeyNotFoundException`. Свои исключения наследуйте от `Exception`; от `ApplicationException` не нужно.

**`catch`.**

* Блоки проверяются сверху вниз, срабатывает **первый** подходящий; порядок «от частного к общему». Если блок с базовым типом стоит выше блока с производным, компилятор сообщает об ошибке.
* `catch (Exception)` без необходимости скрывает ошибки. Ловите конкретные типы и только те, которые умеете обработать.
* **Фильтр исключений**: `catch (T e) when (условие)`. Условие проверяется **до** того, как стек развернётся: к этому моменту ни `finally`, ни `Dispose()` внутренних блоков ещё не выполнялись. Если фильтр вернул `false`, блок пропускается, а исключение продолжает подниматься (стек при этом не искажается). Фильтр удобен для условий вроде кода ошибки; не помещайте в него побочные эффекты, кроме, например, журналирования с `return false`.

**`throw` и `throw ex`.** Внутри `catch` оператор `throw;` пробрасывает то же исключение с **сохранением** трассы стека. `throw ex;` сбрасывает трассу на текущую строку (анализатор CA2200 предупреждает). Чтобы добавить контекст, оберните: `throw new InvalidOperationException("config", inner)`: оригинал остаётся в `InnerException`.

**Исключение или Try-метод.**

* Исключения дороги (создание объекта, сбор трассы стека, разворачивание), их нельзя использовать для штатных веток логики.
* Для частых ожидаемых отказов у API есть Try-шаблон: `bool TryXxx(..., out T result)` (`int.TryParse`, `Dictionary.TryGetValue`). Свои Try-методы следуют той же форме: возвращают `false` вместо исключения, а `out`-значение при неудаче равно `default`.
* Проверка аргументов своего API: `ArgumentNullException.ThrowIfNull(arg)` и `ArgumentOutOfRangeException.ThrowIfNegative(...)` - охранные методы стандартной библиотеки.

**`finally`.**

* Выполняется и при нормальном завершении блока, и при исключении, и при `return`/`break`/`continue`, пытающихся выйти из `try`.
* `return` внутри `finally` запрещён (CS0157).
* Исключение, брошенное из `finally`, **заменяет** исходное; то же относится к исключению из `Dispose()` в `using`: оригинальная ошибка теряется. Поэтому `Dispose` не должен бросать исключения.
* Не выполняется, если процесс завершён принудительно (`Environment.FailFast`, убит процесс, неперехваченная фатальная ошибка вроде переполнения стека).

**`using` и `IDisposable`.**

* Оператор `using (var r = new R()) { ... }` и объявление `using var r = new R();` (до конца текущей области) разворачиваются в `try/finally` с вызовом `Dispose()`. Для `null`-ресурса вызова нет.
* Несколько ресурсов освобождаются в **обратном порядке** объявления.
* `IAsyncDisposable` и `await using` - то же для асинхронной очистки (`DisposeAsync()` возвращает `ValueTask`).
* Реализация `Dispose()` должна быть **идемпотентной** (повторный вызов безвреден). Финализатор нужен только классам, напрямую владеющим неуправляемым ресурсом; обычно достаточно `sealed`-класса с `Dispose()`.
* Сборщик мусора освобождает **память**, но не вызывает `Dispose()` за вас.

<!-- section: examples -->
## Примеры

**Пример 1. Фильтр, `using` и `finally`: кто первым?**

```csharp run
using System;

static bool Log(string s)
{
    Console.WriteLine("filter " + s);
    return true;
}

try
{
    try
    {
        using var r = new Res("A");
        throw new InvalidOperationException("boom");
    }
    finally
    {
        Console.WriteLine("inner finally");
    }
}
catch (InvalidOperationException e) when (Log(e.Message))
{
    Console.WriteLine("caught " + e.Message);
}

class Res(string name) : IDisposable
{
    public void Dispose() => Console.WriteLine("dispose " + name);
}
```
```output
filter boom
dispose A
inner finally
caught boom
```

Фильтр выполнился **раньше** `dispose` и `inner finally`: сначала решается, кто обработает исключение, потом стек разворачивается.

**Пример 2. `throw` против `throw ex`, обёртка и Try-шаблон.**

```csharp run
using System;
using System.Globalization;

static void Thrower() => throw new Exception("x");

try { try { Thrower(); } catch (Exception) { throw; } }
catch (Exception e) { Console.WriteLine("throw;    видит Thrower: " + e.StackTrace!.Contains("Thrower")); }

try { try { Thrower(); } catch (Exception ex) { throw ex; } }
catch (Exception e) { Console.WriteLine("throw ex;  видит Thrower: " + e.StackTrace!.Contains("Thrower")); }

try
{
    try { int.Parse("z", CultureInfo.InvariantCulture); }
    catch (FormatException f) { throw new InvalidOperationException("config", f); }
}
catch (Exception e)
{
    Console.WriteLine($"{e.Message} <- {e.InnerException!.GetType().Name}");
}

Console.WriteLine(int.TryParse("12x", CultureInfo.InvariantCulture, out int v) + " " + v);

try { ArgumentNullException.ThrowIfNull((string?)null, "name"); }
catch (ArgumentNullException e) { Console.WriteLine(e.ParamName); }
```
```output
throw;    видит Thrower: True
throw ex;  видит Thrower: False
config <- FormatException
False 0
name
```

**Пример 3. Порядок освобождения, потеря исходной ошибки, `await using`.**

```csharp run
using System;
using System.Threading.Tasks;

using (var a = new Res("a"))
using (var b = new Res("b"))
{
    Console.WriteLine("body");
}

try
{
    using var r = new Res("bad", throwOnDispose: true);
    throw new Exception("исходная ошибка");
}
catch (Exception e)
{
    Console.WriteLine("пойман: " + e.Message);
}

await using (var ar = new Res("async"))
{
    Console.WriteLine("in");
}

static int F()
{
    try { return 1; }
    finally { Console.WriteLine("finally перед возвратом"); }
}
Console.WriteLine(F());

class Res(string name, bool throwOnDispose = false) : IDisposable, IAsyncDisposable
{
    public void Dispose()
    {
        Console.WriteLine("dispose " + name);
        if (throwOnDispose) throw new Exception("ошибка из Dispose");
    }

    public ValueTask DisposeAsync()
    {
        Console.WriteLine("disposeAsync " + name);
        return ValueTask.CompletedTask;
    }
}
```
```output
body
dispose b
dispose a
dispose bad
пойман: ошибка из Dispose
in
disposeAsync async
finally перед возвратом
1
```

Ресурсы закрываются в обратном порядке (`b`, затем `a`). Исходная ошибка замещена исключением из `Dispose()`.

<!-- section: mistakes -->
## Типичные ошибки

* **`catch (Exception)` «на всякий случай».** Скрывает ошибки программиста (`NullReferenceException`, `IndexOutOfRangeException`); ловите конкретные типы.
* **Пустой `catch`.** Ошибка исчезает без следа; как минимум журналируйте и пробрасывайте.
* **`throw ex;` вместо `throw;`.** Трасса стека теряет место настоящей ошибки.
* **Исключения как управляющий поток.** `try { int.Parse(...) } catch` в цикле вместо `TryParse` медленно и шумно.
* **Забытый `using`.** Если между созданием ресурса и `Close()` произойдёт исключение, ресурс утечёт.
* **Исключения из `Dispose()` и `finally`.** Замещают исходную ошибку.
* **`return` и `throw` в `finally`.** `return` запрещён (CS0157), `throw` маскирует исходное исключение.
* **Общий блок выше частного.** Недостижимый `catch` не компилируется (CS0160); ставьте частные типы выше.
* **Строить логику на тексте `Message`.** Сообщения могут меняться и зависят от локали; используйте тип, свойства (`ParamName`, `HResult`) и фильтр `when`.
* **Терять `InnerException`.** При обёртывании всегда передавайте исходное исключение вторым аргументом.
* **Не освобождать то, чем владеете.** Сборщик мусора не вызывает `Dispose()` вместо вас.

<!-- section: summary -->
## Итоги

* Исключения - для исключительных ситуаций; ожидаемые отказы выражайте Try-методами (`TryParse`).
* Ловите конкретные типы, фильтр `when` проверяется до разворачивания стека и не искажает трассу.
* `throw;` сохраняет трассу, `throw ex;` сбрасывает; при обёртке сохраняйте `InnerException`.
* `finally` и `using` выполняются при любом выходе из блока; ресурсы закрываются в обратном порядке, а исключение из `Dispose()` или `finally` замещает исходное.
* `await using` и `IAsyncDisposable` - асинхронный аналог; `Dispose()` идемпотентен и не бросает исключений.

Сравните с [исключениями в JavaScript](lesson:javascript.fundamentals.errors-and-exceptions) и [ошибками как значениями в Go](lesson:go.fundamentals.functions-and-errors). Дальше: [делегаты, лямбды и события](lesson:csharp.fundamentals.delegates-lambdas-events).

<!-- section: facts -->
## Факты

* Исключение в фильтре `when` подавляется и трактуется как `false`; фильтр выполняется до выполнения внутренних блоков `finally`.
* Оператор `using` компилятор разворачивает в `try/finally` с вызовом `Dispose()`; объявление `using var` действует до конца области видимости.
