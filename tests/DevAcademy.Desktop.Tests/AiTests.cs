using System.Reflection;
using DevAcademy.AI;
using DevAcademy.Application.Tutor;
using DevAcademy.Desktop.Tests.Support;
using DevAcademy.Desktop.ViewModels;

namespace DevAcademy.Desktop.Tests;

public class AiSettingsTests
{
    private const string Secret = "sk-very-secret-key-987";

    private static AiProviderViewModel Fake(AiSettingsViewModel ai) => ai.Providers.Single(p => p.Id == "fake");

    [Fact]
    public async Task Saved_key_sets_HasKey_goes_to_the_secret_store_and_is_never_exposed_by_view_models()
    {
        var fake = new FakeModelProvider();
        using var host = AiKit.CreateHost(fake);
        var ai = await AiKit.LoadSettingsAsync(host);
        var p = Fake(ai);
        Assert.False(p.HasKey);
        Assert.Equal("ключ не задан", p.KeyStateText);

        p.ApiKeyInput = Secret;
        Assert.True(p.SaveKeyCommand.CanExecute(null));
        await p.SaveKeyCommand.ExecuteAsync(null);
        await p.ValidateCommand.ExecuteAsync(null);

        Assert.True(p.HasKey);
        Assert.Equal("ключ сохранён", p.KeyStateText);
        Assert.Equal(Secret, host.Secrets.Get("fake"));
        Assert.Equal("", p.ApiKeyInput);
        Assert.Contains(Secret, fake.KeysSeen); // the key reaches only the provider call

        // Nothing the UI can display or bind to contains the key.
        foreach (object vm in new object[] { p, ai })
            foreach (var prop in vm.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.GetIndexParameters().Length == 0))
                Assert.DoesNotContain(Secret, prop.GetValue(vm) as string ?? "");
        // ... and a freshly loaded VM only knows "has key".
        var again = Fake(await AiKit.LoadSettingsAsync(host));
        Assert.True(again.HasKey);
        Assert.Equal("", again.ApiKeyInput);

        p.RemoveKeyCommand.Execute(null);
        Assert.False(p.HasKey);
        Assert.Null(host.Secrets.Get("fake"));
    }

    [Fact]
    public async Task Unavailable_secret_store_disables_saving_and_shows_its_reason()
    {
        using var host = AiKit.CreateHost(new FakeModelProvider());
        host.Secrets.Available = false;
        var ai = await AiKit.LoadSettingsAsync(host);
        var p = Fake(ai);

        Assert.False(ai.SecretStoreAvailable);
        Assert.True(ai.HasSecretStoreMessage);
        Assert.Contains(host.Secrets.Reason, ai.SecretStoreMessage);
        p.ApiKeyInput = Secret;
        Assert.False(p.CanSave);
        Assert.False(p.SaveKeyCommand.CanExecute(null));
        await p.SaveKeyCommand.ExecuteAsync(null);
        Assert.Equal(0, host.Secrets.SetCalls);
        Assert.False(p.HasKey);
    }

    [Fact]
    public async Task Refreshing_models_fills_the_list_unknown_capabilities_say_неизвестно_and_the_cache_survives_reload()
    {
        using var host = AiKit.CreateHost(new FakeModelProvider());
        host.Secrets.Set("fake", "k");
        var ai = await AiKit.LoadSettingsAsync(host);
        var p = Fake(ai);
        Assert.False(p.HasModels);

        await p.RefreshModelsCommand.ExecuteAsync(null);

        Assert.Equal(2, p.Models.Count);
        var unknown = p.Models.Single(m => m.Id == "model-unknown");
        Assert.Equal("неизвестно", unknown.CapabilitiesText);
        Assert.Equal("неизвестно", unknown.ContextText);
        Assert.Equal("—", unknown.PriceText);
        var known = p.Models.Single(m => m.Id == "model-known");
        Assert.Contains("стриминг: да", known.CapabilitiesText);
        Assert.Contains("инструменты: нет", known.CapabilitiesText);
        Assert.Contains("текст: неизвестно", known.CapabilitiesText); // partially known: the rest stays unknown
        Assert.Equal("Known Model", known.DisplayName);
        Assert.Equal("вход $3 / выход $15", known.PriceText);
        Assert.Contains("Список обновлён", p.CacheText);

        var reloaded = Fake(await AiKit.LoadSettingsAsync(host));
        Assert.Equal(2, reloaded.Models.Count);
        // The model pickers get suggestions from the cache.
        var ai2 = await AiKit.LoadSettingsAsync(host);
        var row = ai2.Assignments[0];
        row.SelectedProvider = row.ProviderChoices.Single(c => c.Value == "fake");
        Assert.Contains("model-known", row.Suggestions);
    }

    [Fact]
    public async Task Manual_model_id_is_passed_untouched_to_the_provider_test()
    {
        var fake = new FakeModelProvider();
        using var host = AiKit.CreateHost(fake);
        host.Secrets.Set("fake", "k");
        var ai = await AiKit.LoadSettingsAsync(host);
        var p = Fake(ai);
        Assert.False(p.TestModelCommand.CanExecute(null)); // nothing chosen yet

        const string id = "ft:Custom/Model_v1.5:org-42@latest";
        p.CustomModelId = id;
        await p.TestModelCommand.ExecuteAsync(null);

        Assert.Equal(id, Assert.Single(fake.TestedModels));
        Assert.False(p.StatusIsError);
        Assert.Contains(id, p.StatusMessage);
        // It is logged as a model test, metadata only.
        var history = await host.Services.Tutor.GetHistoryAsync();
        Assert.Equal("ModelTest", Assert.Single(history).Scenario);
    }

    [Theory]
    [InlineData(AiErrorKind.InvalidKey)]
    [InlineData(AiErrorKind.PermissionDenied)]
    [InlineData(AiErrorKind.ModelNotFound)]
    [InlineData(AiErrorKind.RateLimited)]
    [InlineData(AiErrorKind.QuotaExceeded)]
    [InlineData(AiErrorKind.ProviderUnavailable)]
    [InlineData(AiErrorKind.BadRequest)]
    [InlineData(AiErrorKind.Network)]
    [InlineData(AiErrorKind.Timeout)]
    public async Task Every_error_kind_has_its_own_russian_message_that_includes_the_providers_text(AiErrorKind kind)
    {
        var fake = new FakeModelProvider { FailWith = new AiProviderException(kind, "fake", "PROVIDER-SAID-THIS", 400) };
        using var host = AiKit.CreateHost(fake);
        host.Secrets.Set("fake", "k");
        var p = Fake(await AiKit.LoadSettingsAsync(host));

        await p.RefreshModelsCommand.ExecuteAsync(null);

        Assert.True(p.StatusIsError);
        Assert.Contains("PROVIDER-SAID-THIS", p.StatusMessage);
        Assert.Matches("[А-Яа-я]{4,}", p.StatusMessage!);
        Assert.DoesNotContain("[", AiMessages.Describe(fake.FailWith)); // no missing resource keys
        // distinct from every other kind
        var others = Enum.GetValues<AiErrorKind>().Where(k => k != kind)
            .Select(k => AiMessages.Describe(new AiProviderException(k, "fake", "X")));
        Assert.DoesNotContain(AiMessages.Describe(new AiProviderException(kind, "fake", "X")), others);
    }

    [Fact]
    public void Every_refusal_reason_and_scenario_has_a_localized_text()
    {
        foreach (var reason in Enum.GetValues<TutorRefusalReason>())
        {
            var text = AiMessages.Refusal(new TutorRefusedException(reason, "DETAIL"));
            Assert.DoesNotContain("[AiRefusal_", text);
            Assert.Matches("[А-Яа-я]{4,}", text);
        }
        foreach (var s in Enum.GetValues<TutorScenario>())
            Assert.DoesNotContain("[AiScenario_", AiMessages.ScenarioName(s));
    }

    [Fact]
    public async Task Toggles_limits_and_assignments_are_persisted()
    {
        using var host = AiKit.CreateHost(new FakeModelProvider());
        var ai = await AiKit.LoadSettingsAsync(host);
        Assert.False(ai.IsEnabled); // off by default

        ai.IsEnabled = true;
        ai.AllowCode = false;
        await ai.WhenSavedAsync();
        ai.MaxOutputTokensText = "700";
        ai.MonthlyLimitText = "5,5";
        ai.SessionLimitText = "1";
        await ai.SaveLimitsCommand.ExecuteAsync(null);

        var def = ai.Assignments[0];
        def.SelectedProvider = def.ProviderChoices.Single(c => c.Value == "fake");
        def.ModelId = "any-free-text";
        var hint = ai.Assignments.Single(a => a.Scenario == TutorScenario.Hint);
        hint.SelectedProvider = hint.ProviderChoices.Single(c => c.Value == "fake");
        hint.ModelId = "other";
        await ai.SaveAssignmentsCommand.ExecuteAsync(null);

        var s = await host.Services.TutorSettings.LoadAsync();
        Assert.True(s.AiEnabled);
        Assert.False(s.AllowCode);
        Assert.Equal(700, s.MaxOutputTokens);
        Assert.Equal(5.5m, s.MonthlyLimitUsd);
        Assert.Equal(1m, s.SessionLimitUsd);
        Assert.Equal(new ModelAssignment("fake", "any-free-text"), s.Default);
        Assert.Equal(new ModelAssignment("fake", "other"), s.For(TutorScenario.Hint));
        Assert.Equal(new ModelAssignment("fake", "any-free-text"), s.For(TutorScenario.Debug)); // "по умолчанию"

        // Reload shows the same state; clearing the override returns to the default.
        var again = await AiKit.LoadSettingsAsync(host);
        Assert.True(again.IsEnabled);
        var hint2 = again.Assignments.Single(a => a.Scenario == TutorScenario.Hint);
        Assert.Equal("other", hint2.ModelId);
        hint2.SelectedProvider = hint2.ProviderChoices[0];
        await again.SaveAssignmentsCommand.ExecuteAsync(null);
        Assert.Equal("any-free-text", (await host.Services.TutorSettings.LoadAsync()).For(TutorScenario.Hint)!.ModelId);
    }

    [Fact]
    public async Task Invalid_limits_and_missing_model_are_rejected_without_saving()
    {
        using var host = AiKit.CreateHost(new FakeModelProvider());
        var ai = await AiKit.LoadSettingsAsync(host);
        ai.MonthlyLimitText = "abc";
        await ai.SaveLimitsCommand.ExecuteAsync(null);
        Assert.Contains("Лимит", ai.LimitsMessage);
        Assert.Null((await host.Services.TutorSettings.LoadAsync()).MonthlyLimitUsd);

        var def = ai.Assignments[0];
        def.SelectedProvider = def.ProviderChoices.Single(c => c.Value == "fake");
        def.ModelId = "  ";
        await ai.SaveAssignmentsCommand.ExecuteAsync(null);
        Assert.Contains("Model ID", ai.AssignmentsMessage);
        Assert.Null((await host.Services.TutorSettings.LoadAsync()).Default);
    }

    [Fact]
    public async Task Clearing_history_asks_for_confirmation_and_shows_metadata_only()
    {
        var fake = new FakeModelProvider();
        using var host = AiKit.CreateHost(fake);
        host.Secrets.Set("fake", "k");
        var ai = await AiKit.LoadSettingsAsync(host);
        var p = Fake(ai);
        p.CustomModelId = "m";
        await p.TestModelCommand.ExecuteAsync(null);
        await ai.RefreshHistoryCommand.ExecuteAsync(null);
        var row = Assert.Single(ai.History);
        Assert.Equal("fake", row.Provider);
        Assert.Equal("m", row.Model);
        Assert.Equal("Проверка модели", row.Scenario);
        Assert.Equal("5 / 2", row.Tokens);
        Assert.Equal("ок", row.Status);

        host.Dialogs.Answer = false;
        await ai.ClearHistoryCommand.ExecuteAsync(null);
        Assert.Single(ai.History);
        host.Dialogs.Answer = true;
        await ai.ClearHistoryCommand.ExecuteAsync(null);
        Assert.Empty(ai.History);
        Assert.Empty(await host.Services.Tutor.GetHistoryAsync());
    }

    [Fact]
    public async Task OpenAI_compatible_endpoint_is_registered_from_its_base_url_and_invalid_urls_are_rejected()
    {
        using var host = new TestHost();
        var ai = await AiKit.LoadSettingsAsync(host);
        var compat = ai.Providers.Single(p => p.Id == ProviderIds.OpenAICompatible);
        Assert.False(compat.IsRegistered);
        Assert.False(compat.RefreshModelsCommand.CanExecute(null));

        compat.BaseUrl = "ftp://nope";
        await compat.ApplyBaseUrlCommand.ExecuteAsync(null);
        Assert.True(compat.StatusIsError);
        Assert.False(compat.IsRegistered);

        compat.BaseUrl = "http://localhost:11434/v1";
        await compat.ApplyBaseUrlCommand.ExecuteAsync(null);
        Assert.True(compat.IsRegistered);
        Assert.NotNull(host.Services.Providers.Find(ProviderIds.OpenAICompatible));
        Assert.Equal("http://localhost:11434/v1", await host.Services.Settings.GetAsync(TutorSettingsKeys.OpenAiCompatibleBaseUrl));
        Assert.True(compat.RefreshModelsCommand.CanExecute(null)); // key is optional for local servers
    }
}

public class TutorPanelTests
{
    private static async Task<(TestHost Host, FakeModelProvider Fake, LessonWorkspaceViewModel Ws)> SetupAsync(bool enable = true, Action<FakeModelProvider>? tweak = null)
    {
        var fake = new FakeModelProvider();
        tweak?.Invoke(fake);
        var host = AiKit.CreateHost(fake);
        var ws = await AiKit.OpenExerciseLessonAsync(host);
        if (enable) await AiKit.EnableAsync(host);
        await ws.Tutor.RefreshStatusAsync();
        return (host, fake, ws);
    }

    [Fact]
    public async Task Prepare_shows_the_preview_then_send_streams_chunks_and_writes_a_history_entry()
    {
        var (host, fake, ws) = await SetupAsync();
        using var _ = host;
        var t = ws.Tutor;
        Assert.False(t.IsExpanded); // collapsed by default
        t.Question = "Почему цикл не останавливается?";

        await t.AskCommand.ExecuteAsync(TutorScenario.Explain);

        Assert.True(t.IsPreviewVisible);
        Assert.Contains("Почему цикл не останавливается?", t.PreviewText);
        Assert.Contains("fake", t.PreviewSummary.ToLowerInvariant());
        Assert.Contains(AiKit.ModelId, t.PreviewSummary);
        Assert.Empty(fake.Streamed); // preview only: nothing sent yet
        Assert.Empty(await host.Services.Tutor.GetHistoryAsync());

        await t.SendCommand.ExecuteAsync(null);

        Assert.False(t.IsPreviewVisible);
        Assert.False(t.IsStreaming);
        Assert.Equal("Привет, **мир**", t.ResponseText);
        Assert.Equal("Привет, **мир**", t.ResponseMarkdown);
        Assert.True(t.HasMarkdown);
        Assert.Contains("12", t.UsageText);
        var sent = Assert.Single(fake.Streamed);
        Assert.Equal("test-key-123", sent.ApiKey);
        Assert.Equal(AiKit.ModelId, sent.ModelId);
        // What was sent is exactly what was previewed.
        Assert.Contains(sent.Messages[^1].Content, t.PreviewText);

        var h = Assert.Single(await host.Services.Tutor.GetHistoryAsync());
        Assert.Equal("ok", h.Status);
        Assert.Equal("Explain", h.Scenario);
        Assert.Equal(12, h.InputTokens);
        Assert.Equal(7, h.OutputTokens);
    }

    [Fact]
    public async Task Cancel_in_the_preview_sends_nothing()
    {
        var (host, fake, ws) = await SetupAsync();
        using var _ = host;
        await ws.Tutor.AskCommand.ExecuteAsync(TutorScenario.Hint);
        Assert.True(ws.Tutor.IsPreviewVisible);
        ws.Tutor.CancelPreviewCommand.Execute(null);
        Assert.False(ws.Tutor.IsPreviewVisible);
        await ws.Tutor.SendCommand.ExecuteAsync(null); // nothing pending
        Assert.Empty(fake.Streamed);
        Assert.Empty(await host.Services.Tutor.GetHistoryAsync());
    }

    [Fact]
    public async Task Skip_preview_option_sends_straight_away_but_is_off_by_default()
    {
        var (host, fake, ws) = await SetupAsync();
        using var _ = host;
        Assert.False(ws.Tutor.SkipPreview);
        ws.Tutor.SkipPreview = true;
        await ws.Tutor.AskCommand.ExecuteAsync(TutorScenario.Debug);
        Assert.False(ws.Tutor.IsPreviewVisible);
        Assert.Single(fake.Streamed);
        Assert.True(ws.Tutor.HasMarkdown);
    }

    [Fact]
    public async Task Disabled_ai_refuses_with_a_settings_button_and_sends_nothing()
    {
        var (host, fake, ws) = await SetupAsync(enable: false);
        using var _ = host;
        var t = ws.Tutor;
        Assert.False(t.IsAiEnabled);
        Assert.True(t.IsOffNoticeVisible);

        await t.AskCommand.ExecuteAsync(TutorScenario.Explain);
        await t.SendCommand.ExecuteAsync(null);

        Assert.False(t.IsPreviewVisible);
        Assert.Contains("AI выключен", t.ErrorText);
        Assert.True(t.ShowOpenSettings);
        Assert.Empty(fake.Streamed);
        Assert.Empty(await host.Services.Tutor.GetHistoryAsync());

        await t.OpenSettingsCommand.ExecuteAsync(null);
        var settings = Assert.IsType<SettingsViewModel>(host.Main.Current);
        Assert.Equal(SettingsViewModel.AiTab, settings.SelectedTab);
    }

    [Fact]
    public async Task Missing_model_and_missing_key_are_specific_refusals()
    {
        var (host, fake, ws) = await SetupAsync(enable: false);
        using var _ = host;
        await host.Services.TutorSettings.SetAiEnabledAsync(true);

        await ws.Tutor.AskCommand.ExecuteAsync(TutorScenario.Explain);
        Assert.Contains("модель", ws.Tutor.ErrorText);
        Assert.True(ws.Tutor.ShowOpenSettings);

        await host.Services.TutorSettings.SetDefaultAsync(new ModelAssignment("fake", "m"));
        await ws.Tutor.AskCommand.ExecuteAsync(TutorScenario.Explain);
        Assert.Contains("ключ", ws.Tutor.ErrorText);
        Assert.True(ws.Tutor.ShowOpenSettings);
        Assert.False(ws.Tutor.IsPreviewVisible);

        host.Secrets.Available = false;
        await ws.Tutor.AskCommand.ExecuteAsync(TutorScenario.Explain);
        Assert.Contains("Хранилище секретов", ws.Tutor.ErrorText);
        Assert.Empty(fake.Streamed);
    }

    [Fact]
    public async Task Code_is_in_the_payload_only_when_toggled_on_and_globally_allowed()
    {
        const string Marker = "SECRET_STUDENT_CODE_MARKER";
        var (host, fake, ws) = await SetupAsync();
        using var _ = host;
        var t = ws.Tutor;
        ws.ActiveSession!.Files[0].Content = $"# {Marker}\nprint(1)\n";

        t.IncludeCode = true;
        await t.AskCommand.ExecuteAsync(TutorScenario.CodeReview);
        Assert.Contains(Marker, t.PreviewText);
        Assert.Contains("Code", string.Join(",", t.PreviewSummary));

        t.IncludeCode = false;
        await t.AskCommand.ExecuteAsync(TutorScenario.CodeReview);
        Assert.True(t.IsPreviewVisible);
        Assert.DoesNotContain(Marker, t.PreviewText);

        t.IncludeCode = true;
        await host.Services.TutorSettings.SetAllowCodeAsync(false);
        await t.AskCommand.ExecuteAsync(TutorScenario.CodeReview);
        Assert.DoesNotContain(Marker, t.PreviewText);
        Assert.True(t.HasCodeBlockedNote);

        await t.SendCommand.ExecuteAsync(null);
        var sent = Assert.Single(fake.Streamed);
        Assert.DoesNotContain(sent.Messages, m => m.Content.Contains(Marker));
    }

    [Fact]
    public async Task Cancelling_a_stream_keeps_the_partial_text_and_logs_cancelled()
    {
        var gate = new TaskCompletionSource();
        var (host, fake, ws) = await SetupAsync(tweak: f => f.Gate = gate);
        using var _ = host;
        var t = ws.Tutor;
        await t.AskCommand.ExecuteAsync(TutorScenario.Explain);

        var sending = t.SendCommand.ExecuteAsync(null);
        await Helpers.WaitUntilAsync(() => t.ResponseText == "Привет");
        Assert.True(t.IsStreaming);
        Assert.False(t.HasMarkdown); // plain text while streaming
        t.CancelStreamingCommand.Execute(null);
        await sending;

        Assert.False(t.IsStreaming);
        Assert.Equal("Привет", t.ResponseText);
        Assert.Equal("Привет", t.ResponseMarkdown);
        Assert.Contains("Остановлено", t.Notice);
        Assert.Equal("cancelled", Assert.Single(await host.Services.Tutor.GetHistoryAsync()).Status);
    }

    [Fact]
    public async Task Provider_errors_while_streaming_are_shown_as_messages_and_logged()
    {
        var (host, _, ws) = await SetupAsync(tweak: f => f.FailWith = new AiProviderException(AiErrorKind.RateLimited, "fake", "slow down", 429));
        using var _ = host;
        await ws.Tutor.AskCommand.ExecuteAsync(TutorScenario.Explain);
        await ws.Tutor.SendCommand.ExecuteAsync(null);
        Assert.Contains("slow down", ws.Tutor.ErrorText);
        Assert.False(ws.Tutor.IsStreaming);
        var h = Assert.Single(await host.Services.Tutor.GetHistoryAsync());
        Assert.Equal("error", h.Status);
        Assert.Equal("RateLimited", h.ErrorKind);
    }

    [Fact]
    public async Task Nothing_is_applied_to_the_code_by_a_response()
    {
        var (host, _, ws) = await SetupAsync();
        using var _ = host;
        var before = ws.ActiveSession!.Files.Select(f => f.Content).ToList();
        ws.Tutor.SkipPreview = true;
        await ws.Tutor.AskCommand.ExecuteAsync(TutorScenario.Debug);
        Assert.Equal(before, ws.ActiveSession.Files.Select(f => f.Content).ToList());
        Assert.Null(ws.ActiveSession.ConsoleResult);
        Assert.Null(ws.ActiveSession.Report);
    }
}
