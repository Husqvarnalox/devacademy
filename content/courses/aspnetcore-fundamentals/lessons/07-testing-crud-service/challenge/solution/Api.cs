using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

public record Item(int Id, string Name, decimal Price);

public record ItemInput(string? Name, decimal? Price);

// Хранилище товаров в памяти: готово, менять не нужно.
public sealed class ItemStore
{
    private readonly Dictionary<int, Item> items = new()
    {
        [1] = new(1, "Tea", 2.5m),
        [2] = new(2, "Coffee", 3m),
    };

    public Item? Find(int id) => items.GetValueOrDefault(id);

    public Item Save(Item item) => items[item.Id] = item;
}

public static class Api
{
    // Те же настройки (camelCase), что использует Results.Ok / возврат объекта из обработчика.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void Configure(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ItemStore>();
    }

    // Сильный ETag: первые 16 символов SHA-256 от байтов представления (строчные hex-цифры) в кавычках.
    public static string ETagOf(ReadOnlySpan<byte> representation) =>
        $"\"{Convert.ToHexStringLower(SHA256.HashData(representation))[..16]}\"";

    // Список тегов из If-Match / If-None-Match: через запятую, слабый префикс W/ отбрасываем (для If-None-Match допустимо сравнение по слабой схеме).
    private static IEnumerable<string> Tags(string? header) =>
        (header ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.StartsWith("W/", StringComparison.Ordinal) ? tag[2..] : tag);

    public static void Map(WebApplication app)
    {
        // Условный GET: буферизуем тело успешного GET, проставляем ETag и отвечаем 304, если If-None-Match совпал.
        app.Use(async (context, next) =>
        {
            if (!HttpMethods.IsGet(context.Request.Method)) { await next(); return; }

            Stream original = context.Response.Body;
            using var buffer = new MemoryStream();
            context.Response.Body = buffer;
            try { await next(); }
            finally { context.Response.Body = original; }

            if (context.Response.StatusCode == StatusCodes.Status200OK && buffer.Length > 0)
            {
                string etag = ETagOf(buffer.ToArray());
                context.Response.Headers.ETag = etag;
                var candidates = Tags(context.Request.Headers.IfNoneMatch.ToString()).ToList();
                if (candidates.Contains("*") || candidates.Contains(etag))
                {
                    context.Response.StatusCode = StatusCodes.Status304NotModified;
                    context.Response.ContentType = null;
                    context.Response.ContentLength = null;
                    return;
                }
            }
            buffer.Position = 0;
            await buffer.CopyToAsync(original);
        });

        app.MapGet("/items/{id:int}", (int id, ItemStore store) =>
            store.Find(id) is { } item ? Results.Ok(item) : NotFound(id));

        app.MapPut("/items/{id:int}", (int id, ItemInput input, ItemStore store, HttpContext context) =>
        {
            if (store.Find(id) is not { } current) return NotFound(id);

            // If-Match: сильное сравнение с текущим ETag; "*" подходит любому существующему ресурсу
            string currentTag = ETagOf(JsonSerializer.SerializeToUtf8Bytes(current, Json));
            if (context.Request.Headers.IfMatch.Count > 0)
            {
                var offered = context.Request.Headers.IfMatch.ToString()
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (!offered.Contains("*") && !offered.Contains(currentTag))
                    return Results.Problem(statusCode: 412, title: "Precondition Failed", detail: "The item was changed by someone else");
            }

            if (string.IsNullOrWhiteSpace(input.Name) || input.Price is not > 0)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["body"] = ["name and a positive price are required"] });

            Item saved = store.Save(new Item(id, input.Name, input.Price.Value));
            context.Response.Headers.ETag = ETagOf(JsonSerializer.SerializeToUtf8Bytes(saved, Json));
            return Results.Ok(saved);
        });
    }

    private static IResult NotFound(int id) =>
        Results.Problem(statusCode: 404, title: "Not Found", detail: $"Item {id} was not found");
}
