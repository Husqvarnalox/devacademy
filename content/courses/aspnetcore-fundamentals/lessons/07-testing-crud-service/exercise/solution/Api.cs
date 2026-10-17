using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

public record Item(int Id, string Name, decimal Price);

// Тело POST и PUT: поля необязательные, чтобы отсутствующее значение можно было отличить и вернуть 400.
public record ItemInput(string? Name, decimal? Price);

public interface IItemRepository
{
    IReadOnlyList<Item> List();
    Item? Find(int id);
    bool NameTaken(string name, int? exceptId = null);
    Item Add(string name, decimal price);
    Item? Update(int id, string name, decimal price);
    bool Remove(int id);
}

// Хранилище в памяти: готово, менять не нужно. Новые id растут на единицу.
public sealed class InMemoryItemRepository : IItemRepository
{
    private readonly object gate = new();
    private readonly List<Item> items = [new(1, "Tea", 2.5m), new(2, "Coffee", 3m)];
    private int lastId = 2;

    public IReadOnlyList<Item> List() { lock (gate) return items.ToList(); }

    public Item? Find(int id) { lock (gate) return items.FirstOrDefault(i => i.Id == id); }

    public bool NameTaken(string name, int? exceptId = null)
    {
        lock (gate) return items.Any(i => i.Id != exceptId && string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public Item Add(string name, decimal price)
    {
        lock (gate)
        {
            var item = new Item(++lastId, name, price);
            items.Add(item);
            return item;
        }
    }

    public Item? Update(int id, string name, decimal price)
    {
        lock (gate)
        {
            int index = items.FindIndex(i => i.Id == id);
            if (index < 0) return null;
            return items[index] = new Item(id, name, price);
        }
    }

    public bool Remove(int id) { lock (gate) return items.RemoveAll(i => i.Id == id) > 0; }
}

public static class Api
{
    public static void Configure(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IItemRepository, InMemoryItemRepository>();
    }

    public static void Map(WebApplication app)
    {
        var items = app.MapGroup("/items");

        items.MapGet("/", (IItemRepository repository) => repository.List());

        items.MapGet("/{id:int}", (int id, IItemRepository repository) =>
            repository.Find(id) is { } item ? Results.Ok(item) : NotFound(id));

        items.MapPost("/", (ItemInput input, IItemRepository repository) =>
        {
            if (Validate(input) is { } errors) return Results.ValidationProblem(errors);
            if (repository.NameTaken(input.Name!)) return Conflict(input.Name!);
            Item item = repository.Add(input.Name!, input.Price!.Value);
            return Results.Created($"/items/{item.Id}", item);
        });

        items.MapPut("/{id:int}", (int id, ItemInput input, IItemRepository repository) =>
        {
            if (repository.Find(id) is null) return NotFound(id);
            if (Validate(input) is { } errors) return Results.ValidationProblem(errors);
            if (repository.NameTaken(input.Name!, id)) return Conflict(input.Name!);
            return Results.Ok(repository.Update(id, input.Name!, input.Price!.Value));
        });

        items.MapDelete("/{id:int}", (int id, IItemRepository repository) =>
            repository.Remove(id) ? Results.NoContent() : NotFound(id));
    }

    private static Dictionary<string, string[]>? Validate(ItemInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(input.Name)) errors["name"] = ["name is required"];
        if (input.Price is not > 0) errors["price"] = ["price must be greater than zero"];
        return errors.Count == 0 ? null : errors;
    }

    private static IResult NotFound(int id) =>
        Results.Problem(statusCode: 404, title: "Not Found", detail: $"Item {id} was not found");

    private static IResult Conflict(string name) =>
        Results.Problem(statusCode: 409, title: "Conflict", detail: $"Item '{name}' already exists");
}
