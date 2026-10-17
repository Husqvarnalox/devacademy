using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

public record Book(int Id, string Title, string Author);

public record NewBook(string Title, string Author);

public static class Api
{
    public static void Configure(WebApplicationBuilder builder)
    {
    }

    public static void Map(WebApplication app)
    {
        var books = new List<Book> { new(1, "Dune", "Herbert"), new(2, "Emma", "Austen"), new(3, "Persuasion", "Austen") };

        app.MapGet("/ping", () => "pong");

        // TODO 1: фильтр ?author=... без учёта регистра (параметр запроса попадает в аргумент лямбды).
        app.MapGet("/books", () => books);

        // TODO 2: GET /books/{id:int} - 200 с книгой или 404 (Results.Ok / Results.NotFound).
        // TODO 3: POST /books с телом NewBook - 201 Created, заголовок Location: /books/<id>, id = максимальный + 1.
    }
}
