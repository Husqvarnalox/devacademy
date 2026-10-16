using System.Text.RegularExpressions;
using DevAcademy.Domain;

namespace DevAcademy.Infrastructure.Workspace;

public sealed record TemplateInstallResult(bool Ok, string? Error, string? ProjectPath = null, IReadOnlyList<string>? PostCreate = null);

/// <summary>
/// Copies a template's files into a new directory. This is the only thing it does: postCreate commands are returned for display and are
/// never executed, so creating a project cannot run code.
/// </summary>
public static partial class TemplateInstaller
{
    public const string NamePlaceholder = "{{name}}";

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]{0,63}$")]
    private static partial Regex ProjectName();

    public static string? ValidateProjectName(string? name) =>
        name is not null && ProjectName().IsMatch(name)
            ? null
            : "Имя проекта: латинские буквы, цифры, «-» и «_», начинается с буквы, до 64 символов.";

    public static TemplateInstallResult Instantiate(ProjectTemplate template, string parentDirectory, string name)
    {
        if (ValidateProjectName(name) is { } bad) return new(false, bad);
        if (!Directory.Exists(template.FilesDirectory)) return new(false, "Файлы шаблона не найдены.");
        var target = Path.Combine(Path.GetFullPath(parentDirectory), name);
        if (File.Exists(target) || (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any()))
            return new(false, $"Каталог «{target}» уже существует и не пуст.");
        try
        {
            Directory.CreateDirectory(target);
            Copy(template.FilesDirectory, target, name);
            return new(true, null, target, template.PostCreate);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(false, ex.Message);
        }
    }

    private static void Copy(string source, string target, string name)
    {
        foreach (var dir in Directory.GetDirectories(source))
        {
            var d = Path.Combine(target, Path.GetFileName(dir));
            Directory.CreateDirectory(d);
            Copy(dir, d, name);
        }
        foreach (var file in Directory.GetFiles(source))
        {
            var dest = Path.Combine(target, Path.GetFileName(file));
            var bytes = File.ReadAllBytes(file);
            var decoded = TextFileStore.Decode(bytes);
            if (decoded.Content is { } c && c.Text.Contains(NamePlaceholder, StringComparison.Ordinal))
                bytes = TextFileStore.Encode(c.Text.Replace(NamePlaceholder, name, StringComparison.Ordinal), c.HasBom, c.LineEnding);
            File.WriteAllBytes(dest, bytes);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(dest, File.GetUnixFileMode(file));
        }
    }
}
