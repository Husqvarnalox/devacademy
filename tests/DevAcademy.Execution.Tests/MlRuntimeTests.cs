using DevAcademy.Execution;

namespace DevAcademy.Execution.Tests;

public class MlRuntimeTests
{
    private static MlPlatform Linux => new(false, true, false, "x64", "Linux 6.8", "EPYC", 16, 64L << 30);
    private static MlPlatform Windows => new(true, false, false, "x64", "Windows 11", "Ryzen", 8, 32L << 30);
    private static MlPlatform Mac => new(false, false, true, "arm64", "macOS 15", "Apple M3", 8, 16L << 30);

    private static AcceleratorCapability Cap(HardwareReport r, string id) => r.Capabilities.Single(c => c.Id == id);

    [Fact]
    public async Task Nvidia_smi_output_gives_available_with_gpu_details()
    {
        var fake = new FakeToolRunner();
        fake.Tools["nvidia-smi"] = "/usr/bin/nvidia-smi";
        fake.Handler = (_, _) => new ToolOutput(0, "NVIDIA GeForce RTX 4090, 550.54.14, 24564 MiB\n", "");
        HardwareReport r = await new MlRuntimeManager(fake, Linux).DetectHardwareAsync(CancellationToken.None);
        Assert.Equal(CapabilityStatus.Available, Cap(r, "nvidia").Status);
        Assert.Contains("RTX 4090", Cap(r, "nvidia").Message);
        Assert.Single(r.NvidiaGpus);
        Assert.Equal(64, r.RamGiB);
        Assert.Equal(CapabilityStatus.NotAvailable, Cap(r, "metal").Status);
    }

    [Fact]
    public async Task Missing_or_failing_nvidia_smi_is_not_available_with_a_reason()
    {
        var none = new FakeToolRunner();
        HardwareReport a = await new MlRuntimeManager(none, Linux).DetectHardwareAsync(CancellationToken.None);
        Assert.Equal(CapabilityStatus.NotAvailable, Cap(a, "nvidia").Status);
        Assert.Contains("nvidia-smi", Cap(a, "nvidia").Message);

        var bad = new FakeToolRunner();
        bad.Tools["nvidia-smi"] = "/x/nvidia-smi";
        bad.Handler = (_, _) => new ToolOutput(9, "", "NVIDIA-SMI has failed");
        HardwareReport b = await new MlRuntimeManager(bad, Linux).DetectHardwareAsync(CancellationToken.None);
        Assert.Equal(CapabilityStatus.NotAvailable, Cap(b, "nvidia").Status);
    }

    [Fact]
    public async Task Macos_never_runs_nvidia_smi_and_reports_metal_on_apple_silicon()
    {
        var fake = new FakeToolRunner();
        fake.Tools["nvidia-smi"] = "/x/nvidia-smi";
        HardwareReport r = await new MlRuntimeManager(fake, Mac).DetectHardwareAsync(CancellationToken.None);
        Assert.Empty(fake.Calls);
        Assert.Equal(CapabilityStatus.NotAvailable, Cap(r, "nvidia").Status);
        Assert.Equal(CapabilityStatus.Available, Cap(r, "metal").Status);
        Assert.Equal(CapabilityStatus.NotAvailable, Cap(r, "rocm").Status);
    }

    [Fact]
    public async Task Rocm_is_linux_only_and_windows_says_to_verify()
    {
        var rocmBox = new FakeToolRunner();
        rocmBox.Tools["rocminfo"] = "/opt/rocm/bin/rocminfo";
        rocmBox.Handler = (_, _) => new ToolOutput(0, "Agent 2\n  Name:                    gfx1100\n  Marketing Name:          AMD Radeon RX 7900 XTX\n  Device Type:             GPU\n", "");
        HardwareReport lin = await new MlRuntimeManager(rocmBox, Linux).DetectHardwareAsync(CancellationToken.None);
        Assert.Equal(CapabilityStatus.Available, Cap(lin, "rocm").Status);

        var win = new FakeToolRunner();
        win.Tools["rocminfo"] = "C:/x/rocminfo";
        HardwareReport w = await new MlRuntimeManager(win, Windows).DetectHardwareAsync(CancellationToken.None);
        Assert.Equal(CapabilityStatus.Unknown, Cap(w, "rocm").Status);
        Assert.Contains("ROCm on Windows: limited/HIP SDK", Cap(w, "rocm").Message);
        Assert.DoesNotContain(win.Calls, c => c.File.Contains("rocminfo"));
    }

    [Fact]
    public void Parsers_handle_garbage()
    {
        Assert.Empty(MlRuntimeManager.ParseNvidiaSmi(""));
        Assert.Empty(MlRuntimeManager.ParseNvidiaSmi("No devices were found"));
        Assert.Null(MlRuntimeManager.ParseRocminfoGpu("nothing here"));
    }

    private static FakeToolRunner FrameworkFake(string torchJson)
    {
        var fake = new FakeToolRunner();
        fake.Handler = (_, args) =>
        {
            string script = args.Count > 2 ? args[2] : "";
            if (script.Contains("find_spec('torch')")) return new ToolOutput(0, torchJson + "\n", "");
            return new ToolOutput(0, "{\"installed\":false}\n", "");
        };
        return fake;
    }

    [Fact]
    public async Task Gpu_present_but_torch_built_without_cuda_says_so()
    {
        var hw = new HardwareReport("Linux", "x64", "c", 8, 0, [new GpuInfo("RTX", null, null)],
            [new("nvidia", "NVIDIA CUDA", CapabilityStatus.Available, "ok")]);
        var mgr = new MlRuntimeManager(FrameworkFake("""{"installed":true,"version":"2.3.0","cuda_available":false,"cuda_version":null,"hip_version":null,"mps":false}"""), Linux);
        IReadOnlyList<AcceleratorCapability> caps = await mgr.ProbeFrameworksAsync("/py", hw, CancellationToken.None);
        AcceleratorCapability cuda = caps.Single(c => c.Id == "torch.cuda");
        Assert.Equal(CapabilityStatus.NotAvailable, cuda.Status);
        Assert.Contains("GPU найден, но установленный torch 2.3.0 собран без CUDA", cuda.Message);
        Assert.Equal(CapabilityStatus.NotInstalled, caps.Single(c => c.Id == "tensorflow").Status);
        Assert.Equal(CapabilityStatus.NotInstalled, caps.Single(c => c.Id == "jax").Status);
        Assert.Equal(CapabilityStatus.NotInstalled, caps.Single(c => c.Id == "onnxruntime").Status);
    }

    [Fact]
    public async Task Working_cuda_and_mps_are_available_and_a_crashing_probe_is_unknown()
    {
        var ok = new MlRuntimeManager(FrameworkFake("""{"installed":true,"version":"2.3.0","cuda_available":true,"cuda_version":"12.1","hip_version":null,"mps":true}"""), Linux);
        IReadOnlyList<AcceleratorCapability> caps = await ok.ProbeFrameworksAsync("/py", null, CancellationToken.None);
        Assert.Equal(CapabilityStatus.Available, caps.Single(c => c.Id == "torch.cuda").Status);
        Assert.Equal(CapabilityStatus.Available, caps.Single(c => c.Id == "torch.mps").Status);

        var crash = new FakeToolRunner { Handler = (_, _) => new ToolOutput(1, "", "ImportError: libcudnn.so") };
        caps = await new MlRuntimeManager(crash, Linux).ProbeFrameworksAsync("/py", null, CancellationToken.None);
        Assert.All(caps, c => Assert.Equal(CapabilityStatus.Unknown, c.Status));
    }

    [Fact]
    public void Package_notes_exist_for_accelerator_sensitive_packages_only()
    {
        var mgr = new MlRuntimeManager(new FakeToolRunner(), Mac);
        Assert.True(MlRuntimeManager.IsAcceleratorSensitive("torch"));
        Assert.True(MlRuntimeManager.IsAcceleratorSensitive("Torch"));
        Assert.False(MlRuntimeManager.IsAcceleratorSensitive("numpy"));
        Assert.Empty(mgr.DescribePackages(["numpy", "requests"], null));
        Assert.NotEmpty(mgr.DescribePackages(["torch"], null));
    }

    [Fact]
    public async Task Real_local_probe_reports_this_machine_honestly()
    {
        var mgr = new MlRuntimeManager(new SystemToolRunner());
        HardwareReport r = await mgr.DetectHardwareAsync(CancellationToken.None);
        Assert.True(r.CpuCores >= 1);
        Assert.True(r.RamBytes > 0);
        Assert.NotEmpty(r.Architecture);
        if (OperatingSystem.IsMacOS())
        {
            Assert.Equal(CapabilityStatus.NotAvailable, Cap(r, "nvidia").Status);   // never assumed on macOS
            if (r.Architecture.Contains("arm", StringComparison.OrdinalIgnoreCase))
                Assert.Equal(CapabilityStatus.Available, Cap(r, "metal").Status);
        }
        RuntimeStatus py = await TestSupport.StatusAsync("python");
        if (py.Availability != RuntimeAvailability.Available) return;
        IReadOnlyList<AcceleratorCapability> fw = await mgr.ProbeFrameworksAsync(py.ExecutablePath!, r, CancellationToken.None);
        Assert.Equal(5, fw.Count);
        // The Homebrew system python has no ML frameworks; if a developer has them, the statuses are just different, not wrong.
        Assert.All(fw, c => Assert.NotEqual(CapabilityStatus.Unknown, c.Status));
    }
}
