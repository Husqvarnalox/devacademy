using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Application.Services;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>One competency of a track's route with its evidence and the way into its check.</summary>
public sealed partial class CompetencyRowViewModel : ObservableObject
{
    private readonly INavigator _nav;
    private readonly string _checkId;
    private readonly string? _gapLessonId;

    public CompetencyRowViewModel(CompetencyStatus status, bool isNext, INavigator nav)
    {
        _nav = nav;
        _checkId = status.Check.Id;
        _gapLessonId = status.Gaps.FirstOrDefault()?.Id;
        State = status.State;
        Title = status.Competency.Title;
        Description = status.Competency.Description;
        IsNext = isNext;
        var total = status.Covered.Count;
        StateText = Loc.T(State switch
        {
            CompetencyState.Confirmed => "Diagnostic_StateConfirmed",
            CompetencyState.NeedsWork => "Diagnostic_StateNeedsWork",
            _ => "Diagnostic_StateNotChecked",
        });
        switch (State)
        {
            case CompetencyState.Confirmed:
                EvidenceText = Loc.T(status.CheckPassed ? "Diagnostic_EvidencePassed" : "Diagnostic_EvidenceAllLessons");
                break;
            case CompetencyState.NeedsWork:
                var parts = new List<string>();
                if (status.QuizFailed) parts.Add(Loc.T("Diagnostic_QuizFailed"));
                if (status.ExerciseFailed) parts.Add(Loc.T("Diagnostic_ExerciseFailed"));
                EvidenceText = string.Join("; ", parts);
                GapText = Loc.F("Diagnostic_Gap", status.Gaps.Count, total);
                break;
            default:
                EvidenceText = Loc.F("Diagnostic_Covered", status.CoveredCompleted, total);
                if (status.WaitingFor.Count > 0)
                    GapText = Loc.F("Diagnostic_WaitingFor", string.Join(", ", status.WaitingFor.Select(c => c.Title)));
                break;
        }
        PrimaryText = Loc.T(State switch
        {
            CompetencyState.Confirmed => "Diagnostic_Open",
            CompetencyState.NeedsWork => "Diagnostic_Retake",
            _ => "Diagnostic_Take",
        });
    }

    public CompetencyState State { get; }
    public string Title { get; }
    public string Description { get; }
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public string StateText { get; }
    public string EvidenceText { get; } = "";
    public bool HasEvidence => EvidenceText.Length > 0;
    public string GapText { get; } = "";
    public bool HasGapText => GapText.Length > 0;
    public bool IsNext { get; }
    public string NextText { get; } = Loc.T("Diagnostic_Next");
    public bool IsConfirmed => State == CompetencyState.Confirmed;
    public bool IsNeedsWork => State == CompetencyState.NeedsWork;
    public bool IsNotChecked => State == CompetencyState.NotChecked;
    public string PrimaryText { get; }
    /// <summary>The check is the main action unless the competency is already confirmed (then it is quiet).</summary>
    public bool IsPrimaryAction => State != CompetencyState.Confirmed;
    public bool HasGapLesson => State == CompetencyState.NeedsWork && _gapLessonId is not null;
    public string StudyGapText { get; } = Loc.T("Diagnostic_StudyGap");

    [RelayCommand]
    private Task OpenCheckAsync() => _nav.OpenLessonAsync(_checkId);

    [RelayCommand]
    private Task StudyGapAsync() => _gapLessonId is null ? Task.CompletedTask : _nav.OpenLessonAsync(_gapLessonId);
}

/// <summary>"Проверка знаний": per-competency evidence for one track and the entry into each check.</summary>
public sealed partial class DiagnosticViewModel(AppServices s, INavigator nav, string trackId) : ObservableObject
{
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _summaryText = "";
    [ObservableProperty] private IReadOnlyList<CompetencyRowViewModel> _rows = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasRows))] private bool _loaded;

    public string TrackId { get; } = trackId;
    public bool HasRows => Rows.Count > 0;
    public string Intro { get; } = Loc.T("Diagnostic_Intro");
    public string IntroNote { get; } = Loc.T("Diagnostic_IntroNote");

    public async Task LoadAsync()
    {
        var report = await s.Diagnostics.GetReportAsync(TrackId);
        if (report is null)
        {
            Rows = [];
            Loaded = true;
            return;
        }
        Title = Loc.F("Diagnostic_Title", report.Track.Title);
        SummaryText = Loc.F("Diagnostic_Summary", report.ConfirmedCount, report.NeedsWorkCount, report.NotCheckedCount);
        var next = report.NextCheck?.Competency.Id;
        Rows = report.Competencies.Select(c => new CompetencyRowViewModel(c, c.Competency.Id == next, nav)).ToList();
        OnPropertyChanged(nameof(HasRows));
        Loaded = true;
    }

    [RelayCommand]
    private Task BackAsync() => nav.ShowDashboardAsync();
}
