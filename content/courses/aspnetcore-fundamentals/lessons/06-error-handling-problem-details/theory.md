<!-- section: simple -->
## Ошибки - тоже часть контракта API

Когда что-то идёт не так, клиент должен получить **понятный, предсказуемый ответ**: подходящий код состояния и описание проблемы, а не пустое тело, страницу со стеком вызовов или паролем в тексте исключения. Для описания проблем в HTTP есть стандарт - **Problem Details** (RFC 9457): JSON с типом `application/problem+json` и полями `type`, `title`, `status`, `detail`, `instance`. Клиент может разобрать его программно, а человек - прочитать.

В ASP.NET Core ошибки бывают трёх видов:

* **ожидаемые**, которые вы возвращаете сами: `Results.NotFound()`, `Results.Problem(...)`, `Results.ValidationProblem(...)`;
* **пустые** ответы фреймворка: `404` для неизвестного пути, `405` для неверного метода;
* **исключения**, которых вы не ждали (и доменные исключения, которые вы бросаете намеренно).

Три части решения: `AddProblemDetails()` включает форматирование проблем, `UseExceptionHandler()` ловит исключения и превращает их в ответ, `UseStatusCodePages()` заполняет тело пустых ответов. Для своих исключений пишут `IExceptionHandler`: класс, который решает, какой статус и какое описание соответствуют исключению.

Главное правило: **наружу уходит то, что вы решили показать**. Сообщение `connection failed: Password=...` в теле ответа - утечка, даже если «это просто 500».

<!-- section: precise -->
## Точные правила

**Problem Details (RFC 9457).** Ответ с `Content-Type: application/problem+json`. Стандартные поля: `type` (URI, идентифицирующий вид проблемы), `title` (короткое описание вида, не меняется от случая к случаю), `status` (код HTTP), `detail` (описание конкретного случая), `instance` (URI конкретного случая). Дополнительные поля (расширения) разрешены: например, `errors` у валидации или `retryAfter`. Клиент не должен разбирать `detail` - для программной логики служат `type` и расширения.

**Что включает AddProblemDetails().** Регистрирует `IProblemDetailsService`, через который пишут проблемы middleware и результаты. После этого `UseExceptionHandler()` без аргументов возвращает 500 с телом ProblemDetails, `UseStatusCodePages()` заполняет тело пустых ответов (404, 405 и других от 400 до 599), а к ответам добавляется поле `traceId` (идентификатор запроса, он различается между запусками). Без `AddProblemDetails()` вызов `UseExceptionHandler()` без аргументов при старте завершается ошибкой конфигурации, а `UseStatusCodePages()` пишет простой текст `Status Code: 404; Not Found`. Содержимое настраивает `options.CustomizeProblemDetails`: там можно добавить или убрать поля.

**Исключения.** `UseExceptionHandler()` оборачивает остальной конвейер: исключение, не пойманное выше по стеку, превращается в ответ. Последовательность такая: вызываются зарегистрированные `IExceptionHandler` в порядке регистрации, пока один из них не вернёт `true`; если ни один не обработал, а `AddProblemDetails()` подключён, пишется общий 500. Если **ответ уже начат** (заголовки отправлены), статус изменить нельзя: клиент получит оборванный ответ. `UseExceptionHandler("/error")` вместо этого выполняет повторно указанный путь.

**IExceptionHandler.** Метод `ValueTask<bool> TryHandleAsync(HttpContext, Exception, CancellationToken)`. Вернуть `true` - «я ответил»; `false` - «не моё, пусть решает следующий». Типичный обработчик проверяет тип исключения, задаёт `Response.StatusCode` и пишет проблему через `IProblemDetailsService.TryWriteAsync`. Регистрация - `services.AddExceptionHandler<T>()`.

**Ожидаемые ошибки.** `Results.Problem(detail, instance, statusCode, title, type, extensions)` и `TypedResults.Problem` создают ответ ProblemDetails напрямую. `Results.ValidationProblem(errors)` даёт 400 с полем `errors` (словарь «поле - сообщения»); тип по умолчанию - ссылка на раздел 15.5.1 RFC 9110, заголовок «One or more validation errors occurred.».

**Среды.** В среде Development `WebApplication` сам подключает **страницу исключений разработчика**: стек, заголовки, значения запроса. Для разработки это удобно, для боевой среды недопустимо, поэтому в Production исключения обрабатывает `UseExceptionHandler`, который не раскрывает сообщение исключения.

<!-- section: examples -->
## Примеры

Что видит клиент в трёх конфигурациях. Исключение в обработчике, пустые 404/405 и ожидаемые проблемы (`traceId` убран из вывода - он меняется между запусками):

```csharp run
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.TestHost;

async Task Run(string title, Action<WebApplicationBuilder> configure, Action<WebApplication> pipeline)
{
    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseTestServer();
    builder.Logging.ClearProviders();
    configure(builder);
    await using var app = builder.Build();
    pipeline(app);

    app.MapGet("/ok", () => "ok");
    app.MapGet("/boom", string () => throw new InvalidOperationException("secret"));
    app.MapGet("/teapot", () => Results.Problem("Out of tea", statusCode: 418, title: "Teapot", instance: "/teapot",
        extensions: new Dictionary<string, object?> { ["retryAfter"] = 30 }));
    app.MapGet("/invalid", () => Results.ValidationProblem(new Dictionary<string, string[]> { ["q"] = ["q is required"] }));
    await app.StartAsync();
    var client = app.GetTestClient();

    Console.WriteLine(title);
    foreach (var (method, url) in new[] { ("GET", "/boom"), ("GET", "/missing"), ("POST", "/ok"), ("GET", "/teapot"), ("GET", "/invalid") })
    {
        try
        {
            var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));
            var type = response.Content.Headers.ContentType?.MediaType ?? "-";
            var body = (await response.Content.ReadAsStringAsync()).Trim();
            if (type.EndsWith("json") && JsonNode.Parse(body) is JsonObject json)
            {
                json.Remove("traceId");              // меняется от запуска к запуску
                body = json.ToJsonString();
            }
            Console.WriteLine($"  {method,-4} {url,-9} {(int)response.StatusCode} {type} {body}".TrimEnd());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  {method,-4} {url,-9} exception reached the client: {ex.GetType().Name}");
        }
    }
}

await Run("1. Ничего не подключено", _ => { }, _ => { });
await Run("2. AddProblemDetails + UseExceptionHandler", b => b.Services.AddProblemDetails(), app => app.UseExceptionHandler());
await Run("3. + UseStatusCodePages", b => b.Services.AddProblemDetails(), app =>
{
    app.UseExceptionHandler();
    app.UseStatusCodePages();
});
```
```output
1. Ничего не подключено
  GET  /boom     exception reached the client: InvalidOperationException
  GET  /missing  404 -
  POST /ok       405 -
  GET  /teapot   418 application/problem+json {"title":"Teapot","status":418,"detail":"Out of tea","instance":"/teapot","retryAfter":30}
  GET  /invalid  400 application/problem+json {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"q":["q is required"]}}
2. AddProblemDetails + UseExceptionHandler
  GET  /boom     500 application/problem+json {"type":"https://tools.ietf.org/html/rfc9110#section-15.6.1","title":"An error occurred while processing your request.","status":500}
  GET  /missing  404 -
  POST /ok       405 -
  GET  /teapot   418 application/problem+json {"title":"Teapot","status":418,"detail":"Out of tea","instance":"/teapot","retryAfter":30}
  GET  /invalid  400 application/problem+json {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"q":["q is required"]}}
3. + UseStatusCodePages
  GET  /boom     500 application/problem+json {"type":"https://tools.ietf.org/html/rfc9110#section-15.6.1","title":"An error occurred while processing your request.","status":500}
  GET  /missing  404 application/problem+json {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Not Found","status":404}
  POST /ok       405 application/problem+json {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.6","title":"Method Not Allowed","status":405}
  GET  /teapot   418 application/problem+json {"title":"Teapot","status":418,"detail":"Out of tea","instance":"/teapot","retryAfter":30}
  GET  /invalid  400 application/problem+json {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"q":["q is required"]}}
```

Без обработчика `TestServer` пробрасывает исключение прямо в клиент теста. `AddProblemDetails` вместе с `UseExceptionHandler` превращают его в 500 без деталей, а пустые 404 и 405 получают тело только после `UseStatusCodePages`. `Results.Problem` и `ValidationProblem` работают в любой конфигурации.

Собственные обработчики исключений. Порядок регистрации имеет значение; неизвестное исключение получает общий 500 без сообщения; если ответ уже начат, исправить его нельзя:

```csharp run
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;

var builder = WebApplication.CreateBuilder();
builder.WebHost.UseTestServer();
builder.Logging.ClearProviders();

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Extensions.Remove("traceId");       // чтобы вывод был детерминированным
    context.ProblemDetails.Extensions["service"] = "orders-api";
});
// Обработчики вызываются в порядке регистрации, пока один не вернёт true
builder.Services.AddExceptionHandler<NotFoundHandler>();
builder.Services.AddExceptionHandler<TimeoutHandler>();

var app = builder.Build();
app.UseExceptionHandler();
app.MapGet("/missing", string () => throw new KeyNotFoundException("order 5"));
app.MapGet("/slow", string () => throw new TimeoutException("db took 31 s"));
app.MapGet("/crash", string () => throw new InvalidOperationException("password=hunter2"));
app.MapGet("/half", async (HttpContext context) =>
{
    await context.Response.WriteAsync("part one, ");
    await context.Response.Body.FlushAsync();      // заголовки уже отправлены
    throw new InvalidOperationException("late failure");
});

await app.StartAsync();
var client = app.GetTestClient();
foreach (var url in new[] { "/missing", "/slow", "/crash", "/half" })
{
    var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
    string body;
    try { body = await response.Content.ReadAsStringAsync(); }
    catch (HttpRequestException) { body = "<response aborted after headers were sent>"; }
    Console.WriteLine($"{url} -> {(int)response.StatusCode} {response.Content.Headers.ContentType?.MediaType ?? "-"} {body}");
}
await app.StopAsync();

class NotFoundHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not KeyNotFoundException) return false;
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = 404, Title = "Not Found", Detail = exception.Message },
        });
    }
}

class TimeoutHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not TimeoutException) return false;
        context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = 504, Title = "Upstream timeout" },   // Detail намеренно не заполняем
        });
    }
}
```
```output
/missing -> 404 application/problem+json {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Not Found","status":404,"detail":"order 5","service":"orders-api"}
/slow -> 504 application/problem+json {"type":"https://tools.ietf.org/html/rfc9110#section-15.6.5","title":"Upstream timeout","status":504,"service":"orders-api"}
/crash -> 500 application/problem+json {"type":"https://tools.ietf.org/html/rfc9110#section-15.6.1","title":"An error occurred while processing your request.","status":500,"service":"orders-api"}
/half -> 200 - <response aborted after headers were sent>
```

`KeyNotFoundException` обработал первый обработчик, `TimeoutException` - второй (первый вернул `false`), `InvalidOperationException` никто не взял - клиент получил общий 500, пароль из сообщения не показан. Поле `service` добавлено единым местом, `CustomizeProblemDetails`. Для `/half` статус уже ушёл клиенту как 200.

Страница разработчика против Production:

```csharp run
using Microsoft.AspNetCore.TestHost;

async Task Run(string environment, Action<WebApplicationBuilder> configure, Action<WebApplication> pipeline)
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
    builder.WebHost.UseTestServer();
    builder.Logging.ClearProviders();
    configure(builder);
    await using var app = builder.Build();
    pipeline(app);
    app.MapGet("/boom", string () => throw new InvalidOperationException("Password=hunter2"));
    await app.StartAsync();

    var request = new HttpRequestMessage(HttpMethod.Get, "/boom");
    if (environment == "Development") request.Headers.Add("Accept", "text/html");   // так запрашивает браузер
    var response = await app.GetTestClient().SendAsync(request);
    var body = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"{environment,-11} {(int)response.StatusCode} {response.Content.Headers.ContentType?.MediaType} " +
        $"leaks message: {body.Contains("hunter2")}, shows exception type: {body.Contains("InvalidOperationException")}");
}

// Без UseExceptionHandler в Development страница разработчика включена автоматически
await Run("Development", _ => { }, _ => { });
await Run("Production", b => b.Services.AddProblemDetails(), app => app.UseExceptionHandler());
```
```output
Development 500 text/html leaks message: True, shows exception type: True
Production  500 application/problem+json leaks message: False, shows exception type: False
```


<!-- section: mistakes -->
## Типичные ошибки

* **Показывать `ex.Message` и `ex.ToString()` клиенту.** Сообщения исключений содержат строки подключения, пути, SQL. В `detail` помещайте только то, что написано для клиента, а подробности пишите в журнал.
* **Оставить страницу разработчика в боевой среде.** Она раскрывает стек и данные запроса; в Production нужен `UseExceptionHandler`.
* **Вызвать `UseExceptionHandler()` без `AddProblemDetails()`** (и без обработчиков): приложение не запустится.
* **Ловить исключения в каждом обработчике маршрута.** `try/catch` повторяется и расходится; доменные исключения проще превращать в ответы в одном `IExceptionHandler`.
* **Возвращать 200 с телом `{"error": ...}`.** Код состояния - часть контракта: клиенты и прокси смотрят на него первым.
* **Опираться на `detail`/`title` в клиентском коде.** Они для людей; программная логика строится на `status`, `type` и документированных расширениях.
* **Забыть, что обработчик не спасёт начатый ответ.** Если тело уже пошло клиенту, ни статус, ни JSON-ошибку не вернуть: проверяйте всё до первой записи.
* **Ожидать `UseStatusCodePages` от ответа, у которого есть тело.** Он заполняет только пустые ответы 400-599 без `Content-Type`.

<!-- section: summary -->
## Итоги

* Ошибки API описывают Problem Details (RFC 9457): `type`, `title`, `status`, `detail`, `instance` и расширения, тип `application/problem+json`.
* `AddProblemDetails()` + `UseExceptionHandler()` превращают исключения в 500, `UseStatusCodePages()` - пустые 404/405 в ProblemDetails.
* `IExceptionHandler` сопоставляет исключения статусам и телам; обработчики вызываются по порядку, `false` передаёт дальше.
* `Results.Problem`, `TypedResults.Problem`, `Results.ValidationProblem` возвращают ожидаемые ошибки явно.
* Не показывайте сообщения исключений и стек клиенту; `traceId` помогает связать ответ с журналом, но он недетерминирован.
* Начатый ответ изменить нельзя.

Дальше: [тестирование CRUD-сервиса](lesson:csharp.aspnetcore.testing-crud-service). Параллель из другого мира: [обработка ошибок в Express](lesson:express.fundamentals.error-handling).
