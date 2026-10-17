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
        // TODO 2: разберите "поле" или "поле:asc" / "поле:desc". Поля: name, age, joined (регистр не важен);
        // направление по умолчанию - по возрастанию. Любой другой ввод - вернуть false (клиент получит 400).
        return false;
    }
}

// Параметры запроса одним объектом: [AsParameters].
public record UserFilter(Role? Role, DateOnly? JoinedAfter, SortSpec? Sort, [Range(1, 10)] int Limit = 10);

public static class Api
{
    public static void Configure(WebApplicationBuilder builder)
    {
        // TODO 1: включите встроенную валидацию DataAnnotations для minimal APIs.
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

        // TODO 3: параметры запроса одним объектом ([AsParameters] UserFilter): role и joinedAfter (строго позже даты)
        // фильтруют, sort упорядочивает, limit ограничивает число записей.
        app.MapGet("/users", () => users.Select(u => u.Name));

        // TODO 4: POST /register с телом Registration. Повторный email (без учёта регистра) - Results.ValidationProblem
        // с ключом "Email"; иначе добавьте пользователя (id = максимальный + 1, JoinedOn = 2025-06-01)
        // и верните 201 с заголовком Location: /users/<id>.
    }
}
