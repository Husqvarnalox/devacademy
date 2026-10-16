using System.Reflection;
using System.Runtime.InteropServices;

namespace DevAcademy.Desktop.Services;

/// <summary>Build and runtime facts shown in Settings → About. Version comes from Directory.Build.props.</summary>
public sealed record AppInfo(string Version, string? Commit, string Runtime, string Platform)
{
    public static AppInfo Current { get; } = FromAssembly(typeof(AppInfo).Assembly);

    public static AppInfo FromAssembly(Assembly assembly)
    {
        var (version, commit) = ParseInformationalVersion(
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
        return new AppInfo(version, commit, RuntimeInformation.FrameworkDescription,
            $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()})");
    }

    /// <summary>Splits "0.1.0+abc1234" into ("0.1.0", "abc1234"); a missing value yields ("0.0.0", null).</summary>
    public static (string Version, string? Commit) ParseInformationalVersion(string? informational)
    {
        if (string.IsNullOrWhiteSpace(informational)) return ("0.0.0", null);
        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        if (plus < 0) return (informational, null);
        var commit = informational[(plus + 1)..];
        return (informational[..plus], commit.Length == 0 ? null : commit);
    }

    /// <summary>The shipped THIRD-PARTY-NOTICES.md (next to the content directory or the executable), or null when absent.</summary>
    public static string? FindNoticesFile(string? contentRoot)
    {
        var dirs = new List<string>();
        if (contentRoot is not null && Path.GetDirectoryName(contentRoot.TrimEnd(Path.DirectorySeparatorChar)) is { } parent) dirs.Add(parent);
        dirs.Add(AppContext.BaseDirectory);
        return dirs.Select(d => Path.Combine(d, "THIRD-PARTY-NOTICES.md")).FirstOrDefault(File.Exists);
    }
}
