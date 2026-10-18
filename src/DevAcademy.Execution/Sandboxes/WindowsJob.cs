using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DevAcademy.Execution;

/// <summary>
/// A Windows Job Object holding a run's process tree: <see cref="Terminate"/> kills the root and every descendant, including orphans
/// that were reparented (Windows has no process groups, and a tree walk cannot see processes whose parent already exited).
/// Closing the handle kills whatever is left (<c>KILL_ON_JOB_CLOSE</c>), so nothing outlives the run even if the host crashes.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsJob : IDisposable
{
    private const int ExtendedLimitInformation = 9;
    private const uint KillOnJobClose = 0x2000;

    private IntPtr _handle;

    private WindowsJob(IntPtr handle) => _handle = handle;

    /// <summary>Creates a job and puts <paramref name="process"/> in it; null when the OS refuses (the caller falls back to the tree walk).</summary>
    public static WindowsJob? TryCreate(Process process)
    {
        IntPtr job = CreateJobObjectW(IntPtr.Zero, null);
        if (job == IntPtr.Zero) return null;
        var info = new ExtendedLimits { Basic = new BasicLimits { LimitFlags = KillOnJobClose } };
        bool ok = SetInformationJobObject(job, ExtendedLimitInformation, ref info, (uint)Marshal.SizeOf<ExtendedLimits>());
        try { ok = ok && AssignProcessToJobObject(job, process.Handle); }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { ok = false; } // already exited
        if (ok) return new WindowsJob(job);
        CloseHandle(job);
        return null;
    }

    public void Terminate()
    {
        IntPtr h = _handle;
        if (h != IntPtr.Zero) TerminateJobObject(h, 1);
    }

    public void Dispose()
    {
        IntPtr h = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (h != IntPtr.Zero) CloseHandle(h);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimits
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimits
    {
        public BasicLimits Basic;
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount;
        public nuint ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObjectW(IntPtr attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref ExtendedLimits info, uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateJobObject(IntPtr job, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
