using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;

namespace DevAcademy.Desktop.Tests;

public class PracticeAndTheoryTests
{
    [Fact]
    public async Task Practice_lists_every_course_exercise_and_filters_by_language_and_title()
    {
        using var host = new TestHost();
        await Helpers.CompleteOnboardingAsync(host);
        await host.Main.ShowProblemsAsync();
        var page = Assert.IsType<ProblemsViewModel>(host.Main.Current);
        Assert.True(page.HasAlgorithms);
        var algorithms = page.Items.Count;

        await page.SelectScopeCommand.ExecuteAsync(PracticeScope.Exercises);
        Assert.True(page.IsExercisesScope);
        Assert.True(page.Items.Count > algorithms);
        Assert.True(page.HasTopics);

        page.SelectedLanguage = page.LanguageOptions.Single(o => o.Value == "python");
        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, r => Assert.Contains("python", r.LanguageIds));

        var title = page.Items[0].Title;
        page.SearchText = title;
        Assert.All(page.Items, r => Assert.True(r.Title.Contains(title, StringComparison.CurrentCultureIgnoreCase) || r.CourseTitle.Contains(title, StringComparison.CurrentCultureIgnoreCase)));

        page.SearchText = "no such task 1f2e";
        Assert.True(page.IsEmpty);
        await page.ResetFiltersCommand.ExecuteAsync(null);
        Assert.Equal("", page.SearchText);
        Assert.Null(page.SelectedLanguage.Value);
        Assert.False(page.IsEmpty);
    }

    [Fact]
    public async Task Passing_a_placement_check_on_the_practice_page_credits_lessons_and_retires_the_offer()
    {
        using var host = new TestHost(contentRoot: TestHost.PathsRoot);
        await Helpers.CompleteOnboardingAsync(host);
        Assert.True(host.Main.HasPractice);
        await host.Main.ShowProblemsAsync();
        var page = Assert.IsType<ProblemsViewModel>(host.Main.Current);
        Assert.False(page.HasAlgorithms);
        var offer = Assert.Single(page.Placements);

        offer.StartCommand.Execute(null);
        var check = Assert.IsType<PlacementViewModel>(page.PlacementCheck);
        Assert.False(page.ShowPlacementList);
        Helpers.AnswerCorrectly(check.Quiz);
        await check.Quiz.SubmitCommand.ExecuteAsync(null);
        await Helpers.WaitUntilAsync(() => check.Outcome is not null);

        Assert.True(check.Credited);
        Assert.Same(check, page.PlacementCheck);
        Assert.Empty(page.Placements);
        Assert.Equal(3, (await host.Services.Path.GetCoursePathAsync("py-basics"))!.Completed);
        check.CloseCommand.Execute(null);
        Assert.True(page.ShowPlacementList);
    }

    [Fact]
    public async Task Theory_opens_one_section_at_a_time_and_remembers_the_whole_page_choice()
    {
        using var host = new TestHost();
        await Helpers.CompleteOnboardingAsync(host);
        var lessons = host.Services.Catalog.GetLessons(Helpers.CourseId);
        await host.Main.OpenLessonAsync(lessons[0].Id);
        var ws = Assert.IsType<LessonWorkspaceViewModel>(host.Main.Current);

        Assert.True(ws.HasTheorySections);
        Assert.Equal("", ws.TheorySections[^1].Key);
        var first = ws.TheorySections[0];
        Assert.True(first.IsSelected);
        Assert.Equal(first.Key, ws.SelectedSection);
        Assert.True(ws.IsTheoryStart);
        Assert.True(ws.VisibleTheoryMarkdown.Length < ws.TheoryMarkdown.Length);
        Assert.DoesNotContain("<!-- section:", ws.VisibleTheoryMarkdown);

        Assert.True(ws.HasNextSection);
        ws.NextSectionCommand.Execute(null);
        Assert.Equal(ws.TheorySections[1].Key, ws.SelectedSection);
        Assert.False(ws.IsTheoryStart);
        Assert.False(first.IsSelected);

        ws.TheorySections[^1].SelectCommand.Execute(null);
        Assert.Equal(ws.TheoryMarkdown, ws.VisibleTheoryMarkdown);
        Assert.True(ws.IsTheoryStart && ws.IsTheoryEnd);
        for (var i = 0; i < 50 && await host.Services.Settings.GetAsync(LessonWorkspaceViewModel.TheoryViewKey) != "1"; i++) await Task.Delay(20);

        await host.Main.OpenLessonAsync(lessons[1].Id);
        var next = Assert.IsType<LessonWorkspaceViewModel>(host.Main.Current);
        Assert.Equal("", next.SelectedSection);
        Assert.Equal(next.TheoryMarkdown, next.VisibleTheoryMarkdown);
    }

    [Fact]
    public async Task Lesson_navigation_numbers_lessons_and_shows_course_progress()
    {
        using var host = new TestHost();
        await Helpers.CompleteOnboardingAsync(host);
        await host.Main.OpenLessonAsync(Helpers.FirstLessonId(host));
        var ws = Assert.IsType<LessonWorkspaceViewModel>(host.Main.Current);

        Assert.Equal("01", ws.NavItems[0].Number);
        Assert.True(ws.NavItems[0].IsCurrent);
        Assert.StartsWith("Урок 01 из " + ws.NavItems.Count, ws.OverlineText);
        Assert.Equal($"Пройдено 0 из {ws.NavItems.Count} уроков", ws.CourseProgressText);
    }

    [Fact]
    public async Task Catalog_search_and_status_filter_hide_what_does_not_match()
    {
        using var host = new TestHost();
        await Helpers.CompleteOnboardingAsync(host);
        await host.Main.ShowCatalogAsync();
        var catalog = Assert.IsType<CatalogViewModel>(host.Main.Current);
        Assert.NotEmpty(catalog.VisibleGroups);

        catalog.SetFilterCommand.Execute(CatalogFilter.Completed);
        Assert.Empty(catalog.VisibleGroups);
        Assert.True(catalog.ShowNoMatches);

        catalog.SetFilterCommand.Execute(CatalogFilter.All);
        catalog.SearchText = "Основы Python";
        var courses = catalog.VisibleGroups.SelectMany(g => g.Tracks).SelectMany(t => t.Courses).ToList();
        Assert.Contains(courses, c => c.Id == Helpers.CourseId);
        Assert.All(courses, c => Assert.True(c.Matches("Основы Python")));
        Assert.False(catalog.ShowFeaturedPath);

        catalog.ClearSearchCommand.Execute(null);
        Assert.False(catalog.HasQuery);
    }
}
