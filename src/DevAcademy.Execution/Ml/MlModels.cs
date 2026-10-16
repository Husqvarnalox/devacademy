namespace DevAcademy.Execution;

public enum CapabilityStatus
{
    /// <summary>Found and usable.</summary>
    Available,
    /// <summary>Checked and not usable; the message says why.</summary>
    NotAvailable,
    /// <summary>The framework is not installed in the chosen environment, so there is nothing to probe.</summary>
    NotInstalled,
    /// <summary>Could not be determined (platform not checkable, probe failed).</summary>
    Unknown,
}

/// <summary>One accelerator-related finding with a human-readable, specific message.</summary>
/// <param name="Id">Stable id: "nvidia", "rocm", "metal", "torch.cuda", "torch.mps", "tensorflow", "jax", "onnxruntime".</param>
public sealed record AcceleratorCapability(string Id, string Title, CapabilityStatus Status, string Message)
{
    public bool IsAvailable => Status == CapabilityStatus.Available;
}

public sealed record GpuInfo(string Name, string? DriverVersion, string? MemoryText);

public sealed record HardwareReport(
    string Os,
    string Architecture,
    string CpuModel,
    int CpuCores,
    long RamBytes,
    IReadOnlyList<GpuInfo> NvidiaGpus,
    IReadOnlyList<AcceleratorCapability> Capabilities)
{
    public double RamGiB => Math.Round(RamBytes / 1024d / 1024d / 1024d, 1);
    public bool AnyHardwareAccelerator => Capabilities.Any(c => c.IsAvailable);
}

/// <summary>Facts about the host the report is about. Overridable so tests describe a Linux/NVIDIA or Windows box without owning one.</summary>
public sealed record MlPlatform(
    bool IsWindows,
    bool IsLinux,
    bool IsMacOS,
    string Architecture,
    string OsDescription,
    string CpuModel,
    int CpuCores,
    long RamBytes)
{
    public static MlPlatform Current(IToolRunner runner)
    {
        bool mac = OperatingSystem.IsMacOS();
        bool win = OperatingSystem.IsWindows();
        bool linux = OperatingSystem.IsLinux();
        string arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant() switch
        {
            "x64" => "x86_64",
            var a => a,
        };
        string cpu = "unknown";
        try
        {
            if (mac && runner.Find("sysctl") is { } sysctl)
            {
                ToolOutput o = runner.RunAsync(sysctl, ["-n", "machdep.cpu.brand_string"], new ToolRunOptions(Timeout: TimeSpan.FromSeconds(5)), CancellationToken.None).GetAwaiter().GetResult();
                if (o.Succeeded && o.Stdout.Trim().Length > 0) cpu = o.Stdout.Trim();
            }
            else if (linux && File.Exists("/proc/cpuinfo"))
            {
                string? line = File.ReadLines("/proc/cpuinfo").FirstOrDefault(l => l.StartsWith("model name", StringComparison.Ordinal));
                if (line is not null && line.IndexOf(':') is var i and >= 0) cpu = line[(i + 1)..].Trim();
            }
            else if (win && Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") is { Length: > 0 } pid)
            {
                cpu = pid;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return new MlPlatform(win, linux, mac, arch, System.Runtime.InteropServices.RuntimeInformation.OSDescription, cpu,
            Environment.ProcessorCount, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes);
    }
}

/// <summary>A note shown before installing an accelerator-sensitive package. Informational only: it never blocks or alters the install.</summary>
public sealed record PackageAcceleratorNote(string Package, string Text);
