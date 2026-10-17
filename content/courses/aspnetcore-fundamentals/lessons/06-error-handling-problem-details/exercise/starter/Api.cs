using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

public record Order(int Id, string Item, int Quantity);

public record NewOrder(string? Item, int Quantity);

// Доменные исключения: слой данных и правила бросают их, не зная про HTTP.
public abstract class DomainException(string message) : Exception(message);

public sealed class NotFoundException(string resource, int id) : DomainException($"{resource} {id} was not found");

public sealed class ConflictException(string message) : DomainException(message);

public sealed class RuleViolationException(IDictionary<string, string[]> errors) : DomainException("One or more rules were violated")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}

// Превращает доменные исключения в ProblemDetails; неизвестные исключения оставляет фреймворку (вернёт false).
public sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // TODO 2: для DomainException соберите ProblemDetails и запишите его через problemDetails.TryWriteAsync(...):
        //   NotFoundException      -> 404, Title "Not Found",         Type "https://example.com/problems/not-found"
        //   ConflictException      -> 409, Title "Conflict",          Type "https://example.com/problems/conflict"
        //   RuleViolationException -> 400, Title "Validation failed", Type "https://example.com/problems/validation",
        //                             Extensions["errors"] = violation.Errors
        // Для всех: Detail = exception.Message, Instance = путь запроса. Перед записью задайте httpContext.Response.StatusCode.
        // Остальные исключения не обрабатывайте (верните false): фреймворк ответит общим 500 без деталей.
        await Task.CompletedTask;
        return false;
    }
}

public static class Api
{
    public static void Configure(WebApplicationBuilder builder)
    {
        // TODO 1: включите ProblemDetails (AddProblemDetails) и зарегистрируйте DomainExceptionHandler (AddExceptionHandler).
    }

    public static void Map(WebApplication app)
    {
        // TODO 3: подключите UseExceptionHandler() и UseStatusCodePages(), чтобы исключения и пустые 404/405 стали ProblemDetails.

        var orders = new List<Order> { new(1, "tea", 2), new(2, "coffee", 1) };

        app.MapGet("/orders/{id:int}", (int id) =>
            orders.FirstOrDefault(o => o.Id == id) ?? throw new NotFoundException("Order", id));

        app.MapPost("/orders", (NewOrder input) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(input.Item)) errors["item"] = ["item is required"];
            if (input.Quantity <= 0) errors["quantity"] = ["quantity must be positive"];
            if (errors.Count > 0) throw new RuleViolationException(errors);
            if (orders.Any(o => string.Equals(o.Item, input.Item, StringComparison.OrdinalIgnoreCase)))
                throw new ConflictException($"An order for '{input.Item}' already exists");

            var order = new Order(orders.Max(o => o.Id) + 1, input.Item!, input.Quantity);
            orders.Add(order);
            return Results.Created($"/orders/{order.Id}", order);
        });

        app.MapGet("/boom", () =>
        {
            if (orders.Count > 0) throw new InvalidOperationException("connection failed: Server=db;Password=hunter2");
            return "unreachable";
        });

        // TODO 4: GET /search?q=.. - если q пустой или не задан, верните Results.ValidationProblem с ошибкой {"q": ["q is required"]};
        // иначе 200 с JSON { query, found }, где found - число заказов, в названии которых есть q (без учёта регистра).
    }
}
