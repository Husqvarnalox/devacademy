using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;
using DevAcademy.Domain;

namespace DevAcademy.Desktop.ViewModels;

public sealed partial class ComparisonCardViewModel(Comparison comparison, INavigator nav) : ObservableObject
{
    public string Id { get; } = comparison.Id;
    public string Title { get; } = comparison.Title;
    public string Meta { get; } = Loc.F("Comparison_Meta", comparison.Subjects.Count, comparison.Criteria.Count);
    public string SubjectsText { get; } = string.Join(" / ", comparison.Subjects.Select(s => s.Title));

    [RelayCommand]
    private Task OpenAsync() => nav.OpenComparisonAsync(Id);
}

public sealed class ComparisonSubjectViewModel(ComparisonSubject subject)
{
    public string Id { get; } = subject.Id;
    public string Title { get; } = subject.Title;
    public string Header { get; } = subject.Version is null ? subject.Title : $"{subject.Title} {subject.Version}";
    public string KindText { get; } = Loc.T("Subject_" + subject.Kind);
    public IReadOnlyList<string> Docs { get; } = subject.Docs;
    public override string ToString() => Header;
}

public sealed record ComparisonCellViewModel(string SubjectId, string Markdown);

public sealed record ComparisonRowViewModel(string Title, string Description, IReadOnlyList<ComparisonCellViewModel> Cells);

public sealed partial class ComparisonViewModel : ObservableObject
{
    /// <summary>Width of the criterion column plus the minimum comfortable width of one subject column.</summary>
    public const double CriterionColumn = 200, SubjectColumn = 260;

    private readonly AppServices _s;
    private readonly INavigator _nav;
    private readonly Comparison _comparison;

    public ComparisonViewModel(AppServices s, INavigator nav, Comparison comparison)
    {
        _s = s;
        _nav = nav;
        _comparison = comparison;
        Subjects = comparison.Subjects.Select(x => new ComparisonSubjectViewModel(x)).ToList();
        _selectedSubject = Subjects[0];
        Rows = comparison.Criteria.Select(c => new ComparisonRowViewModel(c.Title, c.Description,
            comparison.Subjects.Select(x => new ComparisonCellViewModel(x.Id, comparison.Cell(c.Id, x.Id) ?? "")).ToList())).ToList();
        VerdictGuidance = comparison.VerdictGuidance;
        Sources = comparison.Sources;
        _visibleSubjects = Subjects;
    }

    public string Title => _comparison.Title;
    public IReadOnlyList<ComparisonSubjectViewModel> Subjects { get; }
    public IReadOnlyList<ComparisonRowViewModel> Rows { get; }
    public string VerdictGuidance { get; }
    public IReadOnlyList<string> Sources { get; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(VisibleSubjects))] private bool _isNarrow;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(VisibleSubjects))] private ComparisonSubjectViewModel _selectedSubject;
    [ObservableProperty] private IReadOnlyList<ComparisonSubjectViewModel> _visibleSubjects;

    partial void OnIsNarrowChanged(bool value) => RefreshVisible();
    partial void OnSelectedSubjectChanged(ComparisonSubjectViewModel value) => RefreshVisible();

    private void RefreshVisible() => VisibleSubjects = IsNarrow ? [SelectedSubject] : Subjects;

    /// <summary>Called by the view with its width; below the width where every subject fits, one subject is shown at a time.</summary>
    public void UpdateWidth(double width) => IsNarrow = width < CriterionColumn + SubjectColumn * Subjects.Count;

    [RelayCommand]
    private Task OpenLinkAsync(string url) => _s.Launcher.OpenUrlAsync(url);

    [RelayCommand]
    private Task BackAsync() => _nav.ShowCatalogAsync();
}
