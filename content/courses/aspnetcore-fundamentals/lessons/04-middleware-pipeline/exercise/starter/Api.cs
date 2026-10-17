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

    // TODO 2: возьмите идентификатор из заголовка запроса X-Correlation-Id или, если его нет, ids.Next();
    // запишите его в заголовок ответа X-Correlation-Id ДО вызова next; добавьте в Api.Log "correlation:before"
    // до next и "correlation:after" после него.
    public async Task InvokeAsync(HttpContext context, IdGenerator ids)
    {
        await next(context);
    }
}

public sealed class ApiKeyMiddleware(RequestDelegate next)
{
    // TODO 3: если заголовок X-Api-Key не равен "secret" - добавьте в Api.Log "apikey:rejected", ответьте 401
    // с текстом "unauthorized" и НЕ вызывайте next; иначе добавьте "apikey:ok" и передайте запрос дальше.
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);
    }
}

public static class Api
{
    // Порядок событий: драйвер печатает его после каждого ответа.
    public static readonly List<string> Log = new();

    public static void Configure(WebApplicationBuilder builder)
    {
        // TODO 1: зарегистрируйте IdGenerator как singleton (он нужен CorrelationIdMiddleware).
    }

    public static void Map(WebApplication app)
    {
        // TODO 4: соберите конвейер в правильном порядке (всё это - до конечных точек):
        //   1) app.UseMiddleware<CorrelationIdMiddleware>() - самый внешний слой;
        //   2) app.Use(...) с context.Response.OnStarting(...), который добавляет заголовок ответа X-Handled-By: api;
        //   3) app.UseWhen(путь начинается с /admin, ветка с UseMiddleware<ApiKeyMiddleware>()).

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
