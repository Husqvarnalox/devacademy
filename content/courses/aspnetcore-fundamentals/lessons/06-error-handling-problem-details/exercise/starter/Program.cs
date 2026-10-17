using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

// Драйвер проверки: его менять не нужно. Он собирает приложение, подаёт запросы из stdin прямо в него
// через TestServer (порт не открывается) и печатает ответы. Одна строка ввода - один запрос:
//   METHOD /path [| h:Имя=значение] [| b:тело]
// Для каждого ответа печатается "код тип тело". В JSON-ответах удалено поле traceId (оно меняется от запуска
// к запуску), остальные поля печатаются как есть. Если исключение не обработано приложением и дошло до
// клиента теста, печатается "unhandled <тип исключения>".
WebApplicationBuilder builder = WebApplication.CreateBuilder();
builder.WebHost.UseTestServer();
builder.Logging.ClearProviders();
Api.Configure(builder);
await using WebApplication app = builder.Build();
Api.Map(app);
await app.StartAsync();
using HttpClient client = app.GetTestClient();

var relaxed = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
string input = await Console.In.ReadToEndAsync();
foreach (string line in input.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
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
    try
    {
        using HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();
        string type = response.Content.Headers.ContentType?.MediaType ?? "-";
        if (type.EndsWith("json", StringComparison.Ordinal) && JsonNode.Parse(body) is JsonObject json)
        {
            json.Remove("traceId");
            body = json.ToJsonString(relaxed);
        }
        Console.WriteLine($"{(int)response.StatusCode} {type} {body}".TrimEnd());
    }
    catch (Exception ex)
    {
        Console.WriteLine($"unhandled {ex.GetType().Name}");
    }
}
await app.StopAsync();
