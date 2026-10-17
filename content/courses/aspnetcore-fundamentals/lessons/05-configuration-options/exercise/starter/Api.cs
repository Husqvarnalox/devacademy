using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

// Параметры магазина: секция "Shop" конфигурации.
public class ShopOptions
{
    [Required]
    public string Name { get; set; } = "";

    public string Currency { get; set; } = "USD";

    [Range(1, 100)]
    public int MaxItems { get; set; } = 10;

    [Range(0, 50)]
    public int DiscountPercent { get; set; }
}

public static class Api
{
    public static void Configure(WebApplicationBuilder builder)
    {
        // TODO 1: зарегистрируйте ShopOptions: привяжите секцию "Shop" (builder.Configuration.GetSection),
        // включите ValidateDataAnnotations, добавьте правило Validate (Currency - ровно 3 символа)
        // и ValidateOnStart, чтобы неверная конфигурация останавливала запуск.
    }

    public static void Map(WebApplication app)
    {
        app.MapGet("/ping", () => "pong");

        // TODO 2: GET /shop - JSON { name, currency, maxItems, discountPercent } из IOptions<ShopOptions>.

        // TODO 3: GET /quote?price=..&qty=.. - если qty > MaxItems, ответ 400 с JSON { error: "too many items", maxItems };
        // иначе 200 с JSON { currency, total }, где total = price * qty * (100 - DiscountPercent) / 100 (целые числа).

        // TODO 4: GET /raw?key=Shop:Name - значение ключа из IConfiguration как текст или 404, если ключа нет.

        // TODO 5: GET /env - JSON { environment, isDevelopment } из IHostEnvironment.

        // TODO 6: GET /dev/config (текст "dev tools") регистрируйте только в среде Development.
    }
}
