<!-- section: simple -->
## Что проверяет эта проверка

делегаты `Func` и `Action`, лямбда-выражения и захват переменных, события и безопасный вызов `?.Invoke`, `async`/`await`, `Task` и `Task.WhenAll`.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Делегаты, лямбды и события](lesson:csharp.fundamentals.delegates-lambdas-events), [async/await](lesson:csharp.fundamentals.async-await).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```csharp run
Func<int, int> twice = x => x * 2;
Func<int, int> saved = twice;
twice = x => x + 100;
Console.WriteLine($"{saved(5)} {twice(5)}");

List<Func<int>> funcs = new List<Func<int>>();
for (int i = 0; i < 3; i++)
{
    int copy = i;
    funcs.Add(() => copy);
}
Console.WriteLine(string.Join(" ", funcs.Select(f => f())));

Button button = new Button();
button.Clicked += name => Console.WriteLine($"нажата {name}");
button.Click("OK");

Console.WriteLine("до");
Task<int> task = SquareLaterAsync(6);
Console.WriteLine("между");
Console.WriteLine(await task);

static async Task<int> SquareLaterAsync(int x)
{
    await Task.Delay(20);
    return x * x;
}

class Button
{
    public event Action<string>? Clicked;

    public void Click(string name)
    {
        Clicked?.Invoke(name);
    }
}
```
```output
10 105
0 1 2
нажата OK
до
между
36
```

Переменная `saved` хранит ссылку на исходный делегат, поэтому новое значение `twice` на неё не влияет. Внутри цикла `for` каждая лямбда захватывает свою копию `copy`. Событие `Clicked` вызывается через `?.Invoke`, чтобы не упасть без подписчиков. Метод `SquareLaterAsync` возвращает управление на первом `await`, поэтому «между» печатается до результата, который дожидается `await task`.

<!-- section: precise -->
## Что нужно уметь

* передавать и возвращать функции через `Func<...>` и `Action<...>`;
* понимать, что лямбда захватывает переменную, а не её значение;
* объявлять событие через `event` и вызывать его как `Changed?.Invoke(...)`;
* знать, что `async`-метод выполняется синхронно до первого незавершённого `await`;
* собирать параллельные задачи через `Task.Run` и `Task.WhenAll`;
* помнить, что делегаты неизменяемы: присваивание новой переменной не меняет уже скопированную.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* Лямбда в цикле `for` захватывает общую переменную цикла: все вызовы видят её итоговое значение.
* Прямой вызов события `Changed(n)` без проверки на `null`: `NullReferenceException`.
* Ожидание, что `-=` с новой лямбдой удалит ранее добавленную: это другой делегат.
* `async`-метод без `await` внутри: предупреждение `CS1998`, выполнение полностью синхронное.
* Блокировка через `.Result` или `.Wait()` вместо `await`.
* Возврат `async void` вместо `Task`: исключения нельзя перехватить снаружи.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Делегаты, лямбды и события](lesson:csharp.fundamentals.delegates-lambdas-events), [async/await](lesson:csharp.fundamentals.async-await). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
