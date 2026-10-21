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

/// <summary>Which list the practice page shows: algorithm problems, or the exercises of every available course.</summary>
public enum PracticeScope { Algorithms, Exercises }

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
    public IReadOnlyList<string> LanguageIds { get; } = item.Languages;
    public string LanguagesText { get; } = string.Join(", ", item.Languages.Select(Display.LanguageName));
    /// <summary>The lesson's prerequisites are not done yet; opening it asks for confirmation (same rule as the course page).</summary>
    public bool IsLocked { get; } = item.State == LessonState.Locked;

    [RelayCommand]
    private Task OpenAsync() => nav.OpenLessonAsync(LessonId);
}

/// <summary>Solved exercises of one course in the current list (exercise passed = all tests green).</summary>
public sealed record PracticeTopicViewModel(string Title, string CountText, double Fraction);

/// <summary>A placement quiz the learner can take from the practice page.</summary>
public sealed class PlacementOfferViewModel(PlacementOffer offer, string courseTitle, Action<PlacementOffer> start)
{
    public string Title { get; } = offer.Assessment.Title;
    public string CourseTitle { get; } = courseTitle;
    public string Meta { get; } = Loc.F("Practice_CheckMeta", offer.QuestionCount, (int)Math.Round(offer.PassThreshold * 100), offer.CreditedLessons.Count);
    public IRelayCommand StartCommand { get; } = new RelayCommand(() => start(offer));
}

/// <summary>
/// The practice page: exercises to solve in any order (algorithm problems or every course exercise) filtered by
/// language, difficulty, state and title, plus the placement checks that credit lessons. Everything shown comes from
/// real progress: solved = the exercise passed its tests, credited = a placement quiz was passed.
/// </summary>
public sealed partial class ProblemsViewModel : ObservableObject
{
    private readonly AppServices _s;
    private readonly INavigator _nav;
    private IReadOnlyList<ProblemRowViewModel> _all = [];
    private IReadOnlyList<ProblemRowViewModel>? _algorithms;
    private IReadOnlyList<ProblemRowViewModel>? _exercises;
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
        _selectedLanguage = AllLanguages;
        HasAlgorithms = s.Problems.HasProblems;
        _scope = HasAlgorithms ? PracticeScope.Algorithms : PracticeScope.Exercises;
    }

    /// <summary>The page has something to show: an exercise to solve or a placement check.</summary>
    public static bool HasContent(AppServices s) =>
        s.Content.Current.Lessons.Any(l => l.Exercise is not null || l is { AssessmentFor.Count: > 0, Quiz: not null });

    private static readonly FilterOption<string?> AllLanguages = new(Loc.T("Practice_AllLanguages"), null);

    public IReadOnlyList<FilterOption<Difficulty?>> DifficultyOptions { get; }
    public IReadOnlyList<FilterOption<bool?>> SolvedOptions { get; }
    public ObservableCollection<FilterOption<string?>> LanguageOptions { get; } = [AllLanguages];
    public ObservableCollection<ProblemRowViewModel> Items { get; } = [];

    /// <summary>The algorithms switch exists only when algorithm content is loaded.</summary>
    public bool HasAlgorithms { get; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsAlgorithmsScope), nameof(IsExercisesScope))] private PracticeScope _scope;
    [ObservableProperty] private FilterOption<Difficulty?> _selectedDifficulty;
    [ObservableProperty] private FilterOption<bool?> _selectedSolved;
    [ObservableProperty] private FilterOption<string?> _selectedLanguage;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsEmpty))] private bool _isLoaded;
    [ObservableProperty] private string _countText = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasTopics))] private IReadOnlyList<PracticeTopicViewModel> _topics = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasPlacements))] private IReadOnlyList<PlacementOfferViewModel> _placements = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasPlacementCheck), nameof(ShowPlacementList))] private PlacementViewModel? _placementCheck;

    public bool IsAlgorithmsScope => Scope == PracticeScope.Algorithms;
    public bool IsExercisesScope => Scope == PracticeScope.Exercises;
    public bool IsEmpty => IsLoaded && Items.Count == 0;
    public bool HasTopics => Topics.Count > 1;
    public bool HasPlacements => Placements.Count > 0;
    public bool HasPlacementCheck => PlacementCheck is not null;
    public bool ShowPlacementList => PlacementCheck is null;

    partial void OnSelectedDifficultyChanged(FilterOption<Difficulty?> value) { if (!_suppress) Filter(); }
    partial void OnSelectedSolvedChanged(FilterOption<bool?> value) { if (!_suppress) Filter(); }
    partial void OnSelectedLanguageChanged(FilterOption<string?> value) { if (!_suppress && value is not null) Filter(); }
    partial void OnSearchTextChanged(string value) { if (!_suppress) Filter(); }

    /// <summary>Sets both filters and reloads once (used by tests and "reset filters").</summary>
    public async Task ApplyFiltersAsync(Difficulty? difficulty, bool? solved)
    {
        _suppress = true;
        SelectedDifficulty = DifficultyOptions.First(o => o.Value == difficulty);
        SelectedSolved = SolvedOptions.First(o => o.Value == solved);
        _suppress = false;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ResetFiltersAsync()
    {
        _suppress = true;
        SelectedLanguage = AllLanguages;
        SearchText = "";
        _suppress = false;
        await ApplyFiltersAsync(null, null);
    }

    [RelayCommand]
    private async Task SelectScopeAsync(PracticeScope scope)
    {
        if (Scope == scope) return;
        Scope = scope;
        await LoadAsync();
    }

    /// <summary>Loads the current list (cached per scope until progress changes) and the placement checks.</summary>
    public async Task LoadAsync()
    {
        if (IsAlgorithmsScope)
            _all = _algorithms ??= (await _s.Problems.ListAsync()).Select(r => new ProblemRowViewModel(r, _nav)).ToList();
        else
            _all = _exercises ??= (await _s.Problems.ListExercisesAsync()).Select(r => new ProblemRowViewModel(r, _nav)).ToList();

        var languages = _all.SelectMany(r => r.LanguageIds).Distinct(StringComparer.Ordinal)
            .Select(id => new FilterOption<string?>(Display.LanguageName(id), id)).OrderBy(o => o.Label, StringComparer.CurrentCulture).ToList();
        var keep = SelectedLanguage.Value;
        _suppress = true;
        LanguageOptions.Clear();
        LanguageOptions.Add(AllLanguages);
        foreach (var o in languages) LanguageOptions.Add(o);
        SelectedLanguage = LanguageOptions.FirstOrDefault(o => o.Value == keep) ?? AllLanguages;
        _suppress = false;

        Topics = _all.GroupBy(r => r.CourseTitle)
            .Select(g => new PracticeTopicViewModel(g.Key, Loc.F("Practice_TopicCount", g.Count(r => r.Solved), g.Count()), (double)g.Count(r => r.Solved) / g.Count()))
            .ToList();
        await LoadPlacementsAsync();
        Filter();
        IsLoaded = true;
        OnPropertyChanged(nameof(IsEmpty));
    }

    private async Task LoadPlacementsAsync()
    {
        var set = _s.Content.Current;
        var offers = await _s.Path.GetPlacementOffersAsync(set.Lessons.Where(l => l.AssessmentFor.Count > 0).Select(l => l.Id));
        Placements = offers.Select(o => new PlacementOfferViewModel(o, set.FindCourse(o.Assessment.CourseId)?.Title ?? "", StartPlacement)).ToList();
    }

    private void StartPlacement(PlacementOffer offer)
    {
        var check = new PlacementViewModel(_s, offer, OnPlacementChangedAsync);
        check.Closed += () => PlacementCheck = null;
        PlacementCheck = check;
    }

    /// <summary>A passed placement credits lessons: the lists and the offers change.</summary>
    private async Task OnPlacementChangedAsync()
    {
        _algorithms = null;
        _exercises = null;
        var open = PlacementCheck;
        await LoadAsync();
        PlacementCheck = open;
    }

    private void Filter()
    {
        var query = SearchText.Trim();
        var rows = _all.Where(r =>
            (SelectedDifficulty.Value is not { } d || r.Difficulty == d) &&
            (SelectedSolved.Value is not { } solved || r.Solved == solved) &&
            (SelectedLanguage.Value is not { } lang || r.LanguageIds.Contains(lang)) &&
            (query.Length == 0 || r.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) || r.CourseTitle.Contains(query, StringComparison.CurrentCultureIgnoreCase)));
        Items.Clear();
        foreach (var r in rows) Items.Add(r);
        CountText = Loc.F("Problems_Count", Items.Count);
        OnPropertyChanged(nameof(IsEmpty));
    }
}
