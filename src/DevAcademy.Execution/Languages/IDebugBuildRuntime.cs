namespace DevAcademy.Execution;

/// <summary>Runtimes that can produce a binary a native debugger understands (-g, no optimisation). The normal run plan is untouched.</summary>
internal interface IDebugBuildRuntime : ILanguageRuntime
{
    /// <param name="sourceDirectory">Where the sources live when that is not the (output) working directory; null = the working directory.</param>
    BuildPlan CreateDebugPlan(string workingDirectory, string entryFile, RuntimeStatus status, string? sourceDirectory = null);
}

/// <summary>Marks debug builds whose result is a self-contained native binary (the run plan's arguments are not part of the debug run command).</summary>
internal interface INativeDebugBuild : IDebugBuildRuntime;
