using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

// ---- Сервисы. Каждый получает порядковый номер при создании и сообщает об освобождении. ----

public sealed class AppCache : IDisposable
{
    static int next;
    public int Id { get; } = Interlocked.Increment(ref next);
    public void Dispose() => Api.Events.Add($"AppCache#{Id}");
}

public sealed class RequestContext : IDisposable
{
    static int next;
    public int Id { get; } = Interlocked.Increment(ref next);
    public void Dispose() => Api.Events.Add($"RequestContext#{Id}");
}

public sealed class Worker : IDisposable
{
    static int next;
    public int Id { get; } = Interlocked.Increment(ref next);
    public void Dispose() => Api.Events.Add($"Worker#{Id}");
}

// Зависит от RequestContext и Worker: их время жизни определяет, какие экземпляры он получит.
public sealed class Auditor(RequestContext context, Worker worker) : IDisposable
{
    static int next;
    public int Id { get; } = Interlocked.Increment(ref next);
    public RequestContext Context { get; } = context;
    public Worker Worker { get; } = worker;
    public void Dispose() => Api.Events.Add($"Auditor#{Id}");
}

public interface IRule
{
    string? Check(string text);
}

public sealed class NotEmptyRule : IRule
{
    public string? Check(string text) => text.Length == 0 ? "empty" : null;
}

public sealed class MaxLengthRule : IRule
{
    public string? Check(string text) => text.Length > 10 ? "too long" : null;
}

public sealed class NoDigitsRule : IRule
{
    public string? Check(string text) => text.Any(char.IsDigit) ? "has digits" : null;
}

public interface INotifier
{
    string Send(string to);
}

public sealed class EmailNotifier : INotifier
{
    public string Send(string to) => $"email to {to}";
}

public sealed class SmsNotifier : INotifier
{
    public string Send(string to) => $"sms to {to}";
}

public static class Api
{
    // Порядок освобождения сервисов, который печатает драйвер после каждого ответа.
    public static readonly List<string> Events = new();

    public static void Configure(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddSingleton<AppCache>();
        services.AddScoped<RequestContext>();
        services.AddTransient<Worker>();
        services.AddScoped<Auditor>();

        services.AddSingleton<IRule, NotEmptyRule>();
        services.AddSingleton<IRule, MaxLengthRule>();
        services.AddSingleton<IRule, NoDigitsRule>();

        services.AddKeyedSingleton<INotifier, EmailNotifier>("email");
        services.AddKeyedSingleton<INotifier, SmsNotifier>("sms");
    }

    public static void Map(WebApplication app)
    {
        app.MapGet("/state", (AppCache cache, RequestContext context, Worker worker, Auditor auditor) => new
        {
            cache = cache.Id,
            context = context.Id,
            worker = worker.Id,
            auditorContext = auditor.Context.Id,
            auditorWorker = auditor.Worker.Id,
        });

        // Две ручные области: у каждой свой RequestContext, а после Dispose области он освобождается.
        app.MapGet("/scopes", (IServiceScopeFactory factory) =>
        {
            using var first = factory.CreateScope();
            using var second = factory.CreateScope();
            return new[]
            {
                first.ServiceProvider.GetRequiredService<RequestContext>().Id,
                first.ServiceProvider.GetRequiredService<RequestContext>().Id,
                second.ServiceProvider.GetRequiredService<RequestContext>().Id,
            };
        });

        // Все регистрации IRule приходят списком в порядке регистрации.
        app.MapGet("/check", (string text, IEnumerable<IRule> rules) =>
            rules.Select(r => r.Check(text)).OfType<string>().ToArray());

        // Ключ известен заранее: [FromKeyedServices]; ключ приходит из маршрута: поиск в IServiceProvider.
        app.MapGet("/alert", ([FromKeyedServices("sms")] INotifier notifier) => notifier.Send("duty"));

        app.MapGet("/notify/{channel}", (string channel, string to, IServiceProvider services) =>
            services.GetKeyedService<INotifier>(channel) is { } notifier
                ? Results.Text(notifier.Send(to))
                : Results.NotFound());
    }
}
