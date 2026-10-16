namespace DevAcademy.Notebooks.Kernel;

/// <summary>What the notebook screen needs from the kernel layer. The default implementation talks to real Python; tests substitute a fake.</summary>
public interface INotebookKernelService
{
    /// <summary>Creates a kernel client for <paramref name="interpreter"/> (not started). The kernel's working directory is <paramref name="workingDirectory"/>.</summary>
    IKernelClient Create(string interpreter, string workingDirectory);

    /// <summary>ipykernel version installed for the interpreter, or null.</summary>
    Task<string?> GetInstalledVersionAsync(string interpreter, CancellationToken ct);

    /// <summary>The install command, or null when the interpreter is not a virtual environment.</summary>
    KernelInstallPlan? PlanInstall(string interpreter);

    Task<KernelInstallResult> InstallAsync(KernelInstallPlan plan, Action<string> onLine, CancellationToken ct);
}

public sealed class NotebookKernelService : INotebookKernelService
{
    public IKernelClient Create(string interpreter, string workingDirectory) => new KernelSession(new PythonKernelHost(interpreter, workingDirectory));

    public Task<string?> GetInstalledVersionAsync(string interpreter, CancellationToken ct) => KernelProvisioner.GetInstalledVersionAsync(interpreter, ct);

    public KernelInstallPlan? PlanInstall(string interpreter) => KernelProvisioner.PlanInstall(interpreter);

    public Task<KernelInstallResult> InstallAsync(KernelInstallPlan plan, Action<string> onLine, CancellationToken ct) => KernelProvisioner.InstallAsync(plan, onLine, ct);
}
