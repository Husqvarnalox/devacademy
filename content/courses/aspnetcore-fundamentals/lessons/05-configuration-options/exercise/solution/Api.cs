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
        builder.Services.AddOptions<ShopOptions>()
            .Bind(builder.Configuration.GetSection("Shop"))
            .ValidateDataAnnotations()
            .Validate(o => o.Currency.Length == 3, "Currency must be a 3-letter code")
            .ValidateOnStart();
    }

    public static void Map(WebApplication app)
    {
        app.MapGet("/ping", () => "pong");

        app.MapGet("/shop", (IOptions<ShopOptions> options) =>
        {
            ShopOptions shop = options.Value;
            return new { name = shop.Name, currency = shop.Currency, maxItems = shop.MaxItems, discountPercent = shop.DiscountPercent };
        });

        app.MapGet("/quote", (int price, int qty, IOptions<ShopOptions> options) =>
        {
            ShopOptions shop = options.Value;
            if (qty > shop.MaxItems) return Results.BadRequest(new { error = "too many items", maxItems = shop.MaxItems });
            return Results.Ok(new { currency = shop.Currency, total = price * qty * (100 - shop.DiscountPercent) / 100 });
        });

        app.MapGet("/raw", (string key, IConfiguration configuration) =>
            configuration[key] is { } value ? Results.Text(value) : Results.NotFound());

        app.MapGet("/env", (IHostEnvironment env) => new { environment = env.EnvironmentName, isDevelopment = env.IsDevelopment() });

        if (app.Environment.IsDevelopment())
            app.MapGet("/dev/config", () => "dev tools");
    }
}
