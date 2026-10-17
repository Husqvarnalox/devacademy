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
    public static void Configure(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ItemStore>();
    }

    public static void Map(WebApplication app)
    {
        // TODO 1: ETag. Сильный ETag - первые 16 символов SHA-256 (строчные hex-цифры) от БАЙТОВ ответа, в двойных кавычках:
        //   "796b614f6e826586". Нужна функция вида string ETagOf(ReadOnlySpan<byte> bytes) (SHA256.HashData, Convert.ToHexStringLower).
        // TODO 2: Условный GET - middleware (app.Use). Для успешного (200) GET с телом буферизуйте ответ (подмените Response.Body
        //   на MemoryStream), выставьте заголовок ETag по байтам тела; если If-None-Match содержит этот тег
        //   (список через запятую, префикс W/ игнорируется, "*" подходит всегда) - ответ 304 без тела и без Content-Type
        //   (ETag в 304 остаётся). Иначе скопируйте буфер в настоящий поток ответа.
        // TODO 3: GET /items/{id:int} - товар (Results.Ok) или 404: Results.Problem(statusCode: 404, title: "Not Found", detail: "Item {id} was not found").
        // TODO 4: PUT /items/{id:int} с телом ItemInput: товара нет - 404 как выше; заголовок If-Match задан, но не содержит ТЕКУЩИЙ ETag
        //   (тот же алгоритм от JSON товара в формате JsonSerializerDefaults.Web) и не "*" - 412:
        //   Results.Problem(statusCode: 412, title: "Precondition Failed", detail: "The item was changed by someone else");
        //   пустое имя или цена <= 0 - 400 (Results.ValidationProblem); иначе store.Save, 200 с товаром и заголовок ETag нового представления.
    }
}
