<!-- section: simple -->
## Контейнер сервисов

Обработчик редко работает в одиночку: ему нужны часы, репозиторий, отправитель писем. **Внедрение зависимостей** (dependency injection, DI) решает, откуда они берутся: вы **регистрируете** сервисы один раз при сборке приложения, а фреймворк **создаёт** их и **подставляет** в параметры обработчиков и конструкторы других сервисов.

```csharp
builder.Services.AddSingleton<IClock, SystemClock>();   // регистрация
var app = builder.Build();

app.MapGet("/now", (IClock clock) => clock.Now());      // внедрение в обработчик
```

Главный вопрос при регистрации - **время жизни**: сколько живёт созданный экземпляр.

* **Singleton** - один экземпляр на всё приложение.
* **Scoped** - один экземпляр на **область**; в веб-приложении область создаётся на каждый HTTP-запрос.
* **Transient** - новый экземпляр при каждом запросе к контейнеру.

Всё, что вы записываете в поля сервиса, живёт ровно столько, сколько сам сервис: данные «на запрос» хранят в scoped-сервисе, общий кэш - в singleton, а лёгкий объект без состояния - transient. Ошибка в выборе времени жизни не ломает компиляцию, но даёт гонки данных между запросами или «залипшие» значения.

<!-- section: precise -->
## Точные правила

**Регистрация.** `IServiceCollection` (`builder.Services`) хранит список дескрипторов: тип сервиса, реализация (тип, готовый экземпляр или фабрика) и время жизни. Методы `AddSingleton`, `AddScoped`, `AddTransient` добавляют дескриптор. Контейнер собирается на `builder.Build()`; после этого коллекцию менять бессмысленно.

**Внедрение.** Контейнер выбирает конструктор с наибольшим числом параметров, которые он умеет удовлетворить, и рекурсивно создаёт зависимости. В `minimal APIs` параметр обработчика берётся из контейнера, если тип зарегистрирован; атрибутом `[FromServices]` это можно указать явно. В классах можно использовать первичный конструктор: `class Auditor(RequestContext context)`.

**Область запроса.** Для каждого HTTP-запроса создаётся область (`IServiceScope`): scoped-сервисы внутри неё разделяются, transient создаются по запросу, а в конце запроса область освобождается (`Dispose`). Свою область можно создать вручную через `IServiceScopeFactory.CreateScope()` - например, в фоновой задаче.

**Освобождение.** Контейнер вызывает `Dispose` (или `DisposeAsync`) у тех `IDisposable`-сервисов, которые **создал сам**: scoped и transient - в конце области в порядке, обратном созданию; singleton - при остановке приложения. Экземпляр, который вы передали готовым в `AddSingleton(instance)`, контейнер не освобождает.

**Захваченная зависимость (captive dependency).** Если singleton принимает в конструкторе scoped-сервис, тот «застревает» в singleton на всё время жизни приложения и перестаёт быть «на запрос». Контейнер умеет ловить это: параметр `ValidateScopes` запрещает singleton потреблять scoped-сервисы и разрешать scoped из корневого провайдера, а `ValidateOnBuild` проверяет все регистрации при `Build()`. Для окружения Development оба режима включены по умолчанию, в Production - выключены. Включить явно: `builder.Host.UseDefaultServiceProvider(o => { o.ValidateScopes = true; o.ValidateOnBuild = true; })`. Правило: сервис может зависеть только от сервисов с таким же или более долгим временем жизни.

**Несколько реализаций.** Каждый `Add...` добавляет ещё один дескриптор. Запрос одного `IRule` вернёт **последнюю** регистрацию, а `IEnumerable<IRule>` - **все**, в порядке регистрации. `TryAdd...` (из `Microsoft.Extensions.DependencyInjection.Extensions`) добавляет сервис только если этот тип ещё не зарегистрирован - так библиотеки задают умолчания, которые приложение может переопределить; `Replace` заменяет регистрацию.

**Сервисы с ключом.** `AddKeyedSingleton<INotifier, SmsNotifier>("sms")` регистрирует реализацию по ключу; `[FromKeyedServices("sms")]` в параметре обработчика или конструктора подставляет её, `GetKeyedService` / `GetRequiredKeyedService` ищут по ключу, известному только во время выполнения. Сервис без ключа и сервис с ключом - разные регистрации.

**Фабрики.** `AddScoped<IReceipt>(sp => new Receipt(sp.GetRequiredService<RequestContext>().Id))` строит экземпляр кодом, когда нужны параметры, не выводимые из конструктора. Фабрика подчиняется тому же времени жизни.

<!-- section: examples -->
## Примеры

Время жизни и порядок освобождения. Сервисы нумеруются при создании и сообщают об освобождении:

```csharp run
using Microsoft.AspNetCore.TestHost;

var builder = WebApplication.CreateBuilder();
builder.WebHost.UseTestServer();
builder.Logging.ClearProviders();
var events = new List<string>();
builder.Services.AddSingleton(events);
builder.Services.AddSingleton<Shared>();
builder.Services.AddScoped<PerRequest>();
builder.Services.AddTransient<Fresh>();
var app = builder.Build();

app.MapGet("/", (Shared shared, PerRequest request, Fresh a, Fresh b, IServiceProvider sp) => new
{
    shared = shared.Id,
    request = request.Id,
    requestAgain = sp.GetRequiredService<PerRequest>().Id,
    a = a.Id,
    b = b.Id,
});

await app.StartAsync();
var client = app.GetTestClient();
for (var i = 0; i < 2; i++)
{
    events.Clear();
    Console.WriteLine(await client.GetStringAsync("/"));
    Console.WriteLine("   disposed: " + string.Join(", ", events));
}
events.Clear();
await app.DisposeAsync();
Console.WriteLine("shutdown: " + string.Join(", ", events));

class Shared(List<string> events) : IDisposable
{
    static int n;
    public int Id { get; } = Interlocked.Increment(ref n);
    public void Dispose() => events.Add($"Shared#{Id}");
}
class PerRequest(List<string> events) : IDisposable
{
    static int n;
    public int Id { get; } = Interlocked.Increment(ref n);
    public void Dispose() => events.Add($"PerRequest#{Id}");
}
class Fresh(List<string> events) : IDisposable
{
    static int n;
    public int Id { get; } = Interlocked.Increment(ref n);
    public void Dispose() => events.Add($"Fresh#{Id}");
}
```
```output
{"shared":1,"request":1,"requestAgain":1,"a":1,"b":2}
   disposed: Fresh#2, Fresh#1, PerRequest#1
{"shared":1,"request":2,"requestAgain":2,"a":3,"b":4}
   disposed: Fresh#4, Fresh#3, PerRequest#2
shutdown: Shared#1
```

Захваченная зависимость: singleton-кэш принимает scoped-подключение к «базе». Без проверок вы получаете одно и то же «подключение» во всех запросах; с проверками - ошибку:

```csharp run
using Microsoft.AspNetCore.TestHost;

async Task Try(string title, Action<WebApplicationBuilder> configure, string environment = "Production")
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
    builder.WebHost.UseTestServer();
    builder.Logging.ClearProviders();
    builder.Services.AddScoped<Db>();
    builder.Services.AddSingleton<Cache>();
    configure(builder);
    try
    {
        var app = builder.Build();
        app.MapGet("/", (Cache cache) => cache.Db.Id);
        await app.StartAsync();
        var client = app.GetTestClient();
        Console.WriteLine($"{title}: ответы {await client.GetStringAsync("/")}, {await client.GetStringAsync("/")}");
        await app.StopAsync();
    }
    catch (Exception e)
    {
        Console.WriteLine($"{title}: {e.GetType().Name}");
        Console.WriteLine("   " + (e.InnerException ?? e).Message);
    }
}

await Try("Production, без проверок", _ => { });
await Try("ValidateScopes", b => b.Host.UseDefaultServiceProvider(o => o.ValidateScopes = true));
await Try("ValidateOnBuild", b => b.Host.UseDefaultServiceProvider(o => { o.ValidateScopes = true; o.ValidateOnBuild = true; }));
await Try("Development по умолчанию", _ => { }, "Development");

class Db
{
    static int n;
    public int Id { get; } = Interlocked.Increment(ref n);
}
class Cache(Db db)
{
    public Db Db { get; } = db;
}
```
```output
Production, без проверок: ответы 1, 1
ValidateScopes: InvalidOperationException
   Cannot consume scoped service 'Db' from singleton 'Cache'.
ValidateOnBuild: AggregateException
   Error while validating the service descriptor 'ServiceType: Cache Lifetime: Singleton ImplementationType: Cache': Cannot consume scoped service 'Db' from singleton 'Cache'.
Development по умолчанию: AggregateException
   Error while validating the service descriptor 'ServiceType: Cache Lifetime: Singleton ImplementationType: Cache': Cannot consume scoped service 'Db' from singleton 'Cache'.
```

Несколько реализаций, `TryAdd`, сервисы с ключом и фабрика:

```csharp run
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder();
builder.WebHost.UseTestServer();
builder.Logging.ClearProviders();
var services = builder.Services;

services.AddSingleton<IGreeter, Formal>();
services.AddSingleton<IGreeter, Casual>();
services.TryAddSingleton<IGreeter, Shouting>();      // уже зарегистрирован: ничего не добавит
services.TryAddSingleton<IClock, FixedClock>();      // не было: добавит

services.AddKeyedSingleton<INotifier, EmailNotifier>("email");
services.AddKeyedSingleton<INotifier, SmsNotifier>("sms");

services.AddScoped<RequestContext>();
services.AddScoped<IReceipt>(sp => new Receipt($"R-{sp.GetRequiredService<RequestContext>().Id:000}"));
var app = builder.Build();

app.MapGet("/one", (IGreeter greeter) => greeter.Greet("Анна"));
app.MapGet("/all", (IEnumerable<IGreeter> all) => all.Select(g => g.Greet("Анна")));
app.MapGet("/clock", (IClock clock) => clock.Now);
app.MapGet("/sms", ([FromKeyedServices("sms")] INotifier n) => n.Send("duty"));
app.MapGet("/by/{key}", (string key, IServiceProvider sp) =>
    sp.GetKeyedService<INotifier>(key)?.Send("x") ?? $"нет сервиса с ключом {key}");
app.MapGet("/unkeyed", (IServiceProvider sp) => sp.GetService<INotifier>() is null ? "без ключа сервиса нет" : "есть");
app.MapGet("/receipt", (IReceipt receipt, RequestContext ctx) => $"{receipt.Number} (контекст {ctx.Id})");

await app.StartAsync();
var client = app.GetTestClient();
foreach (var url in new[] { "/one", "/all", "/clock", "/sms", "/by/email", "/by/fax", "/unkeyed", "/receipt", "/receipt" })
    Console.WriteLine($"{url} -> {await client.GetStringAsync(url)}");
await app.StopAsync();

interface IGreeter { string Greet(string name); }
class Formal : IGreeter { public string Greet(string name) => $"Здравствуйте, {name}."; }
class Casual : IGreeter { public string Greet(string name) => $"Привет, {name}!"; }
class Shouting : IGreeter { public string Greet(string name) => $"{name.ToUpperInvariant()}!!!"; }
interface IClock { string Now { get; } }
class FixedClock : IClock { public string Now => "2025-01-01T00:00"; }
interface INotifier { string Send(string to); }
class EmailNotifier : INotifier { public string Send(string to) => $"email to {to}"; }
class SmsNotifier : INotifier { public string Send(string to) => $"sms to {to}"; }
class RequestContext { static int n; public int Id { get; } = Interlocked.Increment(ref n); }
interface IReceipt { string Number { get; } }
record Receipt(string Number) : IReceipt;
```
```output
/one -> Привет, Анна!
/all -> ["Здравствуйте, Анна.","Привет, Анна!"]
/clock -> 2025-01-01T00:00
/sms -> sms to duty
/by/email -> email to x
/by/fax -> нет сервиса с ключом fax
/unkeyed -> без ключа сервиса нет
/receipt -> R-001 (контекст 1)
/receipt -> R-002 (контекст 2)
```

<!-- section: mistakes -->
## Типичные ошибки

* **Singleton с состоянием запроса.** Поле «текущий пользователь» в singleton общее для всех одновременных запросов. Данные запроса хранят в scoped-сервисе или передают параметрами.
* **Захваченная зависимость.** Singleton, принимающий scoped-сервис (подключение к базе, `DbContext`), закрепляет первый экземпляр навсегда. В Production это никак не проявится, пока не включена проверка областей: запускайте тесты с `ValidateScopes` и `ValidateOnBuild`.
* **Разрешать scoped-сервис из корня.** `app.Services.GetRequiredService<Scoped>()` при включённой проверке областей - исключение. Создайте область: `using var scope = app.Services.CreateScope();`.
* **Забыть освободить созданную область.** Область из `CreateScope()` нужно закрывать (`using`), иначе `IDisposable`-сервисы внутри неё не освободятся.
* **Ждать, что `AddX` заменит регистрацию.** Повторный `AddSingleton<IGreeter, ...>` не заменяет, а добавляет: один `IGreeter` вернёт последнюю регистрацию, а `IEnumerable<IGreeter>` - все. Заменяют через `Replace`, не заменять при наличии - через `TryAdd`.
* **Ключевой и безключевой сервис - одно и то же.** Зарегистрировав `INotifier` только по ключам, обычный `INotifier` вы не получите.
* **Служебный локатор.** Запрашивать сервисы из `IServiceProvider` внутри бизнес-кода скрывает зависимости; обычно нужные сервисы лучше принимать параметрами конструктора или обработчика.

<!-- section: summary -->
## Итоги

* Регистрация (`AddSingleton`/`AddScoped`/`AddTransient`) описывает, как создать сервис и как долго он живёт; внедрение происходит в параметры обработчиков и конструкторы.
* Singleton - один на приложение, Scoped - один на запрос (область), Transient - новый при каждом обращении.
* Сервис может зависеть только от сервисов того же или более долгого времени жизни; `ValidateScopes` и `ValidateOnBuild` ловят нарушения.
* Контейнер освобождает созданные им `IDisposable`-сервисы: scoped и transient - в конце области в обратном порядке, singleton - при остановке.
* `IEnumerable<T>` даёт все регистрации, `T` - последнюю; `TryAdd...` не перезаписывает; ключевые сервисы - `[FromKeyedServices]` и `GetKeyedService`.

Дальше: [конвейер middleware](lesson:csharp.aspnetcore.middleware-pipeline).
