using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Application.Services;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;
using DevAcademy.Desktop.Markdown;
using DevAcademy.Domain;

namespace DevAcademy.Desktop.ViewModels;

public enum LessonMode { Theory, Practice, Quiz, Challenge }

public sealed partial class LessonNavItemViewModel(LessonStatus status, bool isCurrent, Func<string, Task> open) : ObservableObject
{
    public string LessonId { get; } = status.Lesson.Id;
    public string Title { get; } = status.Lesson.Title;
    public LessonState State { get; } = status.State;
    public string StateText { get; } = Display.State(status.State);
    public bool IsCurrent { get; } = isCurrent;
    public string Glyph { get; } = status.State switch
    {
        LessonState.Completed => "✓",
        LessonState.InProgress => "●",
        LessonState.Locked => "🔒",
        _ => "○",
    };

    [RelayCommand]
    private Task OpenAsync() => open(LessonId);
}

public sealed class SourceLinkViewModel(string url, ILauncherOpen launcher)
{
    public string Url { get; } = url;
    public IRelayCommand OpenCommand { get; } = new AsyncRelayCommand(() => launcher.OpenAsync(url));
}

public interface ILauncherOpen { Task OpenAsync(string url); }

public sealed class FactViewModel(Fact fact, ILauncherOpen launcher)
{
    public string KindText { get; } = Display.Fact(fact.Kind);
    public string Text { get; } = fact.Text;
    public IReadOnlyList<SourceLinkViewModel> Sources { get; } = fact.Sources.Select(s => new SourceLinkViewModel(s, launcher)).ToList();
    public bool HasSources => Sources.Count > 0;
}

public sealed partial class LessonWorkspaceViewModel : ObservableObject, ILauncherOpen
{
    private readonly AppServices _s;
    private readonly INavigator _nav;
    private ExerciseSessionViewModel? _scratch;

    public LessonWorkspaceViewModel(AppServices s, INavigator nav, Lesson lesson)
    {
        _s = s;
        _nav = nav;
        Lesson = lesson;
        Facts = lesson.Facts.Select(f => new FactViewModel(f, this)).ToList();
        Objectives = lesson.Objectives;
        Quiz = lesson.Quiz is { } q ? new QuizViewModel(s, lesson.Id, q) : null;
        if (Quiz is not null) Quiz.Attempted += u => _ = HandleUpdateAsync(u);
        Tutor = new TutorPanelViewModel(s, nav, lesson.Id, () => ActiveSession);
    }

    public TutorPanelViewModel Tutor { get; }

    public Lesson Lesson { get; }
    public string Title => Lesson.Title;
    public string Goal => Lesson.Goal;
    public string TheoryMarkdown => Lesson.Theory.Markdown;
    public IReadOnlyList<string> Objectives { get; }
    public IReadOnlyList<FactViewModel> Facts { get; }
    public QuizViewModel? Quiz { get; }
    public ExerciseSessionViewModel? Practice { get; private set; }
    public ExerciseSessionViewModel? Challenge { get; private set; }
    public ObservableCollection<LessonNavItemViewModel> NavItems { get; } = [];

    public string MetaText => Loc.F("Lesson_Meta", Display.Difficulty(Lesson.Difficulty), Lesson.EstimatedMinutes);
    public bool HasPractice => Lesson.Exercise is not null;
    public bool HasQuiz => Quiz is not null;
    public bool HasChallenge => Lesson.Challenge is not null;
    public string ChallengeTitle => Lesson.Challenge?.Title ?? "";
    public string ChallengePrompt => Lesson.Challenge?.Prompt ?? "";
    public bool HasFacts => Facts.Count > 0;
    public bool HasObjectives => Objectives.Count > 0;
    public bool IsTheoryOnly => Lesson.Exercise is null && Lesson.Quiz is null;
    public bool ShowModeTabs => HasPractice || HasQuiz || HasChallenge;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsTheoryMode), nameof(IsPracticeMode), nameof(IsQuizMode), nameof(IsChallengeMode), nameof(ShowSession))]
    private LessonMode _mode = LessonMode.Theory;

    public bool IsTheoryMode => Mode == LessonMode.Theory;
    public bool IsPracticeMode => Mode == LessonMode.Practice;
    public bool IsQuizMode => Mode == LessonMode.Quiz;
    public bool IsChallengeMode => Mode == LessonMode.Challenge;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowSession), nameof(IsScratchActive))] private ExerciseSessionViewModel? _activeSession;
    public bool ShowSession => ActiveSession is not null && Mode != LessonMode.Quiz;
    public bool IsScratchActive => _scratch is not null && ReferenceEquals(ActiveSession, _scratch);

    [ObservableProperty] private bool _isNavVisible = true;
    [ObservableProperty] private bool _isNarrow;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsLocked))] private LessonState _state;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanMarkRead))] private bool _isCompleted;
    [ObservableProperty] private string _stateText = "";
    [ObservableProperty] private string? _completionText;
    [ObservableProperty] private string? _requirementsText;
    [ObservableProperty] private string? _nextLessonId;
    [ObservableProperty] private string? _nextLessonTitle;
    [ObservableProperty] private string? _message;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasComplexity), nameof(ComplexityTime), nameof(ComplexitySpace), nameof(ComplexityExplanation), nameof(HasComplexityExplanation))]
    private ProblemComplexity? _complexity;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsEditorialVisible), nameof(CanShowEditorial))] private string? _editorialMarkdown;
    public bool HasComplexity => Complexity is not null;
    public string ComplexityTime => Complexity is null ? "" : Loc.F("Complexity_Time", Complexity.Time);
    public string ComplexitySpace => Complexity is null ? "" : Loc.F("Complexity_Space", Complexity.Space);
    public string ComplexityExplanation => Complexity?.Explanation ?? "";
    public bool HasComplexityExplanation => !string.IsNullOrWhiteSpace(Complexity?.Explanation);
    public bool HasEditorial => HasPractice && _s.Problems.HasEditorial(Lesson.Id);
    public bool IsEditorialVisible => EditorialMarkdown is not null;
    public bool CanShowEditorial => HasEditorial && !IsEditorialVisible;
    public bool IsLocked => State == LessonState.Locked;
    public bool CanMarkRead => IsTheoryOnly && !IsCompleted;
    public bool HasNextLesson => NextLessonId is not null;
    partial void OnNextLessonIdChanged(string? value) => OnPropertyChanged(nameof(HasNextLesson));

    public async Task LoadAsync()
    {
        await _s.Progress.StartLessonAsync(Lesson.Id);
        if (Lesson.Exercise is { } ex)
        {
            Practice = await ExerciseSessionViewModel.CreateAsync(_s, Lesson.Id, ex);
            Practice.Submitted += u => _ = HandleUpdateAsync(u);
        }
        if (Lesson.Challenge is { } ch)
        {
            Challenge = await ExerciseSessionViewModel.CreateAsync(_s, Lesson.Id, ch);
            Challenge.Submitted += u => _ = HandleUpdateAsync(u);
        }
        UpdateActive();
        await RefreshAsync();
        await Tutor.RefreshStatusAsync();
    }

    [RelayCommand]
    private void SelectMode(LessonMode mode)
    {
        Mode = mode;
        UpdateActive();
    }

    private void UpdateActive() =>
        ActiveSession = _scratch ?? (Mode == LessonMode.Challenge ? Challenge : Practice);

    public void SetNarrow(bool narrow)
    {
        if (IsNarrow == narrow) return;
        IsNarrow = narrow;
        IsNavVisible = !narrow;
    }

    [RelayCommand]
    private void ToggleNav() => IsNavVisible = !IsNavVisible;

    [RelayCommand]
    private void OpenSample(CodeSampleRequest sample)
    {
        _scratch = ExerciseSessionViewModel.CreateScratch(_s, Lesson.Id, sample.LanguageId, sample.Code);
        if (Mode == LessonMode.Quiz) Mode = LessonMode.Theory;
        UpdateActive();
        OnPropertyChanged(nameof(IsScratchActive));
    }

    [RelayCommand]
    private void CloseScratch()
    {
        _scratch = null;
        UpdateActive();
        OnPropertyChanged(nameof(IsScratchActive));
    }

    [RelayCommand]
    private async Task BackToCourseAsync() => await _nav.OpenCourseAsync(Lesson.CourseId);

    /// <summary>Shows the editorial: straight away once solved, otherwise only after an explicit spoiler confirmation.</summary>
    [RelayCommand]
    private async Task ShowEditorialAsync()
    {
        if (!HasEditorial) return;
        var text = await _s.Problems.GetEditorialAsync(Lesson.Id, confirmedSpoiler: false);
        if (text is null)
        {
            if (!await _s.Dialogs.ConfirmAsync(Loc.T("Editorial_Title"), Loc.T("Editorial_Message"), Loc.T("Editorial_Confirm"))) return;
            text = await _s.Problems.GetEditorialAsync(Lesson.Id, confirmedSpoiler: true);
        }
        EditorialMarkdown = text;
    }

    [RelayCommand]
    private void HideEditorial() => EditorialMarkdown = null;

    [RelayCommand(CanExecute = nameof(CanMarkRead))]
    private async Task MarkReadAsync()
    {
        try
        {
            var update = await _s.Progress.MarkTheoryReadAsync(Lesson.Id);
            await HandleUpdateAsync(update);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { Message = ex.Message; }
    }

    [RelayCommand(CanExecute = nameof(HasNextLesson))]
    private async Task NextLessonAsync()
    {
        if (NextLessonId is { } id) await _nav.OpenLessonAsync(id);
    }

    public Task OpenAsync(string url) => OpenUrlAsync(url);

    private async Task OpenUrlAsync(string url)
    {
        if (!await _s.Launcher.OpenUrlAsync(url)) Message = Loc.F("Error_OpenLink", url);
    }

    [RelayCommand]
    private Task OpenLinkAsync(string url) => OpenUrlAsync(url);

    public async Task FlushAsync()
    {
        Tutor.Shutdown();
        if (Practice is not null) await Practice.Debug.ShutdownAsync();
        if (Challenge is not null) await Challenge.Debug.ShutdownAsync();
        if (Practice is not null) await Practice.FlushDraftsAsync();
        if (Challenge is not null) await Challenge.FlushDraftsAsync();
    }

    private async Task HandleUpdateAsync(ProgressUpdate update)
    {
        await RefreshAsync();
        if (update.JustCompleted) Message = Loc.T("Lesson_JustCompleted");
    }

    /// <summary>Re-reads state, requirements, navigation and the next lesson after any progress change.</summary>
    public async Task RefreshAsync()
    {
        var status = await _s.Path.GetLessonStatusAsync(Lesson.Id);
        var p = status?.Progress;
        State = status?.State ?? LessonState.Available;
        IsCompleted = State == LessonState.Completed;
        StateText = Display.State(State);
        CompletionText = IsCompleted ? Loc.F("Lesson_CompletedBy", Display.Source(p?.CompletionSource)) : null;
        Quiz?.SetBest(p?.QuizBestScore);
        Complexity = Lesson.Exercise is null ? null : await _s.Problems.GetComplexityAsync(Lesson.Id);
        MarkReadCommand.NotifyCanExecuteChanged();

        var missing = new List<string>();
        if (!IsCompleted)
        {
            if (Lesson.Exercise is not null && p?.ExercisePassed != true) missing.Add(Loc.T("Req_Exercise"));
            if (Lesson.Quiz is { } q && (p?.QuizBestScore ?? 0) < q.PassThreshold)
                missing.Add(Loc.F("Req_Quiz", (int)Math.Round(q.PassThreshold * 100)));
            if (IsTheoryOnly && p?.TheoryRead != true) missing.Add(Loc.T("Req_Read"));
        }
        RequirementsText = missing.Count == 0 ? null : Loc.F("Req_Needed", string.Join("; ", missing));

        var path = await _s.Path.GetCoursePathAsync(Lesson.CourseId);
        NavItems.Clear();
        if (path is not null)
            foreach (var l in path.Lessons)
                NavItems.Add(new LessonNavItemViewModel(l, l.Lesson.Id == Lesson.Id, id => _nav.OpenLessonAsync(id)));

        await ComputeNextAsync(path);
        NextLessonCommand.NotifyCanExecuteChanged();
    }

    private async Task ComputeNextAsync(CoursePath? path)
    {
        NextLessonId = null;
        NextLessonTitle = null;
        if (!IsCompleted) return;

        async Task<Lesson?> FirstOpen(IEnumerable<string> ids)
        {
            foreach (var id in ids.Where(i => i != Lesson.Id))
                if (await _s.Path.GetLessonStatusAsync(id) is { State: not LessonState.Completed } st) return st.Lesson;
            return null;
        }

        var next = await FirstOpen(Lesson.Next);
        if (next is null)
        {
            var profile = await _s.Settings.GetProfileAsync();
            var recs = await _s.Path.GetRecommendationsAsync(profile);
            next = await FirstOpen(recs.Where(r => r.Kind != RecommendationKind.Review).Select(r => r.LessonId));
        }
        if (next is null && path is not null)
        {
            var idx = path.Lessons.ToList().FindIndex(l => l.Lesson.Id == Lesson.Id);
            next = path.Lessons.Skip(idx + 1).FirstOrDefault(l => l.State != LessonState.Completed)?.Lesson;
        }
        NextLessonId = next?.Id;
        NextLessonTitle = next?.Title;
    }
}
