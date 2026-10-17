<!-- section: simple -->
## Настройки отдельно от кода

Адрес базы данных, лимиты, флаги функций, название магазина - всё это **конфигурация**: значения, которые меняются между машинами и средами, но не требуют перекомпиляции. В ASP.NET Core конфигурация - это набор **пар «ключ - строка»**, собранных из нескольких **источников** (провайдеров): файла `appsettings.json`, переменных окружения, аргументов командной строки, секретов разработчика и своих источников.

Иерархию превращают в плоские ключи с двоеточием: JSON `{ "Shop": { "Name": "Books" } }` даёт ключ `Shop:Name`. Источники применяются по порядку, и **более поздний перекрывает более ранний**: так переменная окружения или аргумент командной строки переопределяют значение из файла.

Читать значения по строковым ключам неудобно и ненадёжно, поэтому настройки связывают с классом - **шаблон параметров** (options pattern). Вы описываете класс `ShopOptions`, привязываете к нему секцию `Shop`, а обработчик просит `IOptions<ShopOptions>` и получает готовый типизированный объект. Заодно параметры можно **проверять**: неверная конфигурация должна останавливать запуск, а не ломать запросы позже.

В этом курсе вместо файла на диске мы читаем JSON из памяти (`AddJsonStream`) и словарь (`AddInMemoryCollection`): так пример состоит из одного `Program.cs`, а поведение провайдеров то же.

<!-- section: precise -->
## Точные правила

**Источники и приоритет.** `WebApplication.CreateBuilder` заранее подключает `appsettings.json`, `appsettings.{Environment}.json`, секреты пользователя (в среде Development), переменные окружения и аргументы командной строки; эти источники перечислены от низшего приоритета к высшему. Всё, что вы добавляете через `builder.Configuration.Add...`, ставится **после** уже подключённых и потому побеждает их. Ключи регистронезависимы.

**Ключи.** Уровни иерархии разделяет `:`. В переменных окружения двоеточие часто недопустимо, поэтому используют `__`: `Shop__Limits__MaxItems` превращается в `Shop:Limits:MaxItems`. Элементы массива - числовые ключи: `Shop:Tags:0`, `Shop:Tags:1`. Аргументы командной строки: `--Shop:Name=Books` или `Shop:Name=Books`.

**Чтение.** `config["Shop:Name"]` возвращает `string?` (отсутствующий ключ - `null`). `GetSection("Shop")` возвращает `IConfigurationSection` (с `Path`, `Key`, `Value`, `GetChildren()`); раздел всегда не `null`, даже если данных нет. `GetValue<T>(key, default)` преобразует значение к типу, `section.Get<T>()` создаёт и заполняет объект, `section.Bind(obj)` заполняет существующий. Значение со структурой (объект) само строки не хранит: `section.Value` равен `null`, а данные лежат в дочерних ключах.

**Шаблон параметров.** `services.Configure<T>(section)` или `services.AddOptions<T>().Bind(section)` регистрирует привязку. Получить значение можно тремя способами:

* `IOptions<T>` - singleton; значение вычисляется при первом обращении и **дальше не меняется**, даже если конфигурация перезагрузилась;
* `IOptionsSnapshot<T>` - scoped; значение пересчитывается на каждый запрос (область видимости) и видит изменения конфигурации; для singleton-сервисов он не подходит;
* `IOptionsMonitor<T>` - singleton; `CurrentValue` всегда актуально, `OnChange` сообщает об изменениях. Чтобы изменение произошло, источник должен уметь перезагружаться и сообщать об этом (файловые провайдеры с `reloadOnChange`; `AddInMemoryCollection` перезагрузку не сообщает).

**Проверка параметров.** `ValidateDataAnnotations()` проверяет атрибуты `[Required]`, `[Range]` и другие, `Validate(predicate, message)` добавляет своё правило. Без `ValidateOnStart()` проверка выполняется лениво - при первом чтении `Value`, и ошибка (`OptionsValidationException`) вылетает из запроса; с `ValidateOnStart()` приложение не стартует с неверной конфигурацией. Все нарушения собираются вместе: `ex.Failures`.

**Среда.** `IHostEnvironment` (`app.Environment`) хранит `EnvironmentName`; `IsDevelopment()`, `IsStaging()`, `IsProduction()` и `IsEnvironment("X")` сравнивают без учёта регистра. Имя берётся из `DOTNET_ENVIRONMENT`/`ASPNETCORE_ENVIRONMENT` (по умолчанию `Production`) или задаётся при создании: `new WebApplicationOptions { EnvironmentName = "Staging" }`. От среды зависит файл `appsettings.{Environment}.json`, страница ошибок разработчика и условная регистрация маршрутов.

<!-- section: examples -->
## Примеры

Провайдеры, приоритет, иерархические ключи и разные способы чтения. JSON подаётся из памяти, затем переменная окружения и аргумент командной строки перекрывают его:

```csharp run
using System.Text;
using Microsoft.AspNetCore.TestHost;

var builder = WebApplication.CreateBuilder();
builder.WebHost.UseTestServer();
builder.Logging.ClearProviders();

// Провайдеры применяются по порядку: поздний перекрывает ранний.
var json = """
{ "Shop": { "Name": "Books", "Limits": { "MaxItems": 10, "Tags": ["new", "sale"] } }, "Debug": "false" }
""";
builder.Configuration.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)));
Environment.SetEnvironmentVariable("Shop__Limits__MaxItems", "20");
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(["--Shop:Name=CLI Books"]);

var config = builder.Configuration;
Console.WriteLine($"Shop:Name = {config["Shop:Name"]}");
Console.WriteLine($"Shop:Limits:MaxItems = {config["Shop:Limits:MaxItems"]}");
Console.WriteLine($"Shop:Limits:Tags:1 = {config["Shop:Limits:Tags:1"]}");
Console.WriteLine($"Missing = {config["Shop:Nope"] ?? "<null>"}");

var section = config.GetSection("Shop:Limits");
Console.WriteLine($"section.Path = {section.Path}, Key = {section.Key}, Value = {section.Value ?? "<null>"}");
Console.WriteLine($"GetValue<int> = {section.GetValue<int>("MaxItems")}, default = {section.GetValue("Absent", 7)}");
Console.WriteLine($"Get<string[]> = {string.Join(",", section.GetSection("Tags").Get<string[]>()!)}");
Console.WriteLine($"Debug as bool = {config.GetValue<bool>("Debug")}");
Console.WriteLine($"children = {string.Join(",", config.GetSection("Shop").GetChildren().Select(c => c.Key))}");

var limits = new Limits();
section.Bind(limits);
Console.WriteLine($"Bind -> MaxItems={limits.MaxItems}, Tags=[{string.Join(",", limits.Tags)}]");

builder.Services.Configure<Limits>(section);
var app = builder.Build();
app.MapGet("/limits", (Microsoft.Extensions.Options.IOptions<Limits> o) => o.Value);
await app.StartAsync();
var client = app.GetTestClient();
Console.WriteLine(await client.GetStringAsync("/limits"));
await app.StopAsync();

class Limits
{
    public int MaxItems { get; set; }
    public List<string> Tags { get; set; } = [];
}
```
```output
Shop:Name = CLI Books
Shop:Limits:MaxItems = 20
Shop:Limits:Tags:1 = sale
Missing = <null>
section.Path = Shop:Limits, Key = Limits, Value = <null>
GetValue<int> = 20, default = 7
Get<string[]> = new,sale
Debug as bool = False
children = Limits,Name
Bind -> MaxItems=20, Tags=[new,sale]
{"maxItems":20,"tags":["new","sale"]}
```

Поздние источники победили: `Shop:Name` взят из командной строки, `MaxItems` - из переменной окружения (`__` стало `:`), массив читается по индексам и целиком.

`IOptions`, `IOptionsSnapshot` и `IOptionsMonitor` при изменении конфигурации. Источник умеет сообщать об изменении (`OnReload`), как это делает JSON-файл с `reloadOnChange`:

```csharp run
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder();
builder.WebHost.UseTestServer();
builder.Logging.ClearProviders();

var source = new ReloadableProvider(new() { ["Promo:Title"] = "v1", ["Promo:Percent"] = "10" });
((IConfigurationBuilder)builder.Configuration).Add(new ReloadableSource(source));

builder.Services.AddOptions<Promo>().Bind(builder.Configuration.GetSection("Promo")).ValidateDataAnnotations();

var app = builder.Build();
app.MapGet("/promo", (IOptions<Promo> once, IOptionsSnapshot<Promo> snapshot, IOptionsMonitor<Promo> monitor) =>
    $"IOptions={once.Value.Title} Snapshot={snapshot.Value.Title} Monitor={monitor.CurrentValue.Title}");
app.MapGet("/percent", (IOptionsSnapshot<Promo> snapshot) => snapshot.Value.Percent);

await app.StartAsync();
var client = app.GetTestClient();

app.Services.GetRequiredService<IOptionsMonitor<Promo>>()
    .OnChange(p => Console.WriteLine($"OnChange -> {p.Title}"));

Console.WriteLine(await client.GetStringAsync("/promo"));
source.Update("Promo:Title", "v2");
Console.WriteLine(await client.GetStringAsync("/promo"));

Console.WriteLine($"percent = {await client.GetStringAsync("/percent")}");
source.Set("Promo:Percent", "500");   // значение изменено без уведомления
try { await client.GetStringAsync("/percent"); }
catch (OptionsValidationException ex) { Console.WriteLine(ex.Message); }

await app.StopAsync();

class Promo
{
    [Required] public string Title { get; set; } = "";
    [Range(0, 50)] public int Percent { get; set; }
}

class ReloadableSource(ReloadableProvider provider) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) => provider;
}

class ReloadableProvider(Dictionary<string, string?> data) : ConfigurationProvider
{
    public override void Load() => Data = new Dictionary<string, string?>(data, StringComparer.OrdinalIgnoreCase);

    public void Update(string key, string value)
    {
        Data[key] = value;
        OnReload();   // сообщает токену изменения: IOptionsMonitor пересоздаст значение
    }
}
```
```output
IOptions=v1 Snapshot=v1 Monitor=v1
OnChange -> v2
IOptions=v1 Snapshot=v2 Monitor=v2
percent = 10
DataAnnotation validation failed for 'Promo' members: 'Percent' with the error: 'The field Percent must be between 0 and 50.'.
```

`IOptions` застыл на первом значении, `IOptionsSnapshot` увидел `v2` в следующем запросе, `IOptionsMonitor` - сразу и вызвал `OnChange`. Неверное значение `Percent` обнаружил `[Range]` при первом чтении `Snapshot` - то есть внутри запроса.

Среда и проверка при старте. Все нарушения собираются вместе, а приложение не запускается:

```csharp run
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Staging" });
builder.WebHost.UseTestServer();
builder.Logging.ClearProviders();
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Mail:Host"] = "smtp local",
    ["Mail:Port"] = "70000",
});

builder.Services.AddOptions<MailOptions>()
    .Bind(builder.Configuration.GetSection("Mail"))
    .ValidateDataAnnotations()
    .Validate(o => !o.Host.Contains(' '), "Host must not contain spaces")
    .ValidateOnStart();

var app = builder.Build();
var env = app.Environment;
Console.WriteLine($"{env.EnvironmentName}: IsDevelopment={env.IsDevelopment()}, IsProduction={env.IsProduction()}, IsStaging={env.IsStaging()}");

try
{
    await app.StartAsync();
    Console.WriteLine("started");
}
catch (OptionsValidationException ex)
{
    Console.WriteLine($"startup failed for {ex.OptionsType.Name}:");
    foreach (var failure in ex.Failures) Console.WriteLine($"  - {failure}");
}

class MailOptions
{
    [Required] public string Host { get; set; } = "";
    [Range(1, 65535)] public int Port { get; set; } = 25;
}
```
```output
Staging: IsDevelopment=False, IsProduction=False, IsStaging=True
startup failed for MailOptions:
  - DataAnnotation validation failed for 'MailOptions' members: 'Port' with the error: 'The field Port must be between 1 and 65535.'.
  - Host must not contain spaces
```


<!-- section: mistakes -->
## Типичные ошибки

* **Ждать перезагрузки от `IOptions<T>`.** Он читает значение один раз. Для изменяемых настроек нужен `IOptionsSnapshot<T>` (в запросах) или `IOptionsMonitor<T>` (в singleton-сервисах).
* **Внедрять `IOptionsSnapshot<T>` в singleton.** Он scoped и живёт один запрос: singleton, захвативший его, застрянет на старом значении (а проверка областей в Development вообще откажется его создать); берите `IOptionsMonitor<T>`.
* **Не включать `ValidateOnStart()`.** Без него опечатка в конфигурации проявится только при первом запросе, использующем параметры.
* **Использовать `:` в имени переменной окружения.** В `bash` и многих системах это не работает; пишите `Shop__Name`.
* **Хранить секреты в `appsettings.json` в репозитории.** Для разработки есть секреты пользователя, для боевой среды - переменные окружения или хранилище секретов.
* **Ждать `null` от `GetSection`.** Раздел возвращается всегда; наличие данных проверяют через `Exists()` или `GetChildren()`.
* **Читать `config["Key"]` повсюду.** Строковые ключи не проверяются компилятором; привяжите секцию к классу один раз и передавайте `IOptions<T>`.

<!-- section: summary -->
## Итоги

* Конфигурация - плоские ключи `A:B:C` из нескольких источников; более поздний источник перекрывает ранний.
* В переменных окружения `__` заменяет `:`; аргументы командной строки приходят последними.
* `GetSection`, `GetValue`, `Get<T>`, `Bind` читают значения; для кода приложения привязывайте секции к классам.
* `IOptions<T>` читается один раз, `IOptionsSnapshot<T>` пересчитывается на запрос, `IOptionsMonitor<T>` всегда актуален.
* `ValidateDataAnnotations`, `Validate` и `ValidateOnStart` останавливают запуск при неверной конфигурации.
* `IHostEnvironment` отличает Development от Production и выбирает файлы настроек и маршруты.

Дальше: [обработка ошибок и ProblemDetails](lesson:csharp.aspnetcore.error-handling-problem-details).
