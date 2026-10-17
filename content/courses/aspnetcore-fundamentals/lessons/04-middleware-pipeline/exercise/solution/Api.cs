using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

// Детерминированный генератор идентификаторов: req-1, req-2, ...
public sealed class IdGenerator
{
    int next;
    public string Next() => $"req-{Interlocked.Increment(ref next)}";
}

// Convention-based middleware: конструктор получает RequestDelegate (и сервисы-singleton),
// а InvokeAsync - HttpContext и любые сервисы (в том числе scoped) параметрами метода.
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string Header = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context, IdGenerator ids)
    {
        var id = context.Request.Headers.TryGetValue(Header, out var incoming) && incoming.Count > 0
            ? incoming.ToString()
            : ids.Next();
        context.Response.Headers[Header] = id;
        Api.Log.Add("correlation:before");
        await next(context);
        Api.Log.Add("correlation:after");
    }
}

public sealed class ApiKeyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers["X-Api-Key"] != "secret")
        {
            Api.Log.Add("apikey:rejected");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("unauthorized");
            return; // next не вызываем: конвейер дальше не идёт
        }
        Api.Log.Add("apikey:ok");
        await next(context);
    }
}

public static class Api
{
    // Порядок событий: драйвер печатает его после каждого ответа.
    public static readonly List<string> Log = new();

    public static void Configure(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IdGenerator>();
    }

    public static void Map(WebApplication app)
    {
        // Самый внешний слой: идентификатор запроса и его "до/после".
        app.UseMiddleware<CorrelationIdMiddleware>();

        // Заголовок ответа нельзя менять после начала тела, поэтому его добавляет OnStarting.
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["X-Handled-By"] = "api";
                return Task.CompletedTask;
            });
            await next();
        });

        // Ветка только для путей /admin: проверка ключа выполняется до конечной точки.
        app.UseWhen(
            context => context.Request.Path.StartsWithSegments("/admin"),
            branch => branch.UseMiddleware<ApiKeyMiddleware>());

        // Терминальная ветка: Run ничего не передаёт дальше.
        app.Map("/health", branch => branch.Run(context =>
        {
            Api.Log.Add("health");
            return context.Response.WriteAsync("ok");
        }));

        app.MapGet("/hello", () =>
        {
            Api.Log.Add("handler:hello");
            return "hello";
        });

        var admin = app.MapGroup("/admin");
        admin.MapGet("/stats", () =>
        {
            Api.Log.Add("handler:stats");
            return new { requests = 42 };
        });
    }
}
