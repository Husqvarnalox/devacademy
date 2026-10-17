<!-- section: simple -->
## Конвейер как цепочка слоёв

Запрос в ASP.NET Core проходит через **конвейер middleware**: цепочку слоёв, каждый из которых получает `HttpContext` и функцию `next`. Слой может сделать что-то **до** `next`, вызвать `next` (передать запрос глубже), а затем сделать что-то **после** - на обратном пути ответа. Если `next` не вызвать, запрос дальше не пойдёт: слой **замыкает конвейер** и отвечает сам.

```csharp
app.Use(async (context, next) =>
{
    // до: запрос идёт сверху вниз
    await next();
    // после: ответ идёт снизу вверх
});
```

Представьте матрёшку: внешний слой открывается первым, а закрывается последним. Поэтому **порядок регистрации - это порядок выполнения**: слой, добавленный раньше, видит запрос раньше и ответ позже. Типичные задачи слоёв - сквозные: идентификатор запроса, проверка ключа API, логирование, обработка ошибок, сжатие.

Конечные точки (`MapGet` и другие) - это, по сути, последний слой: маршрутизация выбирает обработчик, и он завершает цепочку. В `WebApplication` вызывать `UseRouting` вручную не нужно: маршрутизация и выполнение конечных точек подключаются автоматически - первая в начале конвейера, вторая в конце, а ваши `Use...` оказываются между ними.

<!-- section: precise -->
## Точные правила

**Четыре способа добавить слой.**

* `app.Use(async (context, next) => ...)` - слой с вызовом `next`, самый частый вариант для небольших вещей.
* `app.Run(context => ...)` - **терминальный** слой: у него нет `next`, он всегда завершает конвейер. Всё, что зарегистрировано после `Run`, недостижимо.
* `app.Map("/prefix", branch => ...)` - **ответвление** по префиксу пути: совпавший запрос уходит в отдельный подконвейер и в основную цепочку не возвращается. Префикс отрезается от `Path` и переносится в `PathBase`.
* `app.UseWhen(predicate, branch => ...)` - ответвление по условию, которое **возвращается** в основной конвейер, если ветка не замкнула его сама.

**Короткое замыкание.** Слой, не вызвавший `next`, сам формирует ответ: код состояния, заголовки, тело. Слои выше по конвейеру при этом всё равно выполнят свою часть «после `next`» - вызов вернётся к ним обычным образом.

**Слой-класс по соглашению.** Подключается через `app.UseMiddleware<T>()`. Требования: конструктор с `RequestDelegate next` (плюс сервисы, которые живут не короче приложения - singleton), публичный метод `Invoke` или `InvokeAsync(HttpContext context, ...)`, возвращающий `Task`. Экземпляр класса создаётся **один раз** на приложение, поэтому scoped-сервисы в конструктор нельзя: их принимают **параметрами `InvokeAsync`** - они разрешаются из контейнера текущего запроса.

**Ответ нельзя менять после начала.** Как только первые байты тела или заголовки отправлены (`response.HasStarted == true`), код состояния и заголовки уже недоступны для записи. Поэтому заголовок «после `next`» не поставить; если он нужен, регистрируйте его заранее через `context.Response.OnStarting(...)` - обратный вызов сработает прямо перед отправкой заголовков.

**Порядок и маршрутизация.** Конечную точку выбирает маршрутизация в начале конвейера, поэтому слои, зарегистрированные в `WebApplication` через `Use...`, уже могут прочитать выбранную точку и её **метаданные**: `context.GetEndpoint()`. Это основа авторизации и CORS: они смотрят атрибуты и метаданные конечной точки.

**Фильтры конечных точек.** `AddEndpointFilter` оборачивает только **обработчик** конкретной точки (или группы): фильтр видит уже привязанные аргументы и результат обработчика. Middleware работает со всем запросом целиком до маршрутизации к обработчику и после; фильтры - с вызовом обработчика. Для «что-то на каждый запрос» берите middleware, для «что-то вокруг этого обработчика» - фильтр.

<!-- section: examples -->
## Примеры

Порядок до и после `next`, `Run`, `Map` и `UseWhen`. Каждый слой пишет событие в список, а в конце мы печатаем его для каждого запроса:

```csharp run
using Microsoft.AspNetCore.TestHost;

var builder = WebApplication.CreateBuilder();
builder.WebHost.UseTestServer();
builder.Logging.ClearProviders();
var app = builder.Build();
var log = new List<string>();

app.Use(async (context, next) =>
{
    log.Add("A before");
    await next();
    log.Add("A after");
});
app.Use(async (context, next) =>
{
    log.Add("B before");
    if (context.Request.Query.ContainsKey("stop"))
    {
        log.Add("B stops");
        await context.Response.WriteAsync("stopped by B");
        return;
    }
    await next();
    log.Add("B after");
});
app.UseWhen(c => c.Request.Path.StartsWithSegments("/api"), branch => branch.Use(async (context, next) =>
{
    log.Add("api-branch");
    await next();
}));
app.Map("/ping", branch => branch.Run(context =>
{
    log.Add($"ping path={context.Request.Path} base={context.Request.PathBase}");
    return context.Response.WriteAsync("pong");
}));
app.MapGet("/api/items", () => { log.Add("handler"); return "items"; });

await app.StartAsync();
var client = app.GetTestClient();
foreach (var url in new[] { "/api/items", "/ping", "/api/items?stop=1", "/nope" })
{
    log.Clear();
    var response = await client.GetAsync(url);
    Console.WriteLine($"{url} -> {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}".TrimEnd());
    Console.WriteLine("   " + string.Join(" > ", log));
}
await app.StopAsync();
```
```output
/api/items -> 200 items
   A before > B before > api-branch > handler > B after > A after
/ping -> 200 pong
   A before > B before > ping path= base=/ping > B after > A after
/api/items?stop=1 -> 200 stopped by B
   A before > B before > B stops > A after
/nope -> 404
   A before > B before > B after > A after
```

Слой-класс по соглашению: конструктор получает `next` и singleton, а `InvokeAsync` - scoped-сервис. Здесь же видно, что заголовок после `next` поставить нельзя, а `OnStarting` - можно, и что маршрутизация уже выбрала конечную точку:

```csharp run
using Microsoft.AspNetCore.TestHost;

var builder = WebApplication.CreateBuilder();
builder.WebHost.UseTestServer();
builder.Logging.ClearProviders();
builder.Services.AddSingleton<Counter>();
builder.Services.AddScoped<RequestInfo>();
builder.Services.AddSingleton<List<string>>();
var app = builder.Build();
var notes = app.Services.GetRequiredService<List<string>>();

app.UseMiddleware<StampMiddleware>();
app.MapGet("/data", (RequestInfo info) => $"handler sees {info.Id}")
   .WithMetadata("sensitive");
app.MapGet("/plain", () => "plain");

await app.StartAsync();
var client = app.GetTestClient();
foreach (var url in new[] { "/data", "/plain", "/data" })
{
    notes.Clear();
    var response = await client.GetAsync(url);
    var stamp = response.Headers.TryGetValues("X-Stamp", out var v) ? v.Single() : "-";
    Console.WriteLine($"{url} -> {await response.Content.ReadAsStringAsync()} | X-Stamp={stamp}");
    foreach (var note in notes) Console.WriteLine("   " + note);
}
await app.StopAsync();

class Counter { int n; public int Next() => Interlocked.Increment(ref n); }
class RequestInfo { public int Id { get; set; } }

class StampMiddleware(RequestDelegate next, Counter counter)   // singleton - в конструкторе
{
    public async Task InvokeAsync(HttpContext context, RequestInfo info, List<string> notes)   // scoped и прочее - здесь
    {
        info.Id = counter.Next();
        var tag = context.GetEndpoint()?.Metadata.GetMetadata<string>() ?? "none";
        notes.Add($"endpoint metadata: {tag}");
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Stamp"] = $"{info.Id}";
            return Task.CompletedTask;
        });
        await next(context);
        notes.Add($"after next: HasStarted={context.Response.HasStarted}");
        try { context.Response.Headers["X-Late"] = "1"; }
        catch (InvalidOperationException e) { notes.Add("late header: " + e.Message); }
    }
}
```
```output
/data -> handler sees 1 | X-Stamp=1
   endpoint metadata: sensitive
   after next: HasStarted=True
   late header: The response headers cannot be modified because the response has already started.
/plain -> plain | X-Stamp=2
   endpoint metadata: none
   after next: HasStarted=True
   late header: The response headers cannot be modified because the response has already started.
/data -> handler sees 3 | X-Stamp=3
   endpoint metadata: sensitive
   after next: HasStarted=True
   late header: The response headers cannot be modified because the response has already started.
```

Фильтр конечной точки - как контраст: он видит аргументы обработчика и может заменить результат, но не знает про остальные запросы:

```csharp run
using Microsoft.AspNetCore.TestHost;

var builder = WebApplication.CreateBuilder();
builder.WebHost.UseTestServer();
builder.Logging.ClearProviders();
var app = builder.Build();
var log = new List<string>();

app.Use(async (context, next) => { log.Add("middleware before"); await next(); log.Add("middleware after"); });

var api = app.MapGroup("/api").AddEndpointFilter(async (ctx, next) =>
{
    log.Add($"filter before, arg0={ctx.Arguments[0]}");
    var result = await next(ctx);
    log.Add("filter after");
    return result;
});
api.MapGet("/square/{n:int}", (int n) =>
{
    log.Add("handler");
    return n * n;
});
api.MapGet("/blocked/{n:int}", (int n) => "unreachable")
   .AddEndpointFilter((ctx, next) => ValueTask.FromResult<object?>(Results.Text("blocked by endpoint filter", statusCode: 403)));

await app.StartAsync();
var client = app.GetTestClient();
foreach (var url in new[] { "/api/square/7", "/api/blocked/1", "/other" })
{
    log.Clear();
    var response = await client.GetAsync(url);
    Console.WriteLine($"{url} -> {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}".TrimEnd());
    Console.WriteLine("   " + string.Join(" > ", log));
}
await app.StopAsync();
```
```output
/api/square/7 -> 200 49
   middleware before > filter before, arg0=7 > handler > filter after > middleware after
/api/blocked/1 -> 403 blocked by endpoint filter
   middleware before > filter before, arg0=1 > filter after > middleware after
/other -> 404
   middleware before > middleware after
```

<!-- section: mistakes -->
## Типичные ошибки

* **Не вызвать `next`, сам того не желая.** Забытый `await next()` оставляет ответ пустым (200 без тела), а всё, что ниже, не выполняется. Если слой замыкает конвейер намеренно, пишите ответ явно.
* **Вызвать `next` и после него писать в ответ.** После того как обработчик отправил тело, менять код состояния и заголовки уже нельзя (`HasStarted`): сработает исключение. Нужен заголовок - `OnStarting`; нужна проверка до обработчика - слой выше `next`.
* **Перепутать порядок.** Проверка ключа API после конечных точек не сработает: точка уже ответила. Сквозные слои (идентификатор запроса, обработка ошибок) ставят выше, зависящие от маршрута - ниже.
* **Принять scoped-сервис в конструкторе слоя.** Слой создаётся один раз, scoped-зависимость окажется «захваченной» на всё время жизни приложения (а при включённой проверке областей - исключение). Scoped-сервисы принимайте параметрами `InvokeAsync`.
* **Считать `Map` и `UseWhen` одним и тем же.** `Map` отрезает префикс и не возвращается в основной конвейер; `UseWhen` оставляет путь как есть и возвращается обратно.
* **Использовать middleware там, где нужен фильтр.** Если логика зависит от аргументов обработчика или его результата, фильтр конечной точки получает их готовыми; middleware видит только сырой запрос.

<!-- section: summary -->
## Итоги

* Конвейер - цепочка слоёв: код до `next` идёт сверху вниз, код после - снизу вверх; порядок регистрации задаёт порядок выполнения.
* `Use` вызывает `next`, `Run` завершает конвейер, `Map` уводит запрос в ветку навсегда, `UseWhen` - временно.
* Слой, не вызвавший `next`, замыкает конвейер; внешние слои всё равно выполнят свой «после».
* Слой-класс: `RequestDelegate` и singleton - в конструктор, scoped-сервисы - в `InvokeAsync`.
* После начала ответа заголовки менять нельзя - используйте `OnStarting`.
* Фильтры конечных точек оборачивают обработчик, middleware - весь запрос.

Дальше: [конфигурация и Options](lesson:csharp.aspnetcore.configuration-options).
