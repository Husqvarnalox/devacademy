<!-- section: simple -->
## Ждать, не блокируя поток

Операции ввода-вывода (сеть, диск, таймер) занимают время, в течение которого процессор свободен. **Асинхронность** позволяет не держать поток заблокированным в ожидании: метод «приостанавливается» на `await`, поток возвращается к другим делам, а когда операция завершается, выполнение продолжается дальше.

Ключевые слова:

* `async` помечает метод, внутри которого можно писать `await`;
* `await` приостанавливает метод до завершения задачи и возвращает её результат (или выбрасывает исключение);
* **`Task`** - объект «работа, которая ещё идёт» (без результата), **`Task<T>`** - то же с результатом типа `T`.

Асинхронный метод выглядит как обычный последовательный код, но компилятор превращает его в **конечный автомат**: каждый `await` - точка, где метод может «заснуть» и позже продолжиться. Это не потоки: `async` сам по себе не запускает ничего в другом потоке, а `await` не блокирует поток.

Три практических правила: ожидайте задачи через `await`, а не `.Result`; передавайте `CancellationToken`, чтобы работу можно было отменить; запускайте независимые операции параллельно (`Task.WhenAll`), а не последовательными `await`.

<!-- section: precise -->
## Точные правила

**Модель выполнения.**

* Асинхронный метод выполняется **синхронно до первого `await`** на незавершённой задаче, затем возвращает вызывающему `Task`. Остаток метода (продолжение) выполняется после завершения ожидаемой операции.
* Конечный автомат хранит локальные переменные и текущую точку; если задача к моменту `await` уже завершена, метод не приостанавливается вовсе.
* Продолжение выполняется в **захваченном контексте**: если у потока есть `SynchronizationContext` или специальный `TaskScheduler` (UI-приложения), продолжение возвращается в него. В консольном приложении контекста нет, и продолжение идёт в пуле потоков.
* Возвращаемые типы: `Task`, `Task<T>`, `ValueTask`/`ValueTask<T>`, `void` (только для обработчиков событий), `IAsyncEnumerable<T>`.
* `async`-методы не могут иметь параметры `ref`, `in` и `out` (CS1988), `await` нельзя использовать внутри `lock` (CS1996), а `Span<T>` и другие `ref struct` нельзя держать через `await` (CS4007).

**Исключения и задачи.**

* Исключение в `async`-методе сохраняется в возвращённой задаче и выбрасывается при `await`. Исключение в `async void` уходит в контекст синхронизации, а без него обычно завершает процесс.
* `await` выбрасывает **одно** исключение (не все); `Task.Wait()` и `.Result` оборачивают их в `AggregateException`.
* `Task.WhenAll(tasks)` завершается, когда завершены **все** задачи; результат - массив значений **в порядке аргументов**, а не завершения. Если несколько задач упали, `await` выбросит одно исключение из них (какое именно - зависит от того, какая задача упала раньше, поэтому на него нельзя полагаться), а все остальные доступны в `task.Exception.InnerExceptions`. `Task.WhenAny` завершается при завершении первой задачи.
* Неожиданно «забытый» `await` (CS4014) - исключение и завершение задачи никто не увидит.

**Отмена.**

* `CancellationTokenSource` создаёт токен: `cts.Cancel()` или `new CancellationTokenSource(timeout)` / `CancelAfter`. Токен передают во все асинхронные API (`Task.Delay(ms, token)`, `HttpClient`...) и проверяют в своих циклах: `token.ThrowIfCancellationRequested()`.
* Отмена - **кооперативная**: она ничего не прерывает насильно, операция сама замечает токен. При отмене выбрасывается `OperationCanceledException` (для `Task.Delay` - наследник `TaskCanceledException`), а задача получает статус `Canceled`. Это нормальный сценарий, а не ошибка.

**`ConfigureAwait`.**

* `await task` по умолчанию возвращает продолжение в захваченный контекст. `await task.ConfigureAwait(false)` разрешает продолжить где угодно.
* **Библиотечный код** обычно использует `ConfigureAwait(false)`: он не должен зависеть от UI-контекста, это быстрее и защищает вызывающего от взаимоблокировки. **Код приложения** (обработчики UI) оставляет контекст по умолчанию, если дальше нужен доступ к элементам интерфейса. В консольных приложениях и в ASP.NET Core контекста синхронизации нет, поэтому разницы по поведению нет.

**`.Result` и `.Wait()`: взаимоблокировка.** Если поток, у которого есть однопоточный контекст (UI), блокируется на `.Result`/`.Wait()`, а продолжение должно вернуться именно в этот поток, получается **deadlock**: поток ждёт задачу, а задача ждёт поток. Лекарство: `await` вместо блокировки (async «до самого верха») или `ConfigureAwait(false)` в нижних слоях. Блокировка на задаче вообще нежелательна: она занимает поток пула, ради экономии которого и придумана асинхронность.

**`ValueTask<T>`.** Структура, которая может содержать готовый результат без выделения объекта `Task`. Нужна для методов, которые часто завершаются синхронно (кеш). Ограничения: ожидать можно один раз, нельзя ожидать параллельно из нескольких мест и нельзя вызывать `.Result` до завершения; если сомневаетесь - возвращайте `Task<T>`.

**`async void`.** Только для обработчиков событий. Исключение нельзя перехватить снаружи, нельзя дождаться завершения, юнит-тест не увидит ошибку.

**Производительность.** Асинхронный метод выделяет небольшие объекты (автомат, `Task`); на горячем пути, который чаще завершается синхронно, помогает `ValueTask`. Параллельный запуск (`Task.WhenAll`) сокращает время до длительности самой долгой операции вместо суммы.

<!-- section: examples -->
## Примеры

**Пример 1. Синхронная часть, продолжение и параллельность.**

```csharp run
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

static async Task<int> Work()
{
    Console.WriteLine("до await, поток пула: " + Thread.CurrentThread.IsThreadPoolThread);
    await Task.Delay(10);
    Console.WriteLine("после await, поток пула: " + Thread.CurrentThread.IsThreadPoolThread);
    return 1;
}

Task<int> t = Work();
Console.WriteLine("управление вернулось вызывающему");
await t;

static async Task<string> Fetch(string name, int delayMs)
{
    await Task.Delay(delayMs);
    return name;
}

// результаты WhenAll идут в порядке аргументов, а не завершения
string[] results = await Task.WhenAll(Fetch("a", 60), Fetch("b", 5), Fetch("c", 30));
Console.WriteLine(string.Join(",", results));

var sw = Stopwatch.StartNew();
await Task.WhenAll(Fetch("x", 200), Fetch("y", 200), Fetch("z", 200));
long parallel = sw.ElapsedMilliseconds;
sw.Restart();
await Fetch("x", 200);
await Fetch("y", 200);
await Fetch("z", 200);
long sequential = sw.ElapsedMilliseconds;
Console.WriteLine("параллельно быстрее, чем по очереди: " + (parallel < sequential));
Console.WriteLine("по очереди не быстрее 590 мс: " + (sequential >= 590));
```
```output
до await, поток пула: False
управление вернулось вызывающему
после await, поток пула: True
a,b,c
параллельно быстрее, чем по очереди: True
по очереди не быстрее 590 мс: True
```

Тело метода выполняется синхронно до первого `await`, а продолжение - в потоке пула. Три параллельные задачи по 200 мс занимают ~200 мс, те же три по очереди - ~600 мс. Пример сравнивает два замера между собой, а не с точным числом: на загруженной машине время плавает.

**Пример 2. Отмена и исключения в `WhenAll`.**

```csharp run
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

static async Task<string> Fetch(string name, int delayMs, CancellationToken ct)
{
    await Task.Delay(delayMs, ct);
    return name;
}

using var cts = new CancellationTokenSource(100);
Task<string> fast = Fetch("fast", 10, cts.Token);
Task<string> slow = Fetch("slow", 2000, cts.Token);
try
{
    await Task.WhenAll(fast, slow);
}
catch (OperationCanceledException e)
{
    Console.WriteLine("поймано " + e.GetType().Name);
}
Console.WriteLine($"{fast.Status} {slow.Status} {slow.IsCanceled}");

static async Task Fail(string message, int delayMs)
{
    await Task.Delay(delayMs);
    throw new InvalidOperationException(message);
}

Task all = Task.WhenAll(Fail("one", 10), Fail("two", 30));
try
{
    await all;
}
catch (Exception e)
{
    // Какое из двух исключений поймал await, зависит от порядка падения задач, поэтому выводим тип и отсортированный список всех ошибок.
    string messages = string.Join(", ", all.Exception!.InnerExceptions.Select(x => x.Message).Order());
    Console.WriteLine($"await бросил {e.GetType().Name}; всего ошибок: {all.Exception!.InnerExceptions.Count} ({messages})");
}

try
{
    all.Wait();
}
catch (AggregateException e)
{
    Console.WriteLine($"Wait бросил AggregateException из {e.InnerExceptions.Count}");
}
```
```output
поймано TaskCanceledException
RanToCompletion Canceled True
await бросил InvalidOperationException; всего ошибок: 2 (one, two)
Wait бросил AggregateException из 2
```

**Пример 3. Блокировка на `.Wait()`, `ConfigureAwait` и `ValueTask`.** Однопоточный контекст имитируется классом `Pump`: он ставит продолжения в очередь, а единственный «его» поток блокирован на `Wait`.

```csharp run
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

SynchronizationContext.SetSynchronizationContext(new Pump());

static async Task WorkAsync(bool captureContext)
{
    await Task.Delay(20).ConfigureAwait(captureContext);
}

Task captured = WorkAsync(captureContext: true);
Console.WriteLine("продолжение вернулось в контекст, Wait(300): " + captured.Wait(300));

Task free = WorkAsync(captureContext: false);
Console.WriteLine("ConfigureAwait(false), Wait(300): " + free.Wait(300));

SynchronizationContext.SetSynchronizationContext(null);

static ValueTask<int> Lookup(bool cached) =>
    cached
        ? new ValueTask<int>(42)
        : new ValueTask<int>(Task.Run(async () => { await Task.Delay(5); return 7; }));

ValueTask<int> hit = Lookup(true);
Console.WriteLine($"{hit.IsCompletedSuccessfully} {await hit}");
Console.WriteLine(await Lookup(false));

class Pump : SynchronizationContext
{
    private readonly BlockingCollection<(SendOrPostCallback, object?)> _queue = new();

    public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));
}
```
```output
продолжение вернулось в контекст, Wait(300): False
ConfigureAwait(false), Wait(300): True
True 42
7
```

В первом случае продолжение поставлено в очередь контекста, но поток контекста занят `Wait` - классическая взаимоблокировка (завершается только по тайм-ауту `Wait(300)`). С `ConfigureAwait(false)` продолжение идёт в пуле, и всё завершается.

<!-- section: mistakes -->
## Типичные ошибки

* **`.Result` и `.Wait()`.** Блокируют поток и в приложениях с однопоточным контекстом дают взаимоблокировку; используйте `await`.
* **`async void` вне обработчиков событий.** Ошибки не поймать, завершения не дождаться; возвращайте `Task`.
* **Последовательные `await` для независимых операций.** Суммируют время; запускайте задачи, затем `await Task.WhenAll(...)`.
* **Не передавать `CancellationToken`.** Операцию нельзя отменить, а запросы «висят» после отключения клиента.
* **Считать отмену ошибкой.** `OperationCanceledException` - штатный путь; не журналируйте её как сбой.
* **Забытый `await`.** Предупреждение CS4014: исключение задачи потеряется, а метод вернёт управление раньше времени.
* **`Task.Run` вокруг асинхронного ввода-вывода.** Лишний поток пула без пользы; `Task.Run` нужен для тяжёлых вычислений процессора.
* **`await` внутри `lock`.** Не компилируется (CS1996); используйте `SemaphoreSlim.WaitAsync()`.
* **Ожидание `ValueTask` дважды или `.Result` до завершения.** Поведение не определено; для таких сценариев используйте `Task` или вызывайте `AsTask()`.
* **Ожидание в конструкторе или свойстве.** `async`-конструкторов нет; используйте фабричный метод `static async Task<T> CreateAsync()`.
* **Забыть `ConfigureAwait(false)` в библиотеке или поставить его в UI-коде, которому нужен контекст.** Правило: библиотека - `false`, код приложения - по умолчанию.

<!-- section: summary -->
## Итоги

* `async/await` превращает метод в конечный автомат: до первого незавершённого `await` код синхронный, продолжение идёт позже (в консоли - в пуле потоков).
* `Task.WhenAll` запускает параллельно и возвращает результаты в порядке аргументов; ошибки лежат в `Exception.InnerExceptions`.
* Отмена кооперативна: передавайте `CancellationToken` вниз по цепочке; результат - `OperationCanceledException` и статус `Canceled`.
* Не блокируйтесь на `.Result`/`.Wait()`; в библиотеках используйте `ConfigureAwait(false)`; `async void` - только для событий.
* `ValueTask<T>` экономит выделения при частых синхронных завершениях, но имеет ограничения; по умолчанию возвращайте `Task<T>`.

Сравните с [промисами и async в JavaScript](lesson:javascript.fundamentals.promises-and-async), [горутинами в Go](lesson:go.fundamentals.goroutines-and-channels) и [asyncio в Python](lesson:python.concurrency.asyncio-basics). Предыдущий урок: [делегаты и события](lesson:csharp.fundamentals.delegates-lambdas-events).

<!-- section: facts -->
## Факты

* Для методов `async` компилятор генерирует конечный автомат; ключевое слово `async` само по себе не создаёт потоков.
* В консольном приложении .NET нет `SynchronizationContext`, поэтому продолжение после `await` выполняется в пуле потоков; в UI-приложениях оно по умолчанию возвращается в поток интерфейса.
