using DevAcademy.Domain;

namespace DevAcademy.Execution;

public sealed record PackageManagerInfo(string Name, string? Version, string? ExecutablePath)
{
    public bool IsInstalled => Version is not null;
}

/// <summary>Detection only: reports which JavaScript package managers exist. It never installs packages or runs project scripts.</summary>
public static class PackageManagerDetector
{
    private static readonly string[] Names = ["npm", "pnpm", "yarn"];

    public static async Task<IReadOnlyList<PackageManagerInfo>> DetectAsync(CancellationToken ct) =>
        await Task.WhenAll(Names.Select(async n =>
        {
            string? path = ToolLocator.Find(n);
            string? v = path is null ? null : await ToolLocator.ProbeAsync(path, ["--version"], ct).ConfigureAwait(false);
            return new PackageManagerInfo(n, v, v is null ? null : path);
        })).ConfigureAwait(false);

    public static LocalizedText Describe(IEnumerable<PackageManagerInfo> managers) =>
        EngineMessages.PackageManagers(managers.Select(m => m.IsInstalled ? LocalizedText.Literal($"{m.Name} {m.Version}") : EngineMessages.PackageManagerMissing(m.Name)));
}
