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
        if (exception is not DomainException domain) return false;

        var problem = new ProblemDetails
        {
            Detail = domain.Message,
            Instance = httpContext.Request.Path,
        };
        switch (domain)
        {
            case NotFoundException:
                problem.Status = StatusCodes.Status404NotFound;
                problem.Title = "Not Found";
                problem.Type = "https://example.com/problems/not-found";
                break;
            case ConflictException:
                problem.Status = StatusCodes.Status409Conflict;
                problem.Title = "Conflict";
                problem.Type = "https://example.com/problems/conflict";
                break;
            case RuleViolationException violation:
                problem.Status = StatusCodes.Status400BadRequest;
                problem.Title = "Validation failed";
                problem.Type = "https://example.com/problems/validation";
                problem.Extensions["errors"] = violation.Errors;
                break;
            default:
                return false;
        }

        httpContext.Response.StatusCode = problem.Status.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }
}

public static class Api
{
    public static void Configure(WebApplicationBuilder builder)
    {
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<DomainExceptionHandler>();
    }

    public static void Map(WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();

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

        app.MapGet("/search", (string? q) =>
            string.IsNullOrWhiteSpace(q)
                ? Results.ValidationProblem(new Dictionary<string, string[]> { ["q"] = ["q is required"] })
                : Results.Ok(new { query = q, found = orders.Count(o => o.Item.Contains(q, StringComparison.OrdinalIgnoreCase)) }));
    }
}
