using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;
using DevAcademy.Execution;

namespace DevAcademy.Desktop.ViewModels;

public sealed record CapabilityRow(AcceleratorCapability Capability)
{
    public string Title => Capability.Title;
    public string Status => Loc.T("Ml_Status_" + Capability.Status);
    public string Message => Capability.Message;
    public bool IsAvailable => Capability.IsAvailable;
}

/// <summary>Settings → Runtimes → "ML-ускорение": hardware report, and framework probes inside a chosen environment on demand.</summary>
public sealed partial class MlAccelerationViewModel : ObservableObject
{
    private readonly AppServices _s;

    public MlAccelerationViewModel(AppServices s) => _s = s;

    public ObservableCollection<CapabilityRow> Hardware { get; } = [];
    public ObservableCollection<CapabilityRow> Frameworks { get; } = [];
    public ObservableCollection<EnvironmentRow> Environments { get; } = [];

    [ObservableProperty] private string _systemText = "";
    [ObservableProperty] private bool _isDetecting;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ProbeCommand))] private bool _isProbing;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ProbeCommand))] private EnvironmentRow? _selectedEnvironment;
    [ObservableProperty] private string? _probeMessage;

    public bool HasFrameworks => Frameworks.Count > 0;
    public bool HasEnvironments => Environments.Count > 0;

    public async Task LoadAsync()
    {
        await RefreshEnvironmentsAsync();
        await DetectAsync();
    }

    public async Task RefreshEnvironmentsAsync()
    {
        string? keep = SelectedEnvironment?.Name;
        string? active = null;
        try { active = await _s.Python.GetActiveNameAsync(CancellationToken.None); }
        catch (Exception ex) when (ex is not OperationCanceledException) { }
        Environments.Clear();
        foreach (var e in _s.Python.ListEnvironments()) Environments.Add(new EnvironmentRow(e, e.Name == active));
        SelectedEnvironment = Environments.FirstOrDefault(e => e.Name == keep) ?? Environments.FirstOrDefault(e => e.IsActive) ?? Environments.FirstOrDefault();
        OnPropertyChanged(nameof(HasEnvironments));
    }

    [RelayCommand]
    private async Task DetectAsync()
    {
        IsDetecting = true;
        try
        {
            var hw = await _s.Python.GetHardwareAsync(refresh: true, CancellationToken.None);
            SystemText = Loc.F("Ml_System", hw.Os, hw.Architecture, hw.CpuModel, hw.CpuCores, hw.RamGiB.ToString("0.#", System.Globalization.CultureInfo.CurrentCulture));
            Hardware.Clear();
            foreach (var c in hw.Capabilities) Hardware.Add(new CapabilityRow(c));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SystemText = Loc.F("Py_Error", ex.Message);
        }
        finally { IsDetecting = false; }
    }

    private bool CanProbe() => !IsProbing && SelectedEnvironment is not null;

    /// <summary>"Проверить в окружении…": imports torch / tensorflow / jax / onnxruntime in the chosen environment, only those it contains.</summary>
    [RelayCommand(CanExecute = nameof(CanProbe))]
    private async Task ProbeAsync()
    {
        var env = SelectedEnvironment!;
        IsProbing = true;
        ProbeMessage = Loc.F("Ml_Probing", env.Name);
        try
        {
            var caps = await _s.Python.ProbeFrameworksAsync(env.Name, refresh: true, CancellationToken.None);
            Frameworks.Clear();
            foreach (var c in caps) Frameworks.Add(new CapabilityRow(c));
            OnPropertyChanged(nameof(HasFrameworks));
            ProbeMessage = caps.All(c => c.Status == CapabilityStatus.NotInstalled) ? Loc.F("Ml_NoFrameworks", env.Name) : Loc.F("Ml_ProbeDone", env.Name);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ProbeMessage = Loc.F("Py_Error", ex.Message);
        }
        finally { IsProbing = false; }
    }
}
