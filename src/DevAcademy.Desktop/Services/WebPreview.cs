namespace DevAcademy.Desktop.Services;

/// <summary>
/// Shows a project's local web app. The IDE itself is never a WebView: previews live behind this abstraction, so a different host
/// (an embedded native webview in a separate window or panel) can replace the system browser without touching the workspace.
/// </summary>
public interface IWebPreview
{
    /// <summary>Short stable name of the implementation, e.g. "system-browser".</summary>
    string Id { get; }

    /// <summary>Opens <paramref name="url"/>. Only http(s) URLs on this machine are accepted. False when it could not be shown.</summary>
    Task<bool> OpenAsync(string url, CancellationToken ct = default);
}

/// <summary>The implemented preview: the user's default browser through the launcher. An embedded native webview is planned, not implemented.</summary>
public sealed class SystemBrowserWebPreview(ILauncherService launcher) : IWebPreview
{
    public string Id => "system-browser";

    public static bool IsLocalWebUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme is "http" or "https"
        && (u.IsLoopback || u.Host is "0.0.0.0" or "[::]");

    public Task<bool> OpenAsync(string url, CancellationToken ct = default) =>
        IsLocalWebUrl(url) ? launcher.OpenUrlAsync(url) : Task.FromResult(false);
}
