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

        app.MapGet("/books", (string? author) =>
            author is null ? books : books.Where(b => string.Equals(b.Author, author, StringComparison.OrdinalIgnoreCase)));

        app.MapGet("/books/{id:int}", (int id) =>
            books.FirstOrDefault(b => b.Id == id) is { } book ? Results.Ok(book) : Results.NotFound());

        app.MapPost("/books", (NewBook input) =>
        {
            var book = new Book(books.Max(b => b.Id) + 1, input.Title, input.Author);
            books.Add(book);
            return Results.Created($"/books/{book.Id}", book);
        });
    }
}
