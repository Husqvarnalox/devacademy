using System.Security.Cryptography;
using DevAcademy.Domain;
using DevAcademy.Execution;

namespace DevAcademy.Application.Maven;

/// <summary>
/// What may happen to an exercise run given the Maven content environment. <see cref="BlockedMessage"/> non-null means "do not run";
/// <see cref="InstallSet"/> is the package set whose (explicit, confirmed) installation would unblock it.
/// </summary>
public sealed record MavenRunPlan(MavenRunSelection? Maven, string? BlockedMessage = null, ContentPackageSet? InstallSet = null)
{
    public bool IsBlocked => BlockedMessage is not null;
    public static MavenRunPlan Default { get; } = new((MavenRunSelection?)null);
}

/// <summary>Decides whether an exercise that needs Maven jars (Spring Boot...) can run. Reads only; never downloads.</summary>
public interface IMavenEnvironmentResolver
{
    Task<MavenRunPlan> ResolveAsync(Exercise exercise, CancellationToken ct);
}

public sealed record MavenArtifactInfo(string Coordinates, string? Version);

/// <summary>One package set in the Maven environment root: installed or not, with the artifacts it offers and the size of its download.</summary>
public sealed record MavenSetStatus(ContentPackageSet Set, string Path, bool Installed, IReadOnlyList<MavenArtifactInfo> Packages, int JarCount, long TotalBytes, bool HasLock);

public sealed record MavenEnvironmentStatus(string Root, IReadOnlyList<MavenSetStatus> Sets);

/// <summary>What an explicit install of ONE set downloads: every jar of its lock file. Shown to the learner (count, size, host) before anything runs.</summary>
public sealed record MavenInstallPlan(ContentPackageSet Set, MavenLock Lock, string EnvironmentPath, string LibPath)
{
    public int JarCount => Lock.Jars.Count;
    public long TotalBytes => Lock.TotalBytes;
    public string Host => Lock.Host;
}

public sealed record MavenInstallResult(bool Ok, bool Cancelled, string? Error);

/// <summary><see cref="Problem"/> null = satisfied; <see cref="Set"/> is the set to install (or repair) when the problem is a missing/incomplete set.</summary>
public sealed record MavenCheck(string? Problem, ContentPackageSet? Set = null);

/// <summary>
/// The Maven content environment: a root directory with one subdirectory per package set, each holding a <c>lib</c> folder with the jars that the set's
/// committed <c>lock.json</c> (<c>tools/content-packages/maven/&lt;id&gt;</c>) lists. No Maven tool is involved: installing a set downloads exactly those
/// jars from the lock's source over HTTPS and keeps a file only when its size and sha256 match (a mismatching partial file is deleted). Exercises declare
/// <c>requires.mavenArtifacts</c>; the check picks the ONE set that covers them, and a run puts only that set's <c>lib</c> folder on the class path,
/// read-only (see <see cref="MavenRunSelection"/>). Installing or removing a set is an explicit user action; nothing else ever downloads a jar.
/// </summary>
public sealed class MavenEnvironmentService(HttpClient http, string environmentRoot, ContentPackageIndex? index = null) : IMavenEnvironmentResolver
{
    private const string PartialSuffix = ".part";

    public string EnvironmentRoot { get; } = Path.GetFullPath(environmentRoot);
    public ContentPackageIndex? Index { get; } = index;

    public IReadOnlyList<ContentPackageSet> Sets => Index?.For(PackageSetLanguage.Maven).ToList() ?? [];

    public string SetPath(ContentPackageSet set) => Path.Combine(EnvironmentRoot, set.Id);

    /// <summary>The set's jar folder (<c>lib</c>); the class path of a run is <c>lib/*</c>.</summary>
    public string LibPath(ContentPackageSet set) => Path.Combine(SetPath(set), "lib");

    private MavenLock? ReadLock(ContentPackageSet set) =>
        Index?.SourcePath(set) is { } dir && MavenLock.TryLoad(dir, out var lockFile, out _) ? lockFile : null;

    /// <summary>True when every jar of the set's lock is present with its locked size. (Hashes are checked at download time; a run never trusts anything else in the folder, it only reads it.)</summary>
    public bool IsInstalled(ContentPackageSet set) => ReadLock(set) is { } lockFile && MissingJars(set, lockFile).Count == 0;

    private List<MavenJar> MissingJars(ContentPackageSet set, MavenLock lockFile)
    {
        var lib = LibPath(set);
        var missing = new List<MavenJar>();
        foreach (var jar in lockFile.Jars)
        {
            var info = new FileInfo(Path.Combine(lib, jar.File));
            try { if (!info.Exists || info.Length != jar.Size) missing.Add(jar); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { missing.Add(jar); }
        }
        return missing;
    }

    /// <summary>A developer checkout's <c>.maven-content</c> (made by <c>make maven-env</c>), found next to <c>tools/content-packages</c>.</summary>
    public static string? FindRepositoryEnvironment(string? startDirectory = null)
    {
        var packages = ContentPackageIndex.FindDirectory(startDirectory);
        if (packages is null) return null;
        var root = Directory.GetParent(Directory.GetParent(packages)?.FullName ?? "")?.FullName;
        var env = root is null ? null : Path.Combine(root, ".maven-content");
        return env is not null && HasInstalledSet(env) ? env : null;
    }

    /// <summary>True when any subdirectory of <paramref name="root"/> holds a <c>lib</c> folder with a jar (some set is installed there).</summary>
    public static bool HasInstalledSet(string root)
    {
        try { return Directory.Exists(root) && Directory.EnumerateDirectories(root).Any(d => HasJars(Path.Combine(d, "lib"))); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    private static bool HasJars(string lib)
    {
        try { return Directory.Exists(lib) && Directory.EnumerateFiles(lib, "*.jar").Any(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    // ---------------------------------------------------------------- inspection

    public Task<MavenEnvironmentStatus> InspectAsync(CancellationToken ct)
    {
        var sets = new List<MavenSetStatus>();
        foreach (var set in Sets)
        {
            var lockFile = ReadLock(set);
            var installed = lockFile is not null && MissingJars(set, lockFile).Count == 0;
            var versions = lockFile?.Jars.ToDictionary(j => j.Coordinates, j => j.Version, StringComparer.Ordinal) ?? [];
            var packages = set.Packages.Select(name => new MavenArtifactInfo(name, versions.GetValueOrDefault(name))).ToList();
            sets.Add(new MavenSetStatus(set, SetPath(set), installed, packages, lockFile?.Jars.Count ?? 0, lockFile?.TotalBytes ?? 0, lockFile is not null));
        }
        return Task.FromResult(new MavenEnvironmentStatus(EnvironmentRoot, sets));
    }

    // ---------------------------------------------------------------- requirement check

    private const string Fix = "Установите набор: кнопка в уроке или Настройки → Среды выполнения → Java (Maven) (нужна сеть; приложение ничего не скачивает само; из репозитория: make maven-env).";

    /// <summary>The set that covers <paramref name="artifacts"/>, or a one-line explanation in Russian.</summary>
    public (ContentPackageSet? Set, string? Error) ResolveSet(IReadOnlyCollection<string> artifacts)
    {
        if (Index is not { Sets.Count: > 0 }) return (null, "не найден каталог content-packages с описанием наборов пакетов (он поставляется вместе с приложением и лежит в tools/content-packages репозитория)");
        var resolution = Index.Resolve(PackageSetLanguage.Maven, artifacts);
        return (resolution.Set, resolution.Error);
    }

    /// <summary>Satisfied (<see cref="MavenCheck.Problem"/> null) when the exercise's set is installed completely.</summary>
    public MavenCheck Check(ExerciseRequirements? requires)
    {
        var artifacts = (requires?.Maven ?? []).Distinct(StringComparer.Ordinal).ToList();
        if (artifacts.Count == 0) return new MavenCheck(null);
        var (set, error) = ResolveSet(artifacts);
        if (set is null) return new MavenCheck($"Для упражнения нужны библиотеки Java ({string.Join(", ", artifacts)}), но {error}.");
        if (ReadLock(set) is not { } lockFile)
            return new MavenCheck($"Не найден файл {MavenLock.FileName} набора «{set.Title}» (tools/content-packages/maven/{set.Id}); он поставляется вместе с приложением.", set);
        var missing = MissingJars(set, lockFile);
        if (missing.Count == lockFile.Jars.Count)
            return new MavenCheck($"Для упражнения нужен набор библиотек «{set.Title}» ({lockFile.Jars.Count} jar-файлов), он не установлен ({SetPath(set)}). " + Fix, set);
        return missing.Count > 0
            ? new MavenCheck($"Набор «{set.Title}» установлен не полностью: нет {missing.Count} из {lockFile.Jars.Count} jar-файлов. " + Fix, set)
            : new MavenCheck(null, set);
    }

    public Task<MavenRunPlan> ResolveAsync(Exercise exercise, CancellationToken ct)
    {
        if (!exercise.NeedsMavenEnvironment) return Task.FromResult(MavenRunPlan.Default);
        var check = Check(exercise.Requires);
        if (check.Problem is not null) return Task.FromResult(new MavenRunPlan(null, check.Problem, check.Set));
        return Task.FromResult(check.Set is null ? MavenRunPlan.Default : new MavenRunPlan(new MavenRunSelection(LibPath(check.Set))));
    }

    // ---------------------------------------------------------------- explicit install and removal

    /// <summary>What an install of one set downloads, or an explanation why it cannot be planned. Nothing runs.</summary>
    public (MavenInstallPlan? Plan, string? Error) PlanInstall(ContentPackageSet set)
    {
        if (Index?.SourcePath(set) is not { } dir || !File.Exists(Path.Combine(dir, MavenLock.FileName)))
            return (null, $"Не найден файл content-packages/maven/{set.Id}/{MavenLock.FileName} (он поставляется вместе с приложением и лежит в tools/content-packages репозитория).");
        if (!MavenLock.TryLoad(dir, out var lockFile, out var error))
            return (null, $"Файл {MavenLock.FileName} набора «{set.Title}» повреждён: {error}");
        return (new MavenInstallPlan(set, lockFile, SetPath(set), LibPath(set)), null);
    }

    /// <summary>
    /// Downloads the jars the plan lists, one by one, into the set's <c>lib</c> folder. Each jar goes to <c>name.jar.part</c> first and is renamed only after its size and
    /// sha256 match the lock; a mismatch deletes the partial file and stops the install (jars verified before it stay, so a retry only fetches the rest).
    /// This is the only place that uses the network.
    /// </summary>
    public async Task<MavenInstallResult> InstallAsync(MavenInstallPlan plan, Action<string> onLine, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(plan.LibPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new MavenInstallResult(false, false, $"Не удалось подготовить каталог {plan.LibPath}: {ex.Message}");
        }
        var number = 0;
        foreach (var jar in plan.Lock.Jars)
        {
            number++;
            var target = Path.Combine(plan.LibPath, jar.File);
            var partial = target + PartialSuffix;
            try
            {
                if (await IsVerifiedAsync(target, jar, ct).ConfigureAwait(false))
                {
                    onLine($"[{number}/{plan.JarCount}] {jar.File}: уже есть, хеш совпадает");
                    continue;
                }
                var url = plan.Lock.UrlOf(jar);
                onLine($"[{number}/{plan.JarCount}] {url}");
                var problem = await DownloadAsync(url, partial, jar, ct).ConfigureAwait(false);
                if (problem is not null)
                {
                    TryDelete(partial);
                    return new MavenInstallResult(false, false, problem);
                }
                File.Move(partial, target, overwrite: true);
            }
            catch (OperationCanceledException)
            {
                TryDelete(partial);
                return new MavenInstallResult(false, true, null);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
            {
                TryDelete(partial);
                return new MavenInstallResult(false, false, $"Не удалось скачать {jar.File}: {ex.Message}");
            }
        }
        return new MavenInstallResult(true, false, null);
    }

    private async Task<string?> DownloadAsync(Uri url, string partial, MavenJar jar, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return $"{url}: сервер ответил {(int)response.StatusCode}";
        using var hash = SHA256.Create();
        long written = 0;
        await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var file = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                written += read;
                // The lock says how big the jar is: more bytes than that can only be a wrong file, so stop before it fills the disk.
                if (written > jar.Size) return $"{jar.File}: файл больше, чем записано в lock.json ({jar.Size} байт); загрузка прервана";
                hash.TransformBlock(buffer, 0, read, null, 0);
                await file.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            }
        }
        hash.TransformFinalBlock([], 0, 0);
        if (written != jar.Size) return $"{jar.File}: получено {written} байт вместо {jar.Size}; файл удалён";
        var actual = Convert.ToHexStringLower(hash.Hash!);
        return actual == jar.Sha256 ? null : $"{jar.File}: контрольная сумма sha256 не совпала с lock.json (получено {actual}); файл удалён";
    }

    private static async Task<bool> IsVerifiedAsync(string path, MavenJar jar, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length != jar.Size) return false;
            await using var stream = File.OpenRead(path);
            return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false)) == jar.Sha256;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* nothing more to do for a leftover partial file */ }
    }

    /// <summary>Deletes ONE set's directory (its jars) and nothing else. The caller confirms first.</summary>
    public MavenInstallResult RemoveSet(ContentPackageSet set)
    {
        var path = SetPath(set);
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            return new MavenInstallResult(true, false, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new MavenInstallResult(false, false, $"Не удалось удалить {path}: {ex.Message}");
        }
    }
}
