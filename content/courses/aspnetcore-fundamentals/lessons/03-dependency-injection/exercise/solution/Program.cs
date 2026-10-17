using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

// Драйвер проверки: его менять не нужно. Он собирает приложение, подаёт запросы из stdin прямо в него
// через TestServer (порт не открывается) и печатает ответы. Одна строка ввода - один запрос:
//   METHOD /path [| h:Имя=значение] [| b:тело]
WebApplicationBuilder builder = WebApplication.CreateBuilder();
builder.WebHost.UseTestServer();
builder.Logging.ClearProviders();
Api.Configure(builder);
WebApplication app = builder.Build();
Api.Map(app);
await app.StartAsync();
using HttpClient client = app.GetTestClient();

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
    using HttpResponseMessage response = await client.SendAsync(request);
    string body = await response.Content.ReadAsStringAsync();
    string type = response.Content.Headers.ContentType?.MediaType ?? "-";
    Console.WriteLine($"{(int)response.StatusCode} {type} {body}".TrimEnd());
    if (Api.Events.Count > 0) Console.WriteLine($"  disposed: {string.Join(", ", Api.Events)}");
    Api.Events.Clear();
}
await app.StopAsync();
await app.DisposeAsync();
if (Api.Events.Count > 0) Console.WriteLine($"shutdown: {string.Join(", ", Api.Events)}");
