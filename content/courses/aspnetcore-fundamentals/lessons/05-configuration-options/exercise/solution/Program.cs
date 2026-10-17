using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;

// Драйвер проверки: его менять не нужно. Он собирает приложение, подаёт запросы из stdin прямо в него
// через TestServer (порт не открывается) и печатает ответы.
//
// Формат ввода. Необязательный блок настройки, затем строка `---`, затем запросы (одна строка - один запрос).
// Если строки `---` нет, весь ввод - запросы.
//
// Строки настройки выполняются по порядку; каждая добавляет источник конфигурации ПОСЛЕ предыдущих,
// поэтому более поздняя строка перекрывает более раннюю (так же работает порядок провайдеров):
//   cfg:Ключ:Путь=значение     - AddInMemoryCollection (как значения из appsettings.json)
//   arg:--Ключ:Путь=значение   - AddCommandLine (аргумент командной строки)
//   env:Ключ__Путь=значение    - переменная окружения процесса + AddEnvironmentVariables ('__' означает ':')
//   mode:Имя                   - имя среды приложения (IHostEnvironment.EnvironmentName), по умолчанию Production
//
// Запрос: METHOD /path [| h:Имя=значение] [| b:тело]. Для каждого ответа печатается "код тип тело".
// Если приложение не стартует из-за ошибки валидации параметров (ValidateOnStart), печатается
// "startup failed: <тип параметров>, failures: <число ошибок>" и работа заканчивается.
string[] lines = (await Console.In.ReadToEndAsync()).Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
int separator = Array.IndexOf(lines, "---");
string[] setup = separator < 0 ? [] : lines[..separator];
string[] requests = separator < 0 ? lines : lines[(separator + 1)..];

string? mode = setup.Where(l => l.StartsWith("mode:", StringComparison.Ordinal)).Select(l => l[5..]).LastOrDefault();
WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = mode });
builder.WebHost.UseTestServer();
builder.Logging.ClearProviders();

foreach (string line in setup)
{
    int colon = line.IndexOf(':');
    string kind = line[..colon], value = line[(colon + 1)..];
    switch (kind)
    {
        case "cfg":
            string[] pair = value.Split('=', 2);
            builder.Configuration.AddInMemoryCollection([new KeyValuePair<string, string?>(pair[0], pair[1])]);
            break;
        case "arg":
            builder.Configuration.AddCommandLine([value]);
            break;
        case "env":
            string[] variable = value.Split('=', 2);
            Environment.SetEnvironmentVariable(variable[0], variable[1]);
            builder.Configuration.AddEnvironmentVariables();
            break;
    }
}

Api.Configure(builder);
await using WebApplication app = builder.Build();
Api.Map(app);
try
{
    await app.StartAsync();
}
catch (OptionsValidationException ex)
{
    Console.WriteLine($"startup failed: {ex.OptionsType.Name}, failures: {ex.Failures.Count()}");
    return;
}
using HttpClient client = app.GetTestClient();

foreach (string line in requests)
{
    string[] parts = line.Split(" | ");
    string[] head = parts[0].Split(' ', 2);
    using var request = new HttpRequestMessage(new HttpMethod(head[0]), head[1]);
    foreach (string part in parts.Skip(1))
    {
        if (part.StartsWith("h:", StringComparison.Ordinal))
        {
            string[] header = part[2..].Split('=', 2);
            request.Headers.TryAddWithoutValidation(header[0], header[1]);
        }
        else if (part.StartsWith("b:", StringComparison.Ordinal))
            request.Content = new StringContent(part[2..], Encoding.UTF8, "application/json");
    }
    using HttpResponseMessage response = await client.SendAsync(request);
    string body = await response.Content.ReadAsStringAsync();
    string type = response.Content.Headers.ContentType?.MediaType ?? "-";
    Console.WriteLine($"{(int)response.StatusCode} {type} {body}".TrimEnd());
}
await app.StopAsync();
