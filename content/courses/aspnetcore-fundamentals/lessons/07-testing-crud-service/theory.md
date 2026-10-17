<!-- section: simple -->
## Тест, который общается с приложением по HTTP

**Интеграционный тест** запускает настоящее приложение со всем конвейером (маршрутизация, привязка, middleware, обработка ошибок, DI) и обращается к нему как клиент: отправляет запрос и проверяет ответ. Он ловит то, что модульные тесты не видят: неверный маршрут, забытую регистрацию сервиса, не тот код состояния, потерянный заголовок.

В ASP.NET Core для этого есть **TestServer**: сервер, который живёт в памяти и не открывает порт. Вы получаете `HttpClient`, который доставляет запросы прямо в приложение (`app.GetTestClient()`). Так устроены все примеры этого курса; тест получается быстрым и не зависит от сети.

Хороший тест CRUD-сервиса проверяет не только тело ответа, но и **код состояния**, **заголовки** (`Location`, `Content-Type`) и **побочные эффекты** (после `DELETE` ресурс правда исчез). Каждый тест начинает с чистого состояния: для него создаётся своё приложение.

Зависимости, которые мешают проверке (текущее время, случайные идентификаторы, база данных, внешний сервис), заменяют в контейнере сервисов на тестовые двойники: фиксированные часы, счётчик вместо `Guid`, хранилище в памяти. Замена делается **до** `Build()`.

В конце урока вы напишете сам CRUD-сервис (репозиторий в DI, список/чтение/создание/обновление/удаление, ошибки как ProblemDetails). А в задаче-вызове - условный GET с `ETag`, который проверяется теми же приёмами.

<!-- section: precise -->
## Точные правила

**TestServer и HttpClient.** Пакет `Microsoft.AspNetCore.TestHost` даёт `UseTestServer()` (подменяет настоящий сервер) и `GetTestClient()` (клиент, подключённый к приложению). Запросы проходят весь конвейер, но без сети. Исключение, не обработанное приложением, TestServer **пробрасывает в вызов клиента** (в уроке про ошибки вы видели это).

**Что добавляет WebApplicationFactory.** Пакет `Microsoft.AspNetCore.Mvc.Testing` предоставляет `WebApplicationFactory<TEntryPoint>`: она запускает **настоящее приложение из проекта** на TestServer, копирует файл зависимостей (`.deps`) в проект тестов, выставляет корень содержимого (content root) на корень проверяемого проекта и по умолчанию использует среду Development. Для минимальных API классу `Program` нужно сделать видимым (`public partial class Program { }`), а замену сервисов выполняют в `WithWebHostBuilder(b => b.ConfigureTestServices(...))`; `CreateClient()` возвращает `HttpClient`. В этом курсе приложение собирается прямо в `Program.cs` вместе с тестом, поэтому мы используем `TestServer` напрямую: ничего принципиально другого фабрика не делает, она лишь собирает приложение вашего проекта за вас.

**Замена сервисов.** Контейнер собирается один раз в `builder.Build()`; после этого регистрации менять нельзя. Поэтому замены делают до сборки: `services.RemoveAll<T>()` и затем `AddSingleton<T>(fake)`, либо просто повторная регистрация (при нескольких регистрациях одного типа запрос `T` возвращает **последнюю**). Для времени используйте абстракцию `TimeProvider` (встроенный класс; у тестов своя реализация с фиксированным `GetUtcNow()`), для идентификаторов - собственный интерфейс.

**Что проверять.**

* код состояния: `201`, `204`, `404`, `409`, `400`;
* заголовки: `Location` у `201`, `Content-Type: application/problem+json` у ошибок;
* тело: `ReadFromJsonAsync<T>()` разбирает JSON в тип; `PostAsJsonAsync` и `JsonContent.Create` отправляют объект как JSON;
* побочные эффекты: повторный `GET` после `POST`/`PUT`/`DELETE`, либо шпион (тестовый двойник), запоминающий вызовы;
* поведение при сбое зависимости: двойник, который бросает исключение, и проверка, что клиент получил 500 без внутренностей.

**Изоляция.** Тест не должен зависеть от порядка запуска и от других тестов. Состояние в памяти (хранилище-синглтон) принадлежит приложению, поэтому правило такое: **одно приложение на тест**. Общее приложение на весь класс (например, `IClassFixture` с одной фабрикой) делит состояние между тестами и приводит к зависимостям от порядка.

**CRUD-контракт.** `GET /items` - список; `GET /items/{id}` - 200 или 404; `POST /items` - 201 с `Location` и телом, 400 при неверных данных, 409 при дубликате; `PUT /items/{id}` - полная замена: 200, 404, 400, 409; `DELETE /items/{id}` - 204 без тела или 404. Ошибки - `application/problem+json`.

**Условные запросы (для задачи-вызова).** Сильный `ETag` идентифицирует конкретное представление ресурса. Клиент присылает его в `If-None-Match` при чтении (совпало - ответ `304 Not Modified` без тела) и в `If-Match` при изменении (не совпало - `412 Precondition Failed`: ресурс успели изменить).

<!-- section: examples -->
## Примеры

Полный набор тестов CRUD-сервиса: у каждого теста своё приложение, проверяются код, заголовок, тело, побочный эффект и изоляция. Последний тест намеренно ошибочен - так выглядит падение:

```csharp run
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;

// Каждый тест получает СВОЁ приложение с чистым хранилищем: тесты не зависят друг от друга и от порядка.
WebApplication CreateApp()
{
    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseTestServer();
    builder.Logging.ClearProviders();
    builder.Services.AddProblemDetails();
    builder.Services.AddSingleton<INoteRepository, InMemoryNoteRepository>();

    var app = builder.Build();
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    var notes = app.MapGroup("/notes");
    notes.MapGet("/", (INoteRepository repo) => repo.List());
    notes.MapGet("/{id:int}", (int id, INoteRepository repo) =>
        repo.Find(id) is { } note ? Results.Ok(note) : Results.Problem(statusCode: 404, title: "Not Found", detail: $"Note {id} was not found"));
    notes.MapPost("/", (NoteInput input, INoteRepository repo) =>
    {
        if (string.IsNullOrWhiteSpace(input.Title))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["title"] = ["title is required"] });
        if (repo.Exists(input.Title))
            return Results.Problem(statusCode: 409, title: "Conflict", detail: $"Note '{input.Title}' already exists");
        var note = repo.Add(input.Title);
        return Results.Created($"/notes/{note.Id}", note);
    });
    notes.MapDelete("/{id:int}", (int id, INoteRepository repo) => repo.Remove(id) ? Results.NoContent() : Results.NotFound());
    return app;
}

var results = new List<string>();
async Task Test(string name, Func<HttpClient, Task> body)
{
    await using var app = CreateApp();
    await app.StartAsync();
    try
    {
        await body(app.GetTestClient());
        results.Add($"PASS {name}");
    }
    catch (Exception ex)
    {
        results.Add($"FAIL {name}: {ex.Message}");
    }
    await app.StopAsync();
}
void Expect(bool condition, string message) { if (!condition) throw new Exception(message); }

await Test("create returns 201, Location and body", async client =>
{
    var response = await client.PostAsJsonAsync("/notes", new { title = "Buy tea" });
    Expect(response.StatusCode == HttpStatusCode.Created, $"status was {(int)response.StatusCode}");
    Expect(response.Headers.Location?.ToString() == "/notes/1", $"Location was {response.Headers.Location}");
    var note = await response.Content.ReadFromJsonAsync<Note>();
    Expect(note == new Note(1, "Buy tea"), $"body was {note}");
});

await Test("new app starts empty (isolation)", async client =>
{
    var notes = await client.GetFromJsonAsync<List<Note>>("/notes");
    Expect(notes is { Count: 0 }, $"found {notes?.Count} notes left by another test");
});

await Test("missing note is a 404 problem", async client =>
{
    var response = await client.GetAsync("/notes/42");
    Expect(response.StatusCode == HttpStatusCode.NotFound, $"status was {(int)response.StatusCode}");
    Expect(response.Content.Headers.ContentType?.MediaType == "application/problem+json", "not problem+json");
    var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
    Expect(problem?.Detail == "Note 42 was not found", $"detail was {problem?.Detail}");
});

await Test("duplicate title is a 409 and changes nothing", async client =>
{
    await client.PostAsJsonAsync("/notes", new { title = "Plan" });
    var second = await client.PostAsJsonAsync("/notes", new { title = "plan" });
    Expect(second.StatusCode == HttpStatusCode.Conflict, $"status was {(int)second.StatusCode}");
    var notes = await client.GetFromJsonAsync<List<Note>>("/notes");
    Expect(notes!.Count == 1, $"expected 1 note, found {notes.Count}");
});

await Test("delete removes the note (side effect)", async client =>
{
    await client.PostAsJsonAsync("/notes", new { title = "Temp" });
    Expect((await client.DeleteAsync("/notes/1")).StatusCode == HttpStatusCode.NoContent, "delete was not 204");
    Expect((await client.GetAsync("/notes/1")).StatusCode == HttpStatusCode.NotFound, "note is still there");
});

await Test("deliberately wrong expectation", async client =>
{
    var response = await client.PostAsJsonAsync("/notes", new { title = "" });
    Expect(response.StatusCode == HttpStatusCode.Created, $"status was {(int)response.StatusCode}");
});

results.ForEach(Console.WriteLine);
Console.WriteLine($"{results.Count(r => r.StartsWith("PASS"))}/{results.Count} passed");

record Note(int Id, string Title);
record NoteInput(string? Title);

interface INoteRepository
{
    IReadOnlyList<Note> List();
    Note? Find(int id);
    bool Exists(string title);
    Note Add(string title);
    bool Remove(int id);
}

class InMemoryNoteRepository : INoteRepository
{
    private readonly List<Note> notes = [];
    private int lastId;
    public IReadOnlyList<Note> List() => notes.ToList();
    public Note? Find(int id) => notes.FirstOrDefault(n => n.Id == id);
    public bool Exists(string title) => notes.Any(n => string.Equals(n.Title, title, StringComparison.OrdinalIgnoreCase));
    public Note Add(string title) { var note = new Note(++lastId, title); notes.Add(note); return note; }
    public bool Remove(int id) => notes.RemoveAll(n => n.Id == id) > 0;
}
```
```output
PASS create returns 201, Location and body
PASS new app starts empty (isolation)
PASS missing note is a 404 problem
PASS duplicate title is a 409 and changes nothing
PASS delete removes the note (side effect)
FAIL deliberately wrong expectation: status was 400
5/6 passed
```

Тест «new app starts empty» проходит именно потому, что предыдущие тесты создавали записи в **других** приложениях.

Замена сервисов перед `Build()`: фиксированные часы и счётчик идентификаторов делают вывод детерминированным, шпион проверяет побочный эффект, а ломающееся хранилище проверяет реакцию на сбой:

```csharp run
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;

// Приложение зависит от абстракций времени и идентификаторов, а не от DateTime.UtcNow и Guid.NewGuid()
WebApplication CreateApp(Action<IServiceCollection>? replaceServices = null)
{
    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseTestServer();
    builder.Logging.ClearProviders();
    builder.Services.AddProblemDetails();
    builder.Services.AddSingleton<IIdGenerator, GuidIdGenerator>();
    builder.Services.AddSingleton<ITicketStore, MemoryTicketStore>();
    builder.Services.AddSingleton(TimeProvider.System);

    replaceServices?.Invoke(builder.Services);      // замены регистраций - строго ДО Build()

    var app = builder.Build();
    app.UseExceptionHandler();
    app.MapPost("/tickets", (TicketInput input, IIdGenerator ids, TimeProvider clock, ITicketStore store) =>
    {
        var ticket = new Ticket(ids.Next(), input.Subject, clock.GetUtcNow());
        store.Save(ticket);
        return Results.Created($"/tickets/{ticket.Id}", ticket);
    });
    return app;
}

async Task<string> Send(WebApplication app, string subject)
{
    await app.StartAsync();
    var response = await app.GetTestClient().PostAsJsonAsync("/tickets", new { subject });
    var type = response.Content.Headers.ContentType?.MediaType ?? "-";
    return $"{(int)response.StatusCode} {type} {await response.Content.ReadAsStringAsync()}";
}

// 1. Реальные Guid и время недетерминированы: утверждать можно только форму
await using (var first = CreateApp())
await using (var second = CreateApp())
{
    string a = await Send(first, "printer"), b = await Send(second, "printer");
    Console.WriteLine($"real:  both 201: {a.StartsWith("201") && b.StartsWith("201")}, identical text: {a == b}");
}

// 2. Подмена: фиксированное время и счётчик вместо Guid - вывод детерминирован
await using (var app = CreateApp(services =>
{
    services.RemoveAll<TimeProvider>();
    services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)));
    services.RemoveAll<IIdGenerator>();
    services.AddSingleton<IIdGenerator, SequentialIds>();
}))
{
    Console.WriteLine($"fake:  {await Send(app, "printer")}");
}

// 3. Шпион: замена хранилища позволяет проверить побочный эффект, не читая его через HTTP
var spy = new SpyStore();
await using (var app = CreateApp(services => services.AddSingleton<ITicketStore>(spy)))   // последняя регистрация побеждает
{
    await Send(app, "monitor");
    Console.WriteLine($"spy saved: {string.Join(", ", spy.Saved.Select(t => t.Subject))}");
}

// 4. Хранилище, которое ломается: проверяем реакцию приложения на сбой зависимости
await using (var app = CreateApp(services => services.AddSingleton<ITicketStore, BrokenStore>()))
{
    var text = await Send(app, "mouse");       // тело содержит traceId, поэтому печатаем только его признаки
    Console.WriteLine($"broken: {text[..text.IndexOf('{')]}| leaks message: {text.Contains("db is down")}");
}

record Ticket(string Id, string Subject, DateTimeOffset CreatedAt);
record TicketInput(string Subject);

interface IIdGenerator { string Next(); }
interface ITicketStore { void Save(Ticket ticket); }

class GuidIdGenerator : IIdGenerator { public string Next() => Guid.NewGuid().ToString("N"); }
class SequentialIds : IIdGenerator { private int n; public string Next() => $"T-{++n}"; }
class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
class MemoryTicketStore : ITicketStore { public void Save(Ticket ticket) { } }
class SpyStore : ITicketStore { public List<Ticket> Saved { get; } = []; public void Save(Ticket ticket) => Saved.Add(ticket); }
class BrokenStore : ITicketStore { public void Save(Ticket ticket) => throw new InvalidOperationException("db is down"); }
```
```output
real:  both 201: True, identical text: False
fake:  201 application/json {"id":"T-1","subject":"printer","createdAt":"2026-01-02T03:04:05+00:00"}
spy saved: monitor
broken: 500 application/problem+json | leaks message: False
```

С настоящими `Guid` и временем два запуска дают разный текст, с двойниками - одинаковый. Сообщение исключения (`db is down`) клиенту не попало.

<!-- section: mistakes -->
## Типичные ошибки

* **Одно приложение на все тесты.** Общее хранилище превращает тесты в цепочку: добавили запись в одном - сломали ожидание другого. Создавайте приложение на каждый тест.
* **Утверждать только код состояния.** `201` без проверки `Location` и тела пропускает ошибки в самом важном, а `204` без повторного `GET` не доказывает, что запись удалена.
* **Сравнивать недетерминированные значения.** `Guid.NewGuid()`, `DateTime.UtcNow` и `traceId` меняются между запусками; подставьте двойники или не включайте эти поля в сравнение.
* **Заменять сервис после `Build()`.** После сборки коллекция сервисов доступна только для чтения (попытка добавить регистрацию даёт `InvalidOperationException`); регистрации меняют до сборки.
* **Тестировать через реальную базу и сеть.** Это медленно и нестабильно; для проверки HTTP-контракта хватает хранилища в памяти.
* **Забывать про сбои.** Проверка «всё хорошо» не показывает, что клиент увидит при ошибке зависимости; добавьте тест с ломающимся двойником.
* **Ждать исключение от `SendAsync`, когда в приложении есть `UseExceptionHandler`.** С ним необработанное исключение становится ответом 500, а не исключением в тесте.
* **Использовать `WebApplicationFactory` без пакета.** Она живёт в отдельном пакете `Microsoft.AspNetCore.Mvc.Testing`; без него (как в этом курсе) собирайте приложение вручную через `TestServer`.

<!-- section: summary -->
## Итоги

* Интеграционный тест проходит весь конвейер: `UseTestServer()` + `GetTestClient()` без сети и порта.
* `WebApplicationFactory<Program>` из `Microsoft.AspNetCore.Mvc.Testing` запускает настоящее приложение проекта; курс собирает приложение вручную, принципы те же.
* Замена сервисов - до `Build()`: `RemoveAll<T>` + регистрация двойника (часы, идентификаторы, шпион, ломающееся хранилище).
* Проверяйте код, заголовки, тело и побочные эффекты; `JsonContent`/`PostAsJsonAsync` и `ReadFromJsonAsync<T>` упрощают работу с JSON.
* Одно приложение на тест - основа изоляции.
* `ETag` + `If-None-Match` (304) и `If-Match` (412) делают чтение и изменение условными.

Это последний урок курса: теперь вы можете построить, обработать ошибки и протестировать HTTP API на ASP.NET Core. Параллель из другого мира: [тестирование в Express](lesson:express.fundamentals.testing).
