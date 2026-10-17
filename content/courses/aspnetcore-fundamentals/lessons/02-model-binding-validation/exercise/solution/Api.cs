using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Role { Guest, User, Admin }

public record User(int Id, string Name, string Email, int Age, Role Role, DateOnly JoinedOn);

// Тело POST /register. Правила DataAnnotations проверяет встроенная валидация (AddValidation),
// поэтому тип обязан быть public.
public record Registration(
    [Required, StringLength(20, MinimumLength = 2)] string Name,
    [Required, EmailAddress] string Email,
    [Range(18, 120)] int Age,
    Role Role = Role.User);

// Своя привязка параметра запроса: ?sort=age:desc
public readonly record struct SortSpec(string Field, bool Descending)
{
    public static bool TryParse(string? value, IFormatProvider? provider, out SortSpec result)
    {
        result = default;
        if (string.IsNullOrEmpty(value)) return false;
        var parts = value.Split(':');
        if (parts.Length > 2) return false;
        var field = parts[0].ToLowerInvariant();
        if (field is not ("name" or "age" or "joined")) return false;
        var descending = false;
        if (parts.Length == 2)
        {
            if (parts[1] == "desc") descending = true;
            else if (parts[1] != "asc") return false;
        }
        result = new SortSpec(field, descending);
        return true;
    }
}

// Параметры запроса одним объектом: [AsParameters].
public record UserFilter(Role? Role, DateOnly? JoinedAfter, SortSpec? Sort, [Range(1, 10)] int Limit = 10);

public static class Api
{
    public static void Configure(WebApplicationBuilder builder)
    {
        builder.Services.AddValidation();
    }

    public static void Map(WebApplication app)
    {
        var users = new List<User>
        {
            new(1, "Ann", "ann@example.org", 31, Role.Admin, new DateOnly(2024, 1, 15)),
            new(2, "Bob", "bob@example.org", 24, Role.User, new DateOnly(2024, 6, 1)),
            new(3, "Cid", "cid@example.org", 45, Role.User, new DateOnly(2025, 2, 10)),
            new(4, "Dina", "dina@example.org", 19, Role.Guest, new DateOnly(2025, 3, 1)),
        };

        app.MapGet("/users", ([AsParameters] UserFilter filter) =>
        {
            IEnumerable<User> query = users;
            if (filter.Role is { } role) query = query.Where(u => u.Role == role);
            if (filter.JoinedAfter is { } after) query = query.Where(u => u.JoinedOn > after);
            if (filter.Sort is { } sort)
            {
                query = (sort.Field, sort.Descending) switch
                {
                    ("name", false) => query.OrderBy(u => u.Name),
                    ("name", true) => query.OrderByDescending(u => u.Name),
                    ("age", false) => query.OrderBy(u => u.Age),
                    ("age", true) => query.OrderByDescending(u => u.Age),
                    (_, false) => query.OrderBy(u => u.JoinedOn),
                    _ => query.OrderByDescending(u => u.JoinedOn),
                };
            }
            return query.Take(filter.Limit).Select(u => u.Name);
        });

        app.MapPost("/register", (Registration input) =>
        {
            if (users.Any(u => string.Equals(u.Email, input.Email, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Email"] = ["Этот адрес уже зарегистрирован."],
                });
            }
            var user = new User(users.Max(u => u.Id) + 1, input.Name, input.Email, input.Age, input.Role, new DateOnly(2025, 6, 1));
            users.Add(user);
            return Results.Created($"/users/{user.Id}", user);
        });
    }
}
