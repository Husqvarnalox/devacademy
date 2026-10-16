using Avalonia.Threading;
using DevAcademy.Application.Ports;
using DevAcademy.Infrastructure;
using DevAcademy.LanguageServices.Hosting;
using DevAcademy.LanguageServices.Servers;
using DevAcademy.LanguageServices.Workspace;

namespace DevAcademy.Desktop.Services;

/// <summary>
/// The app-facing side of the language-server layer: owns the <see cref="LanguageServerManager"/>, the user's settings
/// (switch + per-language override command) and the per-exercise workspace mirrors. Servers are only ever started on demand
/// by an opened editor; installing one (npm) happens only through <see cref="InstallAsync"/>, which a user action calls.
/// </summary>
public sealed class LanguageIntelligenceService : IDisposable
{
    public const string EnabledKey = "lsp.enabled";
    public const string CommandKeyPrefix = "lsp.command.";

    private readonly ISettingsRepository _settings;
    private readonly string _cacheDir;
    private readonly string _mirrorBase;
    private readonly Dictionary<string, string> _overrides = new(StringComparer.Ordinal);
    private readonly Dictionary<(string, string), WorkspaceMirror> _mirrors = [];
    private volatile bool _enabled = true;

    public LanguageIntelligenceService(AppPaths paths, DevAcademy.Execution.ExecutionOptions execution, ISettingsRepository settings,
        IServerLauncher? launcher = null, ServerLocator? locator = null, IReadOnlyList<LanguageServerDefinition>? definitions = null)
    {
        _settings = settings;
        _cacheDir = execution.CacheDirectory;
        _mirrorBase = Path.Combine(paths.DataDir, "lsp-workspaces");
        LoadSettings();
        var options = new LanguageServerManagerOptions
        {
            Definitions = definitions ?? LanguageServerRegistry.Defaults,
            Locator = locator ?? new ServerLocator(_cacheDir),
            Launcher = launcher ?? new ProcessServerLauncher(),
            OverrideFor = lang => _overrides.GetValueOrDefault(lang),
            IsEnabled = () => _enabled,
            // Text is read on the UI thread; posting (not blocking) keeps the server's reader loop free of UI deadlocks.
            Invoke = a => { if (Dispatcher.UIThread.CheckAccess()) a(); else Dispatcher.UIThread.Post(a); },
        };
        Manager = new LanguageServerManager(options);
    }

    public LanguageServerManager Manager { get; }
    public bool Enabled => _enabled;
    public string MirrorBaseDirectory => _mirrorBase;

    private void LoadSettings()
    {
        try
        {
            var all = _settings.GetAllAsync().GetAwaiter().GetResult();
            _enabled = !(all.TryGetValue(EnabledKey, out var e) && e == "false");
            foreach (var (k, v) in all)
                if (k.StartsWith(CommandKeyPrefix, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(v))
                    _overrides[k[CommandKeyPrefix.Length..]] = v;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LSP settings unavailable: {ex.Message}"); }
    }

    public async Task SetEnabledAsync(bool enabled)
    {
        _enabled = enabled;
        await _settings.SetAsync(EnabledKey, enabled ? "true" : "false");
        if (!enabled) await Manager.StopAllAsync();
    }

    /// <summary>Sets (or with null/blank clears) the command used for a language instead of the detected server.</summary>
    public async Task SetOverrideAsync(string languageId, string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) _overrides.Remove(languageId); else _overrides[languageId] = command.Trim();
        await _settings.SetAsync(CommandKeyPrefix + languageId, command?.Trim() ?? "");
    }

    public string? OverrideFor(string languageId) => _overrides.GetValueOrDefault(languageId);

    /// <summary>The stable mirror directory for a lesson exercise and language (one instance per pair; shared by tabs).</summary>
    public WorkspaceMirror MirrorFor(string exerciseId, string languageId)
    {
        lock (_mirrors)
        {
            if (!_mirrors.TryGetValue((exerciseId, languageId), out var m))
                _mirrors[(exerciseId, languageId)] = m = new WorkspaceMirror(_mirrorBase, exerciseId, languageId);
            return m;
        }
    }

    /// <summary>The exact install command for a missing npm-based server, or null when it cannot be installed that way. Runs nothing.</summary>
    public ProvisionPlan? PlanInstall(LanguageServerDefinition def) => NpmServerProvisioner.Plan(def, _cacheDir, Manager.Locator);

    /// <summary>Runs a plan previously shown to the user and confirmed by them.</summary>
    public Task<ServerProvisionResult> InstallAsync(LanguageServerDefinition def, ProvisionPlan plan, CancellationToken ct = default) =>
        NpmServerProvisioner.ProvisionAsync(plan, Manager.Locator, () => Manager.Locator.Resolve(def, null) is not null, ct);

    public void Dispose()
    {
        try { Manager.StopAllAsync().Wait(TimeSpan.FromSeconds(8)); } catch { /* shutting down */ }
        lock (_mirrors)
            foreach (var m in _mirrors.Values) try { m.FlushAsync().Wait(TimeSpan.FromSeconds(2)); } catch { }
    }
}
