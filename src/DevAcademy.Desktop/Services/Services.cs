using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using DevAcademy.Desktop.Localization;

namespace DevAcademy.Desktop.Services;

public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message, string confirmText, string? cancelText = null);
    Task ShowMessageAsync(string title, string message);
    /// <summary>Asks for one line of text; null when cancelled.</summary>
    Task<string?> PromptAsync(string title, string message, string initial, string confirmText);
    /// <summary>Shows a message with several buttons; returns the index of the pressed one, or -1 when dismissed.</summary>
    Task<int> ChooseAsync(string title, string message, IReadOnlyList<string> options);
}

public interface IFilePickerService
{
    Task<string?> PickSaveAsync(string title, string suggestedName, string extension);
    Task<string?> PickOpenAsync(string title, string extension);
    Task<string?> PickFolderAsync(string title);
}

public interface ILauncherService
{
    Task<bool> OpenUrlAsync(string url);
}

public enum ThemeChoice { System, Light, Dark }

public interface IThemeService
{
    void Apply(ThemeChoice choice);
}

/// <summary>Holds the window that owns dialogs and pickers (set by MainWindow).</summary>
public sealed class WindowHolder
{
    public TopLevel? Window { get; set; }
}

public sealed class AvaloniaDialogService(WindowHolder holder) : IDialogService
{
    public async Task<bool> ConfirmAsync(string title, string message, string confirmText, string? cancelText = null)
    {
        if (holder.Window is not Window owner) return false;
        var dialog = new Views.ConfirmDialog(title, message, confirmText, cancelText ?? Loc.T("Common_Cancel"), showCancel: true);
        return await dialog.ShowDialog<bool>(owner);
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        if (holder.Window is not Window owner) return;
        var dialog = new Views.ConfirmDialog(title, message, Loc.T("Common_Ok"), null, showCancel: false);
        await dialog.ShowDialog<bool>(owner);
    }

    public async Task<string?> PromptAsync(string title, string message, string initial, string confirmText)
    {
        if (holder.Window is not Window owner) return null;
        var dialog = new Views.PromptDialog(title, message, initial, confirmText, Loc.T("Common_Cancel"));
        return await dialog.ShowDialog<string?>(owner);
    }

    public async Task<int> ChooseAsync(string title, string message, IReadOnlyList<string> options)
    {
        if (holder.Window is not Window owner) return -1;
        var dialog = new Views.ChoiceDialog(title, message, options);
        return await dialog.ShowDialog<int>(owner);
    }
}

public sealed class AvaloniaFilePickerService(WindowHolder holder) : IFilePickerService
{
    public async Task<string?> PickSaveAsync(string title, string suggestedName, string extension)
    {
        if (holder.Window?.StorageProvider is not { } sp) return null;
        var file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            FileTypeChoices = [new FilePickerFileType(extension) { Patterns = [$"*.{extension}"] }],
        });
        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickOpenAsync(string title, string extension)
    {
        if (holder.Window?.StorageProvider is not { } sp) return null;
        var files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(extension) { Patterns = [$"*.{extension}"] }],
        });
        return files.Count == 1 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        if (holder.Window?.StorageProvider is not { } sp) return null;
        var folders = await sp.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return folders.Count == 1 ? folders[0].TryGetLocalPath() : null;
    }
}

public sealed class AvaloniaLauncherService(WindowHolder holder) : ILauncherService
{
    public async Task<bool> OpenUrlAsync(string url)
    {
        // Only web links from content are opened externally; never file: or custom schemes.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return false;
        if (holder.Window?.Launcher is not { } launcher) return false;
        return await launcher.LaunchUriAsync(uri);
    }
}

public sealed class AvaloniaThemeService : IThemeService
{
    public void Apply(ThemeChoice choice)
    {
        if (Avalonia.Application.Current is not { } app) return;
        app.RequestedThemeVariant = choice switch
        {
            ThemeChoice.Light => ThemeVariant.Light,
            ThemeChoice.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
