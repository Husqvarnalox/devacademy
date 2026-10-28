using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevAcademy.Application.Services;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;
using DevAcademy.Domain;

namespace DevAcademy.Desktop.ViewModels;

/// <summary>After grading each option shows its own outcome: a right pick, a wrong pick, or a right answer that was not picked.</summary>
public sealed partial class QuizOptionViewModel(int index, string text, bool isAnswer) : ObservableObject
{
    public int Index { get; } = index;
    public string Text { get; } = text;
    public bool IsAnswer { get; } = isAnswer;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsRightPick), nameof(IsWrongPick), nameof(IsMissed))] private bool _isSelected;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsRightPick), nameof(IsWrongPick), nameof(IsMissed))] private bool _isGraded;
    public bool IsRightPick => IsGraded && IsSelected && IsAnswer;
    public bool IsWrongPick => IsGraded && IsSelected && !IsAnswer;
    public bool IsMissed => IsGraded && !IsSelected && IsAnswer;
}

public sealed partial class QuizQuestionViewModel : ObservableObject
{
    public QuizQuestionViewModel(int number, QuizQuestion q)
    {
        Question = q;
        Number = number;
        Options = q.Options.Select((o, i) => new QuizOptionViewModel(i, o, q.CorrectIndices.Contains(i))).ToList();
        GroupName = "q-" + q.Id;
    }

    public QuizQuestion Question { get; }
    public int Number { get; }
    public string Id => Question.Id;
    public string Prompt => Question.Prompt;
    public string Explanation => Question.Explanation;
    public string GroupName { get; }
    public IReadOnlyList<QuizOptionViewModel> Options { get; }
    /// <summary>
    /// Options rendered as radio buttons / check boxes. Exactly one of the two is non-empty, so a question never has hidden
    /// controls of the other kind bound to the same IsSelected (a hidden radio group would clear a multiple-choice selection).
    /// </summary>
    public IReadOnlyList<QuizOptionViewModel> SingleOptions => IsSingle ? Options : [];
    public IReadOnlyList<QuizOptionViewModel> MultipleOptions => IsMultiple ? Options : [];
    public bool IsSingle => Question.Type == QuestionType.Single;
    public bool IsMultiple => Question.Type == QuestionType.Multiple;
    public bool IsText => Question.Type is QuestionType.Short or QuestionType.Numeric;
    public string HintText => Loc.T(Question.Type switch
    {
        QuestionType.Single => "Quiz_HintSingle",
        QuestionType.Multiple => "Quiz_HintMultiple",
        QuestionType.Short => "Quiz_HintShort",
        _ => "Quiz_HintNumeric",
    });

    [ObservableProperty] private string _text = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ResultText), nameof(HasExplanation), nameof(IsLocked), nameof(IsCorrect), nameof(IsWrong))] private bool? _correct;
    public bool IsLocked => Correct is not null;
    public bool IsCorrect => Correct == true;
    public bool IsWrong => Correct == false;
    public string ResultText => Correct switch { true => Loc.T("Quiz_Correct"), false => Loc.T("Quiz_Wrong"), _ => "" };
    public bool HasExplanation => Correct is not null && !string.IsNullOrWhiteSpace(Explanation);

    public QuizAnswer? ToAnswer()
    {
        switch (Question.Type)
        {
            case QuestionType.Single:
            case QuestionType.Multiple:
                var sel = Options.Where(o => o.IsSelected).Select(o => o.Index).ToArray();
                return sel.Length == 0 ? null : QuizAnswer.Choice(sel);
            case QuestionType.Short:
                return string.IsNullOrWhiteSpace(Text) ? null : QuizAnswer.FromText(Text);
            default:
                var t = Text.Trim().Replace(',', '.');
                return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? QuizAnswer.FromNumber(n) : null;
        }
    }

    public void Reset()
    {
        Text = "";
        foreach (var o in Options)
        {
            o.IsSelected = false;
            o.IsGraded = false;
        }
        Correct = null;
        OnPropertyChanged(nameof(IsLocked));
    }

    internal void SetResult(bool correct)
    {
        Correct = correct;
        foreach (var o in Options) o.IsGraded = true;
        OnPropertyChanged(nameof(IsLocked));
    }
}

public sealed partial class QuizViewModel : ObservableObject
{
    private readonly AppServices _s;
    private readonly string _lessonId;
    private readonly Quiz _quiz;

    public QuizViewModel(AppServices s, string lessonId, Quiz quiz)
    {
        _s = s;
        _lessonId = lessonId;
        _quiz = quiz;
        Questions = new ObservableCollection<QuizQuestionViewModel>(quiz.Questions.Select((q, i) => new QuizQuestionViewModel(i + 1, q)));
    }

    public ObservableCollection<QuizQuestionViewModel> Questions { get; }
    public string ThresholdText => Loc.F("Quiz_Threshold", (int)Math.Round(_quiz.PassThreshold * 100));

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(SubmitCommand))] private bool _isSubmitted;
    [ObservableProperty] private string? _resultText;
    [ObservableProperty] private bool _passed;
    [ObservableProperty] private string? _bestScoreText;
    [ObservableProperty] private string? _error;

    public event Action<ProgressUpdate>? Attempted;

    public void SetBest(double? best) =>
        BestScoreText = best is { } b ? Loc.F("Quiz_Best", (int)Math.Round(b * 100)) : null;

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        try
        {
            var answers = new Dictionary<string, QuizAnswer>(StringComparer.Ordinal);
            foreach (var q in Questions)
                if (q.ToAnswer() is { } a) answers[q.Id] = a;
            var (result, update) = await _s.Progress.RecordQuizAttemptAsync(_lessonId, answers);
            foreach (var r in result.Questions)
                Questions.First(q => q.Id == r.QuestionId).SetResult(r.Correct);
            var right = result.Questions.Count(r => r.Correct);
            ResultText = Loc.F(result.Passed ? "Quiz_ResultPassed" : "Quiz_ResultFailed", right, result.Questions.Count);
            Passed = result.Passed;
            IsSubmitted = true;
            Error = null;
            SetBest(update.Progress.QuizBestScore);
            Attempted?.Invoke(update);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Error = Loc.F("Error_Quiz", ex.Message);
        }
    }

    private bool CanSubmit() => !IsSubmitted;

    [RelayCommand]
    private void Retry()
    {
        foreach (var q in Questions) q.Reset();
        IsSubmitted = false;
        ResultText = null;
        Passed = false;
    }
}
