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

        // TODO 1: подберите время жизни. AppCache - один на приложение, RequestContext и Auditor - один на запрос,
        // Worker - новый при каждом запросе экземпляра. Сейчас все четыре зарегистрированы как Transient.
        services.AddTransient<AppCache>();
        services.AddTransient<RequestContext>();
        services.AddTransient<Worker>();
        services.AddTransient<Auditor>();

        // TODO 2: зарегистрируйте NotEmptyRule, MaxLengthRule и NoDigitsRule как IRule (именно в таком порядке).

        // TODO 3: INotifier по ключам "email" (EmailNotifier) и "sms" (SmsNotifier).
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

        // TODO 4: примите IEnumerable<IRule> и верните сообщения всех сработавших правил (Check вернул не null).
        app.MapGet("/check", (string text) => Array.Empty<string>());

        // TODO 5: /alert - сервис с ключом "sms" через [FromKeyedServices("sms")], ответ notifier.Send("duty").
        app.MapGet("/alert", () => "not implemented");

        // TODO 6: /notify/{channel}?to=... - сервис с ключом из маршрута (IServiceProvider.GetKeyedService<INotifier>):
        // text/plain notifier.Send(to) или 404, если такого ключа нет.
        app.MapGet("/notify/{channel}", (string channel, string to) => Results.NotFound());
    }
}
