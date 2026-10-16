namespace DevAcademy.Execution;

/// <summary>A language adapter that can also run inside a container image using bare command names.</summary>
public interface IContainerizableRuntime : ILanguageRuntime
{
    /// <summary>Plan whose FileName values are commands resolved inside the image (no host paths except the working directory).</summary>
    BuildPlan CreateContainerPlan(string workingDirectory, string entryFile);
}
