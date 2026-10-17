<!-- section: simple -->
## От HTTP-запроса к аргументам обработчика

Запрос приходит текстом: путь, строка запроса, заголовки, тело. Обработчик же хочет типизированные аргументы - `int`, `DateOnly`, `NewUser`. Превращение первого во второе называется **привязкой модели** (model binding). Его делает фреймворк: вы лишь объявляете параметры.

```csharp
app.MapPost("/users", (NewUser user, string? invite) => ...);
//                     ^ из JSON-тела     ^ из строки запроса, может отсутствовать
```

Если значение нельзя получить или преобразовать, обработчик **не вызывается**, клиент получает **400**. Привязка отвечает на вопрос «можно ли прочитать запрос как типы», но не на вопрос «осмысленны ли данные»: возраст `-5` или email `abc` привяжутся без ошибок. Для этого есть **валидация**: правила описывают атрибутами (`[Required]`, `[Range(18, 120)]`, `[EmailAddress]`) или кодом, а ошибки собирают **по полям** и возвращают клиенту единым ответом 400 в формате `ValidationProblem`.

Итого цепочка такова: запрос -> привязка (400, если не прочиталось) -> валидация (400 со списком ошибок по полям) -> ваш код с гарантией, что данные корректны.

<!-- section: precise -->
## Точные правила

**Источники и атрибуты.** Источник определяется автоматически, но его можно указать явно: `[FromRoute]`, `[FromQuery]`, `[FromHeader(Name = "X-Tenant")]`, `[FromBody]`, `[FromForm]`, `[FromServices]`. Простые типы вне маршрута - из строки запроса; сложный тип в `POST`/`PUT` - из тела; в `GET` сложный тип без явного источника **не** выводится из запроса, и приложение падает при первом обращении с сообщением «Body was inferred but the method does not allow inferred body parameters».

**Тело (JSON).** Десериализуется `System.Text.Json` с настройками веба: имена свойств регистронезависимы, лишние поля игнорируются, перечисления по умолчанию - числами (строками - с `JsonStringEnumConverter`). Неверный JSON или пустое тело для параметра, не допускающего `null`, - 400; другой `Content-Type` (не `application/json`) - 415. Параметр `[FromBody] Order? order` необязателен: без тела он будет `null`.

**Необязательные параметры.** Параметр обязателен, если тип не допускает `null` и значение по умолчанию не задано. `int? n`, `string? s` или `int page = 1` можно опустить. Значение, которое присутствует, но не разбирается (`n=x`), - всегда 400, даже у необязательного параметра.

**Типы, которые привязываются из строки.** Числа, `bool`, `Guid`, `DateOnly`, `DateTime`, `TimeSpan` и **перечисления**; все разбираются по правилам инвариантной культуры. Перечисление в строке запроса ищется по имени с учётом регистра (или по числу); `level=high` для `Level.High` - 400.

**Свой тип в параметре запроса.** Достаточно статического метода `TryParse(string? value, out T result)` (или с `IFormatProvider`): вернул `false` - клиент получает 400. Для источников, которые нельзя выразить строкой (несколько заголовков, смесь запроса и заголовков), служит `static ValueTask<T?> BindAsync(HttpContext context)`; её результат подставляется как есть. Для обязательного параметра `null` из `BindAsync` даёт 400.

**Группа параметров.** `[AsParameters]` разворачивает объект с конструктором в набор отдельных параметров: каждый член привязывается как самостоятельный параметр со своим источником и значением по умолчанию. Так запрос с десятью фильтрами не превращается в лямбду с десятью аргументами.

**Валидация DataAnnotations.** `builder.Services.AddValidation()` (пространство имён `Microsoft.Extensions.DependencyInjection`, входит в общую платформу ASP.NET Core 10, отдельный пакет не нужен) включает встроенную проверку: фильтр выполняется после привязки и проверяет параметры обработчика, их сложные свойства, вложенные объекты и элементы коллекций по атрибутам `System.ComponentModel.DataAnnotations`. Ошибки группируются по пути поля (`Lines[0].Sku`, `Address.City`), ответ - `400` с `ValidationProblem`. Атрибуты на параметрах и членах `[AsParameters]`-типа проверяются так же. **Проверяются только публичные (`public`) типы**: для `internal` и вложенных непубличных типов проверка тихо ничего не делает.

**Ручная проверка.** Кроме атрибутов, правила часто зависят от состояния (уникальность, согласованность двух полей). Их проверяют в обработчике и возвращают `Results.ValidationProblem(errors)` или `TypedResults.ValidationProblem(errors)`: `errors` - словарь «поле -> массив сообщений». Ответ - `400 application/problem+json` со `status`, `title`, `type` и `errors`. Атрибуты можно вызвать и вручную: `Validator.TryValidateObject(obj, new ValidationContext(obj), results, validateAllProperties: true)`; он смотрит на **свойства**, поэтому в позиционной записи атрибуты нужно писать как `[property: Required]`, иначе они останутся на параметре конструктора и ручная проверка их не увидит.

<!-- section: examples -->
## Примеры

Источники значений и что происходит при ошибке привязки. Здесь есть и свой `TryParse`, и `BindAsync`, и `[AsParameters]`:

```csharp run
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;

var builder = WebApplication.CreateBuilder();
builder.WebHost.UseTestServer();
builder.Logging.ClearProviders();
var app = builder.Build();

app.MapGet("/items/{id:guid}", (Guid id, Level level, DateOnly day, int? count, string note = "-") =>
    $"{id} {level} {day:yyyy-MM-dd} count={count} note={note}");
app.MapGet("/distance", (Point from, Point to) => Math.Round(Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2)), 2));
app.MapGet("/search", ([AsParameters] Search search) => $"q={search.Q} page={search.Page} tenant={search.Tenant}");
app.MapGet("/client", (ClientInfo info) => info.ToString());
app.MapPost("/notes", (Note note) => TypedResults.Ok(note));
app.MapPost("/notes/optional", ([FromBody] Note? note) => note is null ? "no body" : note.Text);

await app.StartAsync();
var client = app.GetTestClient();

async Task Show(string method, string url, string? body = null, string? contentType = "application/json", params (string, string)[] headers)
{
    var request = new HttpRequestMessage(new HttpMethod(method), url);
    if (body is not null) request.Content = new StringContent(body, System.Text.Encoding.UTF8, contentType!);
    foreach (var (name, value) in headers) request.Headers.Add(name, value);
    var response = await client.SendAsync(request);
    Console.WriteLine($"{method} {url} -> {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}".TrimEnd());
}

const string id = "00000000-0000-0000-0000-000000000001";
await Show("GET", $"/items/{id}?level=High&day=2025-03-04");
await Show("GET", $"/items/{id}?level=1&day=2025-03-04&count=5&note=hi");
await Show("GET", $"/items/{id}?level=high&day=2025-03-04");
await Show("GET", $"/items/{id}?level=High&day=2025-03-04&count=x");
await Show("GET", "/items/not-a-guid?level=High&day=2025-03-04");
await Show("GET", "/distance?from=0,0&to=3,4");
await Show("GET", "/distance?from=0,0&to=3");
await Show("GET", "/search?q=cats&page=2", headers: ("X-Tenant", "acme"));
await Show("GET", "/search?q=cats");
await Show("GET", "/client", headers: [("User-Agent", "demo/1.0"), ("X-Retries", "3")]);
await Show("GET", "/client", headers: ("X-Retries", "many"));
await Show("POST", "/notes", """{"TEXT":"hello","ignored":true}""");
await Show("POST", "/notes", "{broken");
await Show("POST", "/notes", """{"text":"x"}""", "text/plain");
await Show("POST", "/notes");
await Show("POST", "/notes/optional");
await app.StopAsync();

enum Level { Low, High }

record Note(string Text);

readonly record struct Point(int X, int Y)
{
    public static bool TryParse(string? value, out Point point)
    {
        point = default;
        var parts = value?.Split(',');
        if (parts is { Length: 2 } && int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var y))
        {
            point = new Point(x, y);
            return true;
        }
        return false;
    }
}

record Search(string Q, [FromHeader(Name = "X-Tenant")] string? Tenant, int Page = 1);

record ClientInfo(string Agent, int Retries)
{
    public static ValueTask<ClientInfo?> BindAsync(HttpContext context)
    {
        var agent = context.Request.Headers.UserAgent.ToString();
        return int.TryParse(context.Request.Headers["X-Retries"], out var retries)
            ? ValueTask.FromResult<ClientInfo?>(new ClientInfo(agent, retries))
            : ValueTask.FromResult<ClientInfo?>(null);
    }
}
```
```output
GET /items/00000000-0000-0000-0000-000000000001?level=High&day=2025-03-04 -> 200 00000000-0000-0000-0000-000000000001 High 2025-03-04 count= note=-
GET /items/00000000-0000-0000-0000-000000000001?level=1&day=2025-03-04&count=5&note=hi -> 200 00000000-0000-0000-0000-000000000001 High 2025-03-04 count=5 note=hi
GET /items/00000000-0000-0000-0000-000000000001?level=high&day=2025-03-04 -> 400
GET /items/00000000-0000-0000-0000-000000000001?level=High&day=2025-03-04&count=x -> 400
GET /items/not-a-guid?level=High&day=2025-03-04 -> 404
GET /distance?from=0,0&to=3,4 -> 200 5
GET /distance?from=0,0&to=3 -> 400
GET /search?q=cats&page=2 -> 200 q=cats page=2 tenant=acme
GET /search?q=cats -> 200 q=cats page=1 tenant=
GET /client -> 200 ClientInfo { Agent = demo/1.0, Retries = 3 }
GET /client -> 400
POST /notes -> 200 {"text":"hello"}
POST /notes -> 400
POST /notes -> 415
POST /notes -> 400
POST /notes/optional -> 200 no body
```

Валидация DataAnnotations встроенным механизмом. Ошибки группируются по полям, вложенные объекты и элементы коллекций получают путь. В конце - тот же тип, объявленный как `internal`: его атрибуты не проверяются:

```csharp run
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.TestHost;

var builder = WebApplication.CreateBuilder();
builder.WebHost.UseTestServer();
builder.Logging.ClearProviders();
builder.Services.AddValidation();
var app = builder.Build();

app.MapPost("/orders", (Order order) => TypedResults.Created($"/orders/1", order));
app.MapPost("/hidden", (HiddenOrder order) => "accepted");
app.MapGet("/page", ([Range(1, 100)] int size, [StringLength(5, MinimumLength = 2)] string? tag) => $"{size} {tag}");

await app.StartAsync();
var client = app.GetTestClient();

async Task Post(string url, string json)
{
    var response = await client.PostAsync(url, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
    Console.WriteLine($"POST {url} -> {(int)response.StatusCode} {response.Content.Headers.ContentType?.MediaType} {await response.Content.ReadAsStringAsync()}");
}

await Post("/orders", """{"lines":[{"sku":"A","qty":2}],"address":{"city":"Москва"},"email":"a@b.co"}""");
await Post("/orders", """{"lines":[{"sku":"","qty":0},{"sku":"B","qty":500}],"address":{"city":""},"email":"nope"}""");
await Post("/orders", """{"lines":[],"address":null,"email":"a@b.co"}""");
await Post("/hidden", """{"quantity":-5}""");
Console.WriteLine(await (await client.GetAsync("/page?size=0&tag=x")).Content.ReadAsStringAsync());
Console.WriteLine(await (await client.GetAsync("/page?size=10")).Content.ReadAsStringAsync());
await app.StopAsync();

public record Line([Required] string Sku, [Range(1, 100)] int Qty);

public class Address
{
    [Required] public string City { get; set; } = "";
}

public record Order([MinLength(1)] List<Line> Lines, [Required] Address Address, [EmailAddress] string Email);

record HiddenOrder([Range(1, 10)] int Quantity);
```
```output
POST /orders -> 201 application/json {"lines":[{"sku":"A","qty":2}],"address":{"city":"Москва"},"email":"a@b.co"}
POST /orders -> 400 application/json {"title":"One or more validation errors occurred.","errors":{"Lines[0].Sku":["The Sku field is required."],"Lines[0].Qty":["The field Qty must be between 1 and 100."],"Lines[1].Qty":["The field Qty must be between 1 and 100."],"Address.City":["The City field is required."],"Email":["The Email field is not a valid e-mail address."]}}
POST /orders -> 400 application/json {"title":"One or more validation errors occurred.","errors":{"Lines":["The field Lines must be a string or array type with a minimum length of '1'."],"Address":["The Address field is required."]}}
POST /hidden -> 200 text/plain accepted
{"title":"One or more validation errors occurred.","errors":{"size":["The field size must be between 1 and 100."],"tag":["The field tag must be a string with a minimum length of 2 and a maximum length of 5."]}}
10
```

Ручная проверка: правила, которые зависят от состояния, и `ValidationProblem`. Сравните с форматом встроенной валидации выше:

```csharp run
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.TestHost;

var builder = WebApplication.CreateBuilder();
builder.WebHost.UseTestServer();
builder.Logging.ClearProviders();
var app = builder.Build();
var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "admin", "root" };

// Без AddValidation: проверяем сами, в том числе атрибуты через Validator.
app.MapPost("/signup", (Signup input) =>
{
    var errors = new Dictionary<string, string[]>();
    var results = new List<ValidationResult>();
    if (!Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true))
    {
        foreach (var group in results.GroupBy(r => r.MemberNames.FirstOrDefault() ?? ""))
            errors[group.Key] = group.Select(r => r.ErrorMessage ?? "").ToArray();
    }
    if (taken.Contains(input.Login ?? ""))
        errors["Login"] = ["Логин уже занят."];
    if (input.Password is { Length: > 0 } && input.Password == input.Login)
        errors["Password"] = ["Пароль не должен совпадать с логином."];

    return errors.Count > 0 ? Results.ValidationProblem(errors) : Results.Ok(new { input.Login });
});

app.MapPost("/typed", (Signup input) =>
    string.IsNullOrWhiteSpace(input.Login)
        ? TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Login"] = ["Нужен логин."] })
        : Results.Ok(input.Login));

await app.StartAsync();
var client = app.GetTestClient();

async Task Post(string url, string json)
{
    var response = await client.PostAsync(url, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
    Console.WriteLine($"POST {url} -> {(int)response.StatusCode} {response.Content.Headers.ContentType?.MediaType} {await response.Content.ReadAsStringAsync()}");
}

await Post("/signup", """{"login":"anna","password":"s3cret-pass"}""");
await Post("/signup", """{"login":"Admin","password":"Admin"}""");
await Post("/signup", """{"login":"bob","password":"x"}""");
await Post("/typed", """{"password":"whatever"}""");
await app.StopAsync();

// Для Validator атрибуты записи нужно вешать на свойства: [property: ...].
record Signup([property: Required] string? Login, [property: StringLength(30, MinimumLength = 6)] string? Password);
```
```output
POST /signup -> 200 application/json {"login":"anna"}
POST /signup -> 400 application/problem+json {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"Password":["Пароль не должен совпадать с логином."],"Login":["Логин уже занят."]}}
POST /signup -> 400 application/problem+json {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"Password":["The field Password must be a string with a minimum length of 6 and a maximum length of 30."]}}
POST /typed -> 400 application/problem+json {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"Login":["Нужен логин."]}}
```

<!-- section: mistakes -->
## Типичные ошибки

* **Считать привязку валидацией.** Привязка проверяет только, что значение разобралось в тип. `age=-5` или `email=abc` привяжутся; смысл проверяют атрибуты и код.
* **Объявить тело или `[AsParameters]`-тип `internal` и ждать проверки.** Встроенная валидация видит только публичные типы; атрибуты на `internal record` не сработают, и некорректные данные дойдут до обработчика.
* **Ручной `Validator` и позиционные записи.** `[Required] string Login` в первичном конструкторе записи вешает атрибут на параметр, а `Validator.TryValidateObject` читает свойства: проверка молча проходит. Пишите `[property: Required]`.
* **Забыть `AddValidation()`.** Без него атрибуты `[Required]`, `[Range]` на параметрах и свойствах тела - просто метаданные. Обработчик получит `""`, `0` и `200` вместо `400`.
* **Описать запрос сложным типом в `GET`.** `(Filter filter) => ...` без `[AsParameters]` не выводится из строки запроса, а приложение падает при первом обращении. Добавьте `[AsParameters]`.
* **Рассчитывать на нестрогий разбор перечислений.** В строке запроса `level=high` для `Level.High` - 400, потому что имя ищется с учётом регистра; в теле JSON перечисление по умолчанию - число. Для строк в теле подключите `JsonStringEnumConverter` (например, атрибутом на самом перечислении).
* **Возвращать `400` вручную строкой.** Клиентам удобнее единый формат: `Results.ValidationProblem` даёт словарь «поле -> сообщения», который можно показать у каждого поля формы.
* **Путать «нет значения» и «не разбирается».** Опущенный необязательный параметр - `null` или значение по умолчанию; присутствующий, но некорректный (`count=x`) - всегда 400.

<!-- section: summary -->
## Итоги

* Привязка превращает части запроса в параметры: маршрут, строка запроса, заголовки, JSON-тело, сервисы; не получилось - 400 (415 для чужого `Content-Type`).
* Необязательность задают `?` или значением по умолчанию; свои типы подключают через `TryParse` и `BindAsync`, группы параметров - через `[AsParameters]`.
* `AddValidation()` включает проверку DataAnnotations с ответом 400 и ошибками по полям; типы должны быть `public`.
* Правила, зависящие от состояния, проверяйте в обработчике и возвращайте `Results.ValidationProblem` или `TypedResults.ValidationProblem` в том же формате.

Дальше: [внедрение зависимостей](lesson:csharp.aspnetcore.dependency-injection).
