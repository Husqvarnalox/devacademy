using System.Text;
using DevAcademy.Application.Go;
using DevAcademy.Desktop.Composition;
using DevAcademy.Desktop.Localization;
using DevAcademy.Domain;

namespace DevAcademy.Desktop.ViewModels;

public enum PackageInstallOutcome { Installed, Declined, Cancelled, Failed }

/// <summary><see cref="Message"/> is the Russian explanation for Declined/Cancelled/Failed (null when installed).</summary>
public sealed record PackageInstallResult(PackageInstallOutcome Outcome, string? Message = null)
{
    public bool Ok => Outcome == PackageInstallOutcome.Installed;
}

/// <summary>
/// The one way a course package set gets installed from the UI (lesson button and Settings share it): build the plan, show the packages with
/// versions, the exact command and the network notice, and only a confirmation runs it. Declining runs nothing. Node and Go sets go to their own
/// directory under the node / Go environment root; Python sets go into the ACTIVE managed environment.
/// </summary>
public sealed class PackageSetInstallFlow(AppServices s)
{
    public async Task<PackageInstallResult> InstallAsync(ContentPackageSet set, Action<string> onLine, CancellationToken ct)
    {
        try
        {
            string commandLine, network;
            Func<CancellationToken, Task<(bool Ok, bool Cancelled, string? Error)>> run;
            if (set.Language == PackageSetLanguage.Node)
            {
                var (plan, error) = s.Node.PlanInstall(set);
                if (plan is null) return new(PackageInstallOutcome.Failed, error);
                commandLine = plan.CommandLine;
                network = Loc.T("Sets_NetworkNpm");
                run = async token => { var r = await s.Node.InstallAsync(plan, onLine, token); return (r.Ok, r.Cancelled, r.Error); };
            }
            else if (set.Language == PackageSetLanguage.Go)
            {
                var (plan, error) = s.Go.PlanInstall(set);
                if (plan is null) return new(PackageInstallOutcome.Failed, error);
                commandLine = plan.CommandLine;
                network = Loc.F("Sets_NetworkGo", GoEnvironmentService.Proxy);
                run = async token => { var r = await s.Go.InstallAsync(plan, onLine, token); return (r.Ok, r.Cancelled, r.Error); };
            }
            else
            {
                var (plan, _, error) = await s.Python.PlanSetInstallAsync(set, ct);
                if (plan is null) return new(PackageInstallOutcome.Failed, error);
                commandLine = plan.CommandLine;
                network = Loc.F("Sets_NetworkPypi", Path.GetFileName(plan.EnvironmentPath));
                run = async token => { var r = await s.Python.InstallAsync(plan, onLine, token); return (r.Ok, r.Cancelled, r.Error); };
            }

            var text = new StringBuilder();
            text.AppendLine(Loc.F("Sets_ConfirmIntro", set.Title));
            var pins = s.PackageSets.ReadPins(set);
            foreach (var pin in pins) text.AppendLine($"• {pin.Name} {pin.Version}");
            if (pins.Count == 0) foreach (var name in set.Packages) text.AppendLine("• " + name);
            text.AppendLine().AppendLine(Loc.T("Sets_ConfirmCommand")).AppendLine().AppendLine(commandLine).AppendLine().Append(network);
            if (!await s.Dialogs.ConfirmAsync(Loc.F("Sets_ConfirmTitle", set.Title), text.ToString(), Loc.T("Py_ConfirmButton")))
                return new(PackageInstallOutcome.Declined, Loc.T("Py_InstallDeclined"));

            onLine("$ " + commandLine);
            var result = await run(ct);
            return result.Ok ? new(PackageInstallOutcome.Installed)
                : result.Cancelled ? new(PackageInstallOutcome.Cancelled, Loc.T("Py_InstallCancelled"))
                : new(PackageInstallOutcome.Failed, result.Error);
        }
        catch (OperationCanceledException)
        {
            return new(PackageInstallOutcome.Cancelled, Loc.T("Py_InstallCancelled"));
        }
        catch (Exception ex)
        {
            return new(PackageInstallOutcome.Failed, Loc.F("Py_Error", ex.Message));
        }
    }
}
