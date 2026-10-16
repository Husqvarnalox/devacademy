namespace DevAcademy.Execution;

public static class ExecutionEngine
{
    /// <summary>Runner with all built-in language adapters and every sandbox this build knows (availability is checked at run time).</summary>
    public static ICodeRunner CreateDefault(ExecutionOptions? options = null)
    {
        options ??= new ExecutionOptions();
        return new CodeRunner(
            options,
            RuntimeFactory.CreateAll(options.CacheDirectory),
            [
                new LocalProcessSandbox(),
                new MacSandboxExecSandbox(options.CacheDirectory),
                new LinuxBubblewrapSandbox(options.CacheDirectory),
                new DockerSandbox(),
            ]);
    }

    /// <summary>Entry file name the built-in adapter for <paramref name="languageId"/> expects (e.g. "Main.kt"), or null for unknown languages.</summary>
    public static string? DefaultEntryFile(string languageId) =>
        RuntimeFactory.CreateAll(new ExecutionOptions().CacheDirectory)
            .FirstOrDefault(r => r.LanguageId == languageId)?.DefaultEntryFile;
}
