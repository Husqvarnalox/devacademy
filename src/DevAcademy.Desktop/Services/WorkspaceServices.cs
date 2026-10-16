using DevAcademy.Application.Ports;
using DevAcademy.Application.Workspace;
using DevAcademy.Execution;
using DevAcademy.Infrastructure.Workspace;
using DevAcademy.Notebooks.Kernel;
using DevAcademy.Terminal;

namespace DevAcademy.Desktop.Services;

/// <summary>Everything the Projects screen needs beyond <see cref="Composition.AppServices"/>. Tests replace the terminal factory with a fake.</summary>
public sealed class WorkspaceServices(
    IRecentProjectsRepository recents,
    IProjectSessionStore sessions,
    ITerminalFactory terminals,
    ProjectFileOps fileOps,
    IToolRunner tools,
    IClock clock)
{
    public IRecentProjectsRepository Recents { get; } = recents;
    public IProjectSessionStore Sessions { get; } = sessions;
    public ITerminalFactory Terminals { get; } = terminals;
    public ProjectFileOps FileOps { get; } = fileOps;
    public IToolRunner Tools { get; } = tools;
    public IClock Clock { get; } = clock;

    /// <summary>Starts Jupyter kernels for notebooks and installs ipykernel on request. Tests replace it with a fake.</summary>
    public INotebookKernelService Notebooks { get; set; } = new NotebookKernelService();

    /// <summary>Where "open in browser" for a dev server goes. Null means the system browser through the launcher.</summary>
    public IWebPreview? WebPreview { get; set; }

    /// <summary>Whether the file watcher is started for opened projects (tests that call the change handler directly switch it off).</summary>
    public bool WatchFiles { get; set; } = true;
}
