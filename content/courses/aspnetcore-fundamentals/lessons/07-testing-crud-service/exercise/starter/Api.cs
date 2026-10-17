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
        // TODO 1: зарегистрируйте IItemRepository в контейнере сервисов (одно хранилище на всё приложение: singleton).
    }

    public static void Map(WebApplication app)
    {
        // Все маршруты - под префиксом /items. Репозиторий запрашивайте параметром обработчика.
        // Ошибки возвращайте как ProblemDetails: Results.Problem(statusCode: ..., title: ..., detail: ...).

        // TODO 2: GET /items - список всех товаров (JSON).
        // TODO 3: GET /items/{id:int} - товар или 404: title "Not Found", detail "Item {id} was not found".
        // TODO 4: POST /items с телом ItemInput:
        //   - name пустой/пробелы -> ошибка "name": ["name is required"];
        //   - price не задана или <= 0 -> ошибка "price": ["price must be greater than zero"];
        //   - при ошибках 400 через Results.ValidationProblem (обе ошибки сразу);
        //   - имя уже занято (без учёта регистра, repository.NameTaken) -> 409: title "Conflict", detail "Item '{name}' already exists";
        //   - иначе repository.Add и 201 Created с заголовком Location: /items/{id} и самим товаром.
        // TODO 5: PUT /items/{id:int} с телом ItemInput: сначала 404 (если товара нет), затем те же проверки 400 и 409
        //   (409 - если имя занято ДРУГИМ товаром), затем repository.Update и 200 с обновлённым товаром.
        // TODO 6: DELETE /items/{id:int} - 204 без тела или 404 как выше.
    }
}
