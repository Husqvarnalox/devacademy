using System.Reflection;
using System.Text.RegularExpressions;
using DevAcademy.Desktop.Localization;
using DevAcademy.Domain;
using Ai = DevAcademy.AI.EngineMessages;
using Eval = DevAcademy.Application.EngineMessages;
using Dbg = DevAcademy.Debugging.EngineMessages;
using Exe = DevAcademy.Execution.EngineMessages;
using Lsp = DevAcademy.LanguageServices.EngineMessages;
using Nb = DevAcademy.Notebooks.EngineMessages;

namespace DevAcademy.Desktop.Tests;

public class EngineMessageLocalizationTests
{
    private static readonly Type[] Catalogs = [typeof(Exe), typeof(Eval), typeof(Dbg), typeof(Lsp), typeof(Nb), typeof(Ai)];

    private static IEnumerable<(Type Catalog, string Key)> AllKeys() =>
        Catalogs.SelectMany(c => c.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
            .Select(f => (c, (string)f.GetRawConstantValue()!)));

    [Fact]
    public void Every_engine_key_is_in_the_resources()
    {
        var all = AllKeys().ToList();
        Assert.True(all.Count > 100);
        var missing = all.Where(k => !Loc.Has(k.Key)).Select(k => k.Catalog.Namespace + ":" + k.Key).ToList();
        Assert.True(missing.Count == 0, "Missing from Strings.resx: " + string.Join(", ", missing));
    }

    [Fact]
    public void Engine_keys_are_unique_and_namespaced()
    {
        var keys = AllKeys().Select(k => k.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(keys, k => Assert.StartsWith("Engine_", k, StringComparison.Ordinal));
    }

    [Fact]
    public void Translations_are_russian_and_use_the_placeholders_of_the_english_fallback()
    {
        // A representative factory per placeholder count: the resource template must accept the same arguments (no out-of-range {n}).
        var samples = new[]
        {
            Exe.ToolNotFound("go"), Exe.NodeTooOldForTs("20.1.0"), Exe.CommandNotAllowed("a", "b", "c"), Exe.DockerImageMissing("img"),
            Eval.TimeLimitSeconds(20), Eval.CommandFailed("Error", "2"), Dbg.JsDebugWindows("u", "s", "d"), Dbg.NetCoreDbgManual("v", "d", "e"),
            Lsp.Crashed(3, Lsp.ProcessExited()), Nb.KernelConnectionLost("shell", "boom"), Nb.PipFailed(1, "out"), Ai.NetworkError("x"),
        };
        foreach (var s in samples)
        {
            var text = Loc.Of(s);
            Assert.NotEqual(s.Fallback, text);
            Assert.Matches(new Regex("[А-Яа-я]"), text);
            Assert.DoesNotContain("{", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Of_formats_arguments_and_nested_messages()
    {
        var text = Eval.Hinted(Eval.TimeLimit(), "think");
        Assert.Equal("Превышен лимит времени. Подсказка: think", Loc.Of(text));
        Assert.Equal("Превышен лимит времени (20 с)", Loc.Of(Eval.TimeLimitSeconds(20)));
    }

    [Fact]
    public void Of_renders_joined_text_in_order_and_skips_empty_parts()
    {
        var joined = LocalizedText.Join(" ", Exe.CSharpUnsupported(), null, Exe.TsStripping());
        Assert.Equal("Для file-based приложений нужен .NET SDK 10 или новее. Запуск через удаление типов в Node (без enum и namespace).", Loc.Of(joined));
    }

    [Fact]
    public void Of_keeps_verbatim_text_untranslated()
    {
        Assert.Equal("error CS1002: ; expected", Loc.Of(LocalizedText.Literal("error CS1002: ; expected")));
        // Tool output inside a translated message is an argument and stays as is.
        Assert.Equal("Ошибка контента: command.run is empty", Loc.Of(Eval.ContentError("command.run is empty")));
    }

    [Fact]
    public void Of_falls_back_to_english_for_unknown_keys_and_bad_templates()
    {
        var unknown = LocalizedText.Create("Engine_Test_NoSuchKey", "Hello {0}", "world");
        Assert.Equal("Hello world", Loc.Of(unknown));
        Assert.Equal("", Loc.Of(null));
        Assert.Equal("plain", Loc.Of(null, "plain"));
        // "Engine_Eval_Hinted" needs two arguments; with one the format throws and the English text is used.
        var mismatched = new LocalizedText("Engine_Eval_Hinted", "fallback", ["only one"]);
        Assert.Equal("fallback", Loc.Of(mismatched));
    }

    [Fact]
    public void Msg_translates_exceptions_that_carry_a_message()
    {
        var ex = new LocalizedOperationException(Exe.DockerNotFound());
        Assert.Equal("Исполняемый файл docker не найден.", Loc.Msg(ex));
        Assert.Equal("boom", Loc.Msg(new IOException("boom")));
    }

    [Fact]
    public void The_english_fallback_is_what_the_engine_reports_without_a_ui()
    {
        Assert.Equal("Time limit exceeded (20 s)", Eval.TimeLimitSeconds(20).Fallback);
        Assert.Equal("go was not found on PATH.", Exe.ToolNotFound("go").Fallback);
    }
}
