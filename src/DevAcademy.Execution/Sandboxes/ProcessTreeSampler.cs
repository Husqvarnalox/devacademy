using System.Diagnostics;
using System.Globalization;

namespace DevAcademy.Execution;

/// <summary>Resident memory and member pids of a process tree.</summary>
internal sealed record TreeSample(long RssBytes, IReadOnlyList<int> Pids);

internal static class ProcessTreeSampler
{
    public static async Task<TreeSample?> SampleAsync(Process root, CancellationToken ct)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                root.Refresh();
                return root.HasExited ? null : new TreeSample(root.WorkingSet64, [root.Id]);
            }

            Dictionary<int, (int Ppid, long RssBytes)> table = OperatingSystem.IsLinux()
                ? ReadProc()
                : await ReadPsAsync(ct).ConfigureAwait(false);
            return Walk(table, root.Id);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException
                                       or System.ComponentModel.Win32Exception or FormatException)
        {
            return null; // sampling is best effort; the wall timeout still applies
        }
    }

    internal static TreeSample? Walk(Dictionary<int, (int Ppid, long RssBytes)> table, int rootPid)
    {
        if (!table.ContainsKey(rootPid)) return null;
        var children = table.GroupBy(kv => kv.Value.Ppid).ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).ToList());
        var pids = new List<int>();
        var stack = new Stack<int>();
        stack.Push(rootPid);
        long rss = 0;
        while (stack.Count > 0)
        {
            int pid = stack.Pop();
            pids.Add(pid);
            rss += table[pid].RssBytes;
            if (children.TryGetValue(pid, out var kids))
                foreach (int k in kids) stack.Push(k);
        }
        return new TreeSample(rss, pids);
    }

    /// <summary>Parses "pid ppid rss(KiB)" lines as printed by <c>ps -A -o pid=,ppid=,rss=</c>.</summary>
    internal static Dictionary<int, (int Ppid, long RssBytes)> ParsePs(string output)
    {
        var table = new Dictionary<int, (int, long)>();
        foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] f = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (f.Length < 3) continue;
            table[int.Parse(f[0], CultureInfo.InvariantCulture)] =
                (int.Parse(f[1], CultureInfo.InvariantCulture), long.Parse(f[2], CultureInfo.InvariantCulture) * 1024);
        }
        return table;
    }

    private static async Task<Dictionary<int, (int, long)>> ReadPsAsync(CancellationToken ct)
    {
        var psi = new ProcessStartInfo("/bin/ps")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            // Captured so a cancelled sample doesn't print "ps: stdout: Broken pipe" to the host console.
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-A");
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add("pid=,ppid=,rss=");
        using var ps = Process.Start(psi) ?? throw new InvalidOperationException("ps did not start");
        using var _ = ct.Register(() => { try { ps.Kill(); } catch (InvalidOperationException) { } });
        var stderr = ps.StandardError.ReadToEndAsync(CancellationToken.None);
        string output = await ps.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        await ps.WaitForExitAsync(ct).ConfigureAwait(false);
        await stderr.ConfigureAwait(false);
        return ParsePs(output);
    }

    private static Dictionary<int, (int, long)> ReadProc()
    {
        const long PageSize = 4096;
        var table = new Dictionary<int, (int, long)>();
        foreach (string dir in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(dir), NumberStyles.None, CultureInfo.InvariantCulture, out int pid)) continue;
            try
            {
                string stat = File.ReadAllText(Path.Combine(dir, "stat"));
                // Format: pid (comm) state ppid ... ; comm may contain spaces/parens, so split after the last ')'.
                string[] rest = stat[(stat.LastIndexOf(')') + 2)..].Split(' ');
                int ppid = int.Parse(rest[1], CultureInfo.InvariantCulture);
                string[] statm = File.ReadAllText(Path.Combine(dir, "statm")).Split(' ');
                table[pid] = (ppid, long.Parse(statm[1], CultureInfo.InvariantCulture) * PageSize);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // process exited between enumeration and read
            }
        }
        return table;
    }
}
