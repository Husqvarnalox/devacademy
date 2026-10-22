<!-- section: simple -->
## Что проверяет эта проверка

`try`/`catch`/`finally`, иерархию исключений, собственные типы исключений, `throw` и `throw;`, `IDisposable` и `using`.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Исключения и ресурсы](lesson:csharp.fundamentals.exceptions-and-resources).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```csharp run
Console.WriteLine(Describe("12"));
Console.WriteLine(Describe("abc"));
Console.WriteLine(Describe("-1"));

using (Resource r = new Resource("файл"))
{
    Console.WriteLine("работаем с ресурсом");
}
Console.WriteLine("после using");

try
{
    Fail();
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"поймано: {ex.Message}");
}
finally
{
    Console.WriteLine("finally выполнен");
}

static string Describe(string text)
{
    try
    {
        int n = int.Parse(text);
        if (n < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(text), "число отрицательное");
        }
        return $"число {n}";
    }
    catch (FormatException)
    {
        return "не число";
    }
    catch (ArgumentOutOfRangeException ex)
    {
        return $"ошибка: {ex.ParamName}";
    }
}

static void Fail()
{
    throw new InvalidOperationException("что-то пошло не так");
}

class Resource : IDisposable
{
    private readonly string _name;

    public Resource(string name)
    {
        _name = name;
    }

    public void Dispose()
    {
        Console.WriteLine($"ресурс «{_name}» закрыт");
    }
}
```
```output
число 12
не число
ошибка: text
работаем с ресурсом
ресурс «файл» закрыт
после using
поймано: что-то пошло не так
finally выполнен
```

Метод `Describe` перехватывает разные типы исключений отдельными блоками `catch`: `int.Parse` бросает `FormatException`, а явный `throw` - `ArgumentOutOfRangeException`. Блок `using` вызывает `Dispose` при выходе из блока, даже если внутри случилась ошибка. Блок `finally` выполняется в любом случае: и после успешного кода, и после перехваченного исключения.

<!-- section: precise -->
## Что нужно уметь

* перехватывать исключения нужных типов и понимать, что блоки `catch` проверяются сверху вниз;
* описывать собственное исключение, унаследовав `Exception`, и добавлять в него данные;
* использовать `finally` и `using`, чтобы освобождать ресурсы при любом исходе;
* различать `throw;` (сохраняет трассировку) и `throw ex;` (сбрасывает её);
* различать исключения для ошибок ввода (`FormatException`, `OverflowException`) и ошибок аргументов (`ArgumentOutOfRangeException`);
* не менять состояние объекта до проверок, чтобы при ошибке он оставался корректным.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* Блок `catch (Exception)` перед конкретными типами: `CS0160`.
* Пустой блок `catch`, скрывающий ошибку.
* `throw ex;` вместо `throw;`: теряется исходная трассировка.
* Освобождение ресурса вручную после кода, который может бросить исключение: ресурс остаётся открытым.
* Перехват слишком узкого типа: `FormatException` не является `ArgumentException`.
* Изменение баланса до проверки суммы: после отказа состояние остаётся испорченным.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Исключения и ресурсы](lesson:csharp.fundamentals.exceptions-and-resources). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
