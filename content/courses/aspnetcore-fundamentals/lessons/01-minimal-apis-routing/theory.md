<!-- section: simple -->
## Веб-приложение как набор маршрутов

**ASP.NET Core** - фреймворк для веб-приложений на C#. Самый короткий способ написать HTTP API - **minimal APIs**: вы создаёте `WebApplication`, сопоставляете пути с функциями и запускаете приложение. Функция-обработчик (лямбда или метод) принимает то, что ей нужно, а фреймворк сам достаёт значения из адреса, строки запроса, заголовков и тела и превращает возвращённое значение в ответ.

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/hello/{name}", (string name) => $"Hello, {name}!");

app.Run();
```

Три шага стоит запомнить: **builder** собирает настройки и сервисы, `Build()` превращает их в приложение, `Map...` добавляет **конечные точки** (endpoints). Метод запроса и шаблон пути вместе выбирают одну конечную точку; если ни одна не подошла, ответ - 404, а если путь есть, но метод другой, - 405.

В этом курсе приложение никогда не слушает порт: тесты подключают к нему **TestServer** и обращаются через `HttpClient`, который работает в памяти. Код маршрутов от этого не меняется, а проверка идёт быстро и без сети.

Что обработчик возвращает, то и становится ответом: строка - `text/plain`, объект - JSON, `int` или `bool` - JSON-литерал, а `Results.NotFound()`, `Results.Created(...)` и другие помощники явно задают код состояния.

<!-- section: precise -->
## Точные правила

**Конечные точки и маршрутизация.** `MapGet`, `MapPost`, `MapPut`, `MapDelete` и `MapMethods` регистрируют обработчик для метода HTTP и шаблона маршрута. Маршрутизация сопоставляет запрос с одной конечной точкой; при нескольких подходящих побеждает более конкретный шаблон (литерал сильнее параметра, параметр с ограничением сильнее параметра без него), а неоднозначность даёт ошибку на запросе.

**Шаблоны.** Фигурные скобки задают параметр: `/books/{id}`. Ограничение пишется после двоеточия: `{id:int}`, `{id:guid}`, `{n:min(1)}`. Запрос, не прошедший ограничение, **не совпадает с маршрутом** и получает 404, а не 400. Параметр `{**path}` захватывает остаток пути вместе с косыми чертами. Имена в шаблоне сравниваются с именами параметров обработчика без учёта регистра.

**Привязка параметров.** Источник значения определяется автоматически:

* имя есть в шаблоне маршрута - значение берётся из пути;
* простой тип (`int`, `string`, `bool`, `Guid`, `DateOnly` ...), которого нет в маршруте, - из строки запроса;
* сложный тип в `POST`/`PUT` - из тела запроса (JSON);
* сервисы из контейнера (`ILogger<T>`, `HttpContext`, ваши интерфейсы) подставляются сами.

Явно источник задают атрибуты `[FromQuery]`, `[FromHeader]`, `[FromRoute]`, `[FromBody]`, `[FromServices]`. Параметр без значения по умолчанию, не допускающий `null`, - **обязательный**: если значения нет или его нельзя преобразовать, обработчик не вызывается, а ответ - 400. Необязательные параметры делают `int?` или `string? name = null`.

**Результаты.** Обработчик возвращает: `string` (`text/plain`), POCO/record/коллекцию (JSON; имена свойств - camelCase), `IResult`. Статические классы `Results` и `TypedResults` создают `IResult`: `Ok`, `Created(uri, value)`, `NotFound`, `BadRequest`, `NoContent`, `Redirect`, `Problem`. `TypedResults` возвращают конкретные типы - это видно компилятору и тестам.

**Группы маршрутов.** `app.MapGroup("/api/v1")` задаёт общий префикс (и общие настройки: фильтры, политики) для набора конечных точек, чтобы не повторять префикс.

<!-- section: examples -->
## Примеры

Шаблоны, ограничения и разные ответы 404/405. Каждый запрос идёт в приложение через `TestServer`:

```csharp run
using Microsoft.AspNetCore.TestHost;

var builder = WebApplication.CreateBuilder();
builder.WebHost.UseTestServer();
builder.Logging.ClearProviders();
var app = builder.Build();

app.MapGet("/", () => "root");
app.MapGet("/hello/{name}", (string name) => $"Hello, {name}!");
app.MapGet("/orders/{id:int}", (int id) => $"order {id}");
app.MapGet("/files/{**path}", (string path) => $"file {path}");
app.MapPost("/orders", () => Results.Created("/orders/7", new { id = 7 }));

await app.StartAsync();
var client = app.GetTestClient();

foreach (var (method, url) in new[]
{
    ("GET", "/"), ("GET", "/hello/Анна"), ("GET", "/orders/42"), ("GET", "/orders/abc"),
    ("GET", "/files/a/b/c.txt"), ("DELETE", "/orders/42"), ("POST", "/orders"), ("GET", "/nope"),
})
{
    var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));
    var type = response.Content.Headers.ContentType?.MediaType ?? "-";
    Console.WriteLine($"{method,-6} {url,-18} -> {(int)response.StatusCode} {type} {await response.Content.ReadAsStringAsync()}".TrimEnd());
}
await app.StopAsync();
```
```output
GET    /                  -> 200 text/plain root
GET    /hello/Анна        -> 200 text/plain Hello, Анна!
GET    /orders/42         -> 200 text/plain order 42
GET    /orders/abc        -> 404 -
GET    /files/a/b/c.txt   -> 200 text/plain file a/b/c.txt
DELETE /orders/42         -> 405 -
POST   /orders            -> 201 application/json {"id":7}
GET    /nope              -> 404 -
```

Обратите внимание: `/orders/abc` не прошёл ограничение `:int` и получил 404, а `DELETE /orders/42` попал на путь, у которого нет такого метода, - 405.

Источники параметров, обязательность и результаты. Группа даёт общий префикс; обязательные параметры без значения дают 400:

```csharp run
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;

var builder = WebApplication.CreateBuilder();
builder.WebHost.UseTestServer();
builder.Logging.ClearProviders();
var app = builder.Build();

var api = app.MapGroup("/api/v1");
api.MapGet("/sum", (int a, int b) => a + b);
api.MapGet("/users/{id:int}", (int id, [FromQuery] bool verbose, [FromHeader(Name = "X-Tenant")] string? tenant) =>
    TypedResults.Ok(new { id, verbose, tenant }));
api.MapPost("/echo", (Note note) => TypedResults.Created($"/api/v1/notes/{note.Id}", note));
api.MapGet("/legacy", () => Results.Redirect("/api/v1/sum?a=1&b=2"));

await app.StartAsync();
var client = app.GetTestClient();

async Task Show(HttpMethod method, string url, Action<HttpRequestMessage>? configure = null)
{
    var request = new HttpRequestMessage(method, url);
    configure?.Invoke(request);
    var response = await client.SendAsync(request);
    var type = response.Content.Headers.ContentType?.MediaType ?? "-";
    var location = response.Headers.Location is { } l ? $" Location={l}" : "";
    Console.WriteLine($"{method} {url} -> {(int)response.StatusCode} {type}{location} {await response.Content.ReadAsStringAsync()}".TrimEnd());
}

await Show(HttpMethod.Get, "/api/v1/sum?a=2&b=3");
await Show(HttpMethod.Get, "/api/v1/sum?a=2");
await Show(HttpMethod.Get, "/api/v1/sum?a=2&b=x");
await Show(HttpMethod.Get, "/api/v1/users/5?verbose=true", r => r.Headers.Add("X-Tenant", "acme"));
await Show(HttpMethod.Get, "/api/v1/users/5");
await Show(HttpMethod.Post, "/api/v1/echo", r => r.Content = JsonContent.Create(new Note(9, "hi")));
await Show(HttpMethod.Get, "/api/v1/legacy");
await app.StopAsync();

record Note(int Id, string Text);
```
```output
GET /api/v1/sum?a=2&b=3 -> 200 application/json 5
GET /api/v1/sum?a=2 -> 400 -
GET /api/v1/sum?a=2&b=x -> 400 -
GET /api/v1/users/5?verbose=true -> 200 application/json {"id":5,"verbose":true,"tenant":"acme"}
GET /api/v1/users/5 -> 400 -
POST /api/v1/echo -> 201 application/json Location=/api/v1/notes/9 {"id":9,"text":"hi"}
GET /api/v1/legacy -> 302 - Location=/api/v1/sum?a=1&b=2
```

<!-- section: mistakes -->
## Типичные ошибки

* **Ждать 400 от ограничения маршрута.** `{id:int}` и запрос `/books/abc` дают 404: маршрут просто не совпал.
* **Считать параметр необязательным по умолчанию.** `bool verbose` без значения в запросе - это 400; для необязательного используйте `bool? verbose` или значение по умолчанию.
* **Возвращать «пустой» объект вместо 404.** `FirstOrDefault` вернул `null` - значит, нужен `Results.NotFound()`, иначе клиент получит 204 или `null` в теле.
* **Забывать про 201 и `Location` при создании.** `Results.Created($"/books/{id}", book)` выставляет и код, и адрес нового ресурса.
* **Смешивать состояние приложения и запроса.** Коллекция, созданная в `Map`, живёт всё время работы приложения и доступна одновременно нескольким запросам; для настоящих данных нужны сервисы (следующие уроки).
* **Путать порядок маршрутов с приоритетом.** Побеждает не первый зарегистрированный, а более конкретный шаблон; равные по конкретности шаблоны - ошибка неоднозначности.

<!-- section: summary -->
## Итоги

* `WebApplication.CreateBuilder` -> `Build` -> `Map...` -> `Run`: приложение - набор конечных точек.
* Метод и шаблон пути выбирают обработчик; нет пути - 404, нет метода - 405; провал ограничения - тоже 404.
* Значения параметров берутся из маршрута, строки запроса, заголовков, тела и контейнера сервисов; обязательные без значения дают 400.
* Возвращайте значения для простых ответов и `Results`/`TypedResults` для явных кодов (`Created`, `NotFound`, `NoContent`).
* `MapGroup` задаёт общий префикс; `TestServer` позволяет проверять всё это без открытия порта.

Дальше: [привязка модели и валидация](lesson:csharp.aspnetcore.model-binding-validation).
