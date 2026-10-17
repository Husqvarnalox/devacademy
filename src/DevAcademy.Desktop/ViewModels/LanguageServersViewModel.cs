using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;
using DevAcademy.Desktop.Services;
using DevAcademy.LanguageServices.Hosting;
using DevAcademy.LanguageServices.Servers;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>One language-server row of Settings: detected state, an optional override command, and an install button for npm-based servers.</summary>
public sealed partial class LanguageServerRowViewModel : ObservableObject
{
    private readonly LanguageServersViewModel _owner;

    public LanguageServerRowViewModel(LanguageServersViewModel owner, ServerInventoryItem item)
    {
        _owner = owner;
        Definition = item.Definition;
        Language = string.Join(", ", item.Definition.Languages.Select(Display.LanguageName));
        Server = item.Definition.DisplayName;
        State = Loc.T("Lsp_State_" + item.State);
        Path = item.ExecutablePath ?? "";
        Version = item.Version ?? "";
        Message = item.State == ServerState.NotInstalled ? Loc.Of(item.Definition.InstallHintText) : Loc.Of(item.MessageText, item.Message);
        OverrideCommand = item.OverrideCommand ?? "";
        CanInstall = item.State == ServerState.NotInstalled && item.Definition.IsNpmInstallable;
        PrimaryLanguage = item.Definition.Languages[0];
    }

    public LanguageServerDefinition Definition { get; }
    public string PrimaryLanguage { get; }
    public string Language { get; }
    public string Server { get; }
    public string State { get; }
    public string Path { get; }
    public string Version { get; }
    public string Message { get; }
    public bool CanInstall { get; }
    [ObservableProperty] private string _overrideCommand;

    [RelayCommand] private Task SaveOverrideAsync() => _owner.SaveOverrideAsync(this);
    [RelayCommand] private Task InstallAsync() => _owner.InstallAsync(this);
}

public sealed partial class LanguageServersViewModel : ObservableObject
{
    private readonly AppServices _s;

    public LanguageServersViewModel(AppServices s) => _s = s;

    public ObservableCollection<LanguageServerRowViewModel> Rows { get; } = [];
    [ObservableProperty] private bool _enabled = true;
    private bool _loaded;

    public Task LoadAsync()
    {
        _loaded = false;
        Enabled = _s.Language.Enabled;
        _loaded = true;
        Refresh();
        return Task.CompletedTask;
    }

    partial void OnEnabledChanged(bool value)
    {
        if (!_loaded) return;
        _ = SetEnabledAsync(value);
    }

    private async Task SetEnabledAsync(bool value)
    {
        await _s.Language.SetEnabledAsync(value);
        Refresh();
    }

    [RelayCommand]
    public void Refresh()
    {
        Rows.Clear();
        foreach (var item in _s.Language.Manager.Inventory()) Rows.Add(new LanguageServerRowViewModel(this, item));
    }

    internal async Task SaveOverrideAsync(LanguageServerRowViewModel row)
    {
        foreach (var lang in row.Definition.Languages)
            await _s.Language.SetOverrideAsync(lang, row.OverrideCommand);
        Refresh();
    }

    internal async Task InstallAsync(LanguageServerRowViewModel row)
    {
        await LanguageServerInstaller.OfferAsync(_s, row.Definition);
        Refresh();
    }
}
