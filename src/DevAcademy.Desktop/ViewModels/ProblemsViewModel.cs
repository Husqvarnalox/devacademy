using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Application.Services;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;
using DevAcademy.Domain;

namespace DevAcademy.Desktop.ViewModels;

public sealed record FilterOption<T>(string Label, T Value)
{
    public override string ToString() => Label;
}

public sealed partial class ProblemRowViewModel(ProblemItem item, INavigator nav) : ObservableObject
{
    public string LessonId { get; } = item.Lesson.Id;
    public string Title { get; } = item.Lesson.Title;
    public string CourseTitle { get; } = item.Course.Title;
    public Difficulty Difficulty { get; } = item.Difficulty;
    public string DifficultyText { get; } = Display.Difficulty(item.Difficulty);
    public bool Solved { get; } = item.Solved;
    public string SolvedText { get; } = Loc.T(item.Solved ? "Problems_Solved" : "Problems_Unsolved");
    public string Glyph { get; } = item.Solved ? "✓" : "○";
    public string LanguagesText { get; } = string.Join(", ", item.Languages.Select(Display.LanguageName));

    [RelayCommand]
    private Task OpenAsync() => nav.OpenLessonAsync(LessonId);
}

/// <summary>Lessons with an exercise from algorithm courses, filterable by difficulty and solved state.</summary>
public sealed partial class ProblemsViewModel : ObservableObject
{
    private readonly AppServices _s;
    private readonly INavigator _nav;
    private bool _suppress;

    public ProblemsViewModel(AppServices s, INavigator nav)
    {
        _s = s;
        _nav = nav;
        DifficultyOptions =
        [
            new(Loc.T("Problems_AllDifficulties"), null),
            new(Display.Difficulty(Difficulty.Easy), Difficulty.Easy),
            new(Display.Difficulty(Difficulty.Medium), Difficulty.Medium),
            new(Display.Difficulty(Difficulty.Hard), Difficulty.Hard),
            new(Display.Difficulty(Difficulty.Expert), Difficulty.Expert),
        ];
        SolvedOptions =
        [
            new(Loc.T("Problems_AllStates"), null),
            new(Loc.T("Problems_Solved"), true),
            new(Loc.T("Problems_Unsolved"), false),
        ];
        _selectedDifficulty = DifficultyOptions[0];
        _selectedSolved = SolvedOptions[0];
    }

    public IReadOnlyList<FilterOption<Difficulty?>> DifficultyOptions { get; }
    public IReadOnlyList<FilterOption<bool?>> SolvedOptions { get; }
    public ObservableCollection<ProblemRowViewModel> Items { get; } = [];

    [ObservableProperty] private FilterOption<Difficulty?> _selectedDifficulty;
    [ObservableProperty] private FilterOption<bool?> _selectedSolved;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsEmpty))] private bool _isLoaded;
    [ObservableProperty] private string _countText = "";

    public bool IsEmpty => IsLoaded && Items.Count == 0;

    partial void OnSelectedDifficultyChanged(FilterOption<Difficulty?> value) { if (!_suppress) _ = LoadAsync(); }
    partial void OnSelectedSolvedChanged(FilterOption<bool?> value) { if (!_suppress) _ = LoadAsync(); }

    /// <summary>Sets both filters and reloads once (used by tests and "reset filters").</summary>
    public async Task ApplyFiltersAsync(Difficulty? difficulty, bool? solved)
    {
        _suppress = true;
        SelectedDifficulty = DifficultyOptions.First(o => o.Value == difficulty);
        SelectedSolved = SolvedOptions.First(o => o.Value == solved);
        _suppress = false;
        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        var rows = await _s.Problems.ListAsync(SelectedDifficulty.Value, SelectedSolved.Value);
        Items.Clear();
        foreach (var r in rows) Items.Add(new ProblemRowViewModel(r, _nav));
        CountText = Loc.F("Problems_Count", Items.Count);
        IsLoaded = true;
        OnPropertyChanged(nameof(IsEmpty));
    }
}
