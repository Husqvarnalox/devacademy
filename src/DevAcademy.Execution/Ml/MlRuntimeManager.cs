using System.Text.Json;

namespace DevAcademy.Execution;

/// <summary>
/// ML Runtime Manager: reports what accelerator hardware the machine has and what the frameworks inside a chosen Python environment can use.
/// Hardware detection only runs vendor tools that are already installed (nvidia-smi, rocminfo); framework probes run inside the given
/// environment, and only for frameworks that environment actually contains. Nothing is installed or downloaded here.
/// </summary>
public sealed class MlRuntimeManager(IToolRunner runner, MlPlatform? platform = null)
{
    // Lazy: reading the CPU model may run a tool (sysctl), which must not happen while the DI container is built.
    private readonly Lazy<MlPlatform> _lazyPlatform = new(() => platform ?? MlPlatform.Current(runner));

    private MlPlatform _platform => _lazyPlatform.Value;

    public MlPlatform Platform => _platform;

    // ---------------------------------------------------------------- hardware

    public async Task<HardwareReport> DetectHardwareAsync(CancellationToken ct)
    {
        var gpus = new List<GpuInfo>();
        AcceleratorCapability nvidia = await DetectNvidiaAsync(gpus, ct).ConfigureAwait(false);
        AcceleratorCapability rocm = await DetectRocmAsync(ct).ConfigureAwait(false);
        AcceleratorCapability metal = DetectMetal();
        return new HardwareReport(_platform.OsDescription, _platform.Architecture, _platform.CpuModel, _platform.CpuCores, _platform.RamBytes,
            gpus, [nvidia, rocm, metal]);
    }

    private async Task<AcceleratorCapability> DetectNvidiaAsync(List<GpuInfo> gpus, CancellationToken ct)
    {
        const string title = "NVIDIA CUDA";
        if (_platform.IsMacOS)
            return new("nvidia", title, CapabilityStatus.NotAvailable, "macOS: NVIDIA CUDA не поддерживается, nvidia-smi не запускался.");
        string? smi = runner.Find("nvidia-smi");
        if (smi is null)
            return new("nvidia", title, CapabilityStatus.NotAvailable, "nvidia-smi не найден: драйвер NVIDIA не установлен или видеокарты NVIDIA нет.");
        ToolOutput o = await runner.RunAsync(smi, ["--query-gpu=name,driver_version,memory.total", "--format=csv,noheader"],
            new ToolRunOptions(Timeout: TimeSpan.FromSeconds(15)), ct).ConfigureAwait(false);
        if (!o.Succeeded)
            return new("nvidia", title, CapabilityStatus.NotAvailable, $"nvidia-smi найден, но не отработал: {Short(o.FailureText)}");
        gpus.AddRange(ParseNvidiaSmi(o.Stdout));
        if (gpus.Count == 0)
            return new("nvidia", title, CapabilityStatus.NotAvailable, "nvidia-smi не сообщил ни одной видеокарты.");
        GpuInfo first = gpus[0];
        string more = gpus.Count > 1 ? $" (+{gpus.Count - 1})" : "";
        return new("nvidia", title, CapabilityStatus.Available,
            $"{first.Name}{more}, драйвер {first.DriverVersion ?? "?"}, память {first.MemoryText ?? "?"}.");
    }

    /// <summary>Parses <c>nvidia-smi --query-gpu=name,driver_version,memory.total --format=csv,noheader</c>.</summary>
    public static IReadOnlyList<GpuInfo> ParseNvidiaSmi(string stdout)
    {
        var result = new List<GpuInfo>();
        foreach (string raw in stdout.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            string[] parts = line.Split(',').Select(p => p.Trim()).ToArray();
            // Real rows are "name, driver, memory"; a message such as "No devices were found" has no commas and is not a GPU.
            if (parts.Length < 2 || parts[0].Length == 0) continue;
            result.Add(new GpuInfo(parts[0], parts.Length > 1 ? parts[1] : null, parts.Length > 2 ? parts[2] : null));
        }
        return result;
    }

    private async Task<AcceleratorCapability> DetectRocmAsync(CancellationToken ct)
    {
        const string title = "AMD ROCm";
        if (_platform.IsMacOS)
            return new("rocm", title, CapabilityStatus.NotAvailable, "macOS: ROCm не поддерживается.");
        if (_platform.IsWindows)
            return new("rocm", title, CapabilityStatus.Unknown, "ROCm on Windows: limited/HIP SDK — verify (автоматическая проверка не выполняется).");
        string? rocminfo = runner.Find("rocminfo");
        string? smi = runner.Find("rocm-smi");
        if (rocminfo is null && smi is null)
            return new("rocm", title, CapabilityStatus.NotAvailable, "rocminfo и rocm-smi не найдены: ROCm не установлен или видеокарты AMD нет.");
        if (rocminfo is not null)
        {
            ToolOutput o = await runner.RunAsync(rocminfo, [], new ToolRunOptions(Timeout: TimeSpan.FromSeconds(20)), ct).ConfigureAwait(false);
            if (o.Succeeded)
            {
                string? gpu = ParseRocminfoGpu(o.Stdout);
                return gpu is null
                    ? new("rocm", title, CapabilityStatus.NotAvailable, "rocminfo отработал, но GPU-агентов (gfx*) не нашёл.")
                    : new("rocm", title, CapabilityStatus.Available, $"GPU-агент {gpu}.");
            }
        }
        if (smi is not null)
        {
            ToolOutput o = await runner.RunAsync(smi, ["--showproductname"], new ToolRunOptions(Timeout: TimeSpan.FromSeconds(20)), ct).ConfigureAwait(false);
            if (o.Succeeded && o.Stdout.Contains("GPU", StringComparison.OrdinalIgnoreCase))
                return new("rocm", title, CapabilityStatus.Available, "rocm-smi видит GPU AMD.");
            return new("rocm", title, CapabilityStatus.NotAvailable, $"rocm-smi не нашёл GPU: {Short(o.FailureText)}");
        }
        return new("rocm", title, CapabilityStatus.NotAvailable, "rocminfo не отработал.");
    }

    /// <summary>First <c>gfxNNNN</c> agent name in rocminfo output (the CPU agents have no gfx name).</summary>
    public static string? ParseRocminfoGpu(string stdout)
    {
        foreach (string raw in stdout.Split('\n'))
        {
            string line = raw.Trim();
            if (!line.StartsWith("Name:", StringComparison.Ordinal)) continue;
            string value = line["Name:".Length..].Trim();
            if (value.StartsWith("gfx", StringComparison.Ordinal)) return value;
        }
        return null;
    }

    private AcceleratorCapability DetectMetal()
    {
        const string title = "Apple Metal (MPS)";
        if (!_platform.IsMacOS)
            return new("metal", title, CapabilityStatus.NotAvailable, "Metal — только macOS.");
        bool arm = _platform.Architecture.Equals("arm64", StringComparison.OrdinalIgnoreCase);
        return arm
            ? new("metal", title, CapabilityStatus.Available, $"Apple Silicon ({_platform.CpuModel}): Metal доступен; PyTorch использует его как устройство «mps».")
            : new("metal", title, CapabilityStatus.Unknown, "Intel Mac: Metal есть, но ускорение для ML-фреймворков ограничено — проверьте в окружении.");
    }

    // ---------------------------------------------------------------- frameworks (inside an environment)

    private const string TorchScript =
        "import json,importlib.util\n" +
        "if importlib.util.find_spec('torch') is None:\n print(json.dumps({'installed':False})); raise SystemExit\n" +
        "import torch\n" +
        "mps=False\n" +
        "try: mps=bool(torch.backends.mps.is_available())\n" +
        "except Exception: pass\n" +
        "print(json.dumps({'installed':True,'version':torch.__version__,'cuda_available':bool(torch.cuda.is_available())," +
        "'cuda_version':torch.version.cuda,'hip_version':getattr(torch.version,'hip',None),'mps':mps}))";

    private const string TensorflowScript =
        "import json,importlib.util\n" +
        "if importlib.util.find_spec('tensorflow') is None:\n print(json.dumps({'installed':False})); raise SystemExit\n" +
        "import tensorflow as tf\n" +
        "print(json.dumps({'installed':True,'version':tf.__version__,'gpus':[d.name for d in tf.config.list_physical_devices('GPU')]}))";

    private const string JaxScript =
        "import json,importlib.util\n" +
        "if importlib.util.find_spec('jax') is None:\n print(json.dumps({'installed':False})); raise SystemExit\n" +
        "import jax\n" +
        "print(json.dumps({'installed':True,'version':jax.__version__,'devices':[str(d) for d in jax.devices()],'platforms':sorted({d.platform for d in jax.devices()})}))";

    private const string OnnxScript =
        "import json,importlib.util\n" +
        "if importlib.util.find_spec('onnxruntime') is None:\n print(json.dumps({'installed':False})); raise SystemExit\n" +
        "import onnxruntime as ort\n" +
        "print(json.dumps({'installed':True,'version':ort.__version__,'providers':list(ort.get_available_providers())}))";

    /// <summary>
    /// Probes torch, tensorflow, jax and onnxruntime inside <paramref name="interpreterPath"/>, each in its own short-lived process, and
    /// only reports on a framework that is installed there. <paramref name="hardware"/> lets the message distinguish "no GPU" from
    /// "GPU present but this build cannot use it".
    /// </summary>
    public async Task<IReadOnlyList<AcceleratorCapability>> ProbeFrameworksAsync(string interpreterPath, HardwareReport? hardware, CancellationToken ct)
    {
        bool nvidia = hardware?.Capabilities.Any(c => c.Id == "nvidia" && c.IsAvailable) ?? false;
        bool rocm = hardware?.Capabilities.Any(c => c.Id == "rocm" && c.IsAvailable) ?? false;
        bool metal = hardware?.Capabilities.Any(c => c.Id == "metal" && c.IsAvailable) ?? false;

        var tasks = new[]
        {
            RunProbeAsync(interpreterPath, TorchScript, ct),
            RunProbeAsync(interpreterPath, TensorflowScript, ct),
            RunProbeAsync(interpreterPath, JaxScript, ct),
            RunProbeAsync(interpreterPath, OnnxScript, ct),
        };
        JsonElement?[] r = await Task.WhenAll(tasks).ConfigureAwait(false);
        var list = new List<AcceleratorCapability>();
        list.AddRange(DescribeTorch(r[0], nvidia, rocm, metal));
        list.Add(DescribeTensorflow(r[1], nvidia));
        list.Add(DescribeJax(r[2]));
        list.Add(DescribeOnnx(r[3]));
        return list;
    }

    private async Task<JsonElement?> RunProbeAsync(string python, string script, CancellationToken ct)
    {
        ToolOutput o = await runner.RunAsync(python, ["-I", "-c", script], new ToolRunOptions(Timeout: TimeSpan.FromSeconds(90)), ct).ConfigureAwait(false);
        if (!o.Succeeded)
        {
            // A framework that imports badly (broken wheel, missing shared library) is reported as unknown, not as absent.
            using var d = JsonDocument.Parse(JsonSerializer.Serialize(new { error = Short(o.FailureText) }));
            return d.RootElement.Clone();
        }
        try
        {
            string line = o.Stdout.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.StartsWith('{')) ?? "";
            using var doc = JsonDocument.Parse(line);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            using var d = JsonDocument.Parse(JsonSerializer.Serialize(new { error = "непонятный ответ зонда" }));
            return d.RootElement.Clone();
        }
    }

    private static bool IsNotInstalled(JsonElement? e) =>
        e is { } x && x.TryGetProperty("installed", out var i) && i.ValueKind == JsonValueKind.False;

    private static string? ErrorOf(JsonElement? e) =>
        e is { } x && x.TryGetProperty("error", out var er) ? er.GetString() : null;

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";

    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;

    private static IReadOnlyList<string> StrList(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Array
            ? p.EnumerateArray().Select(x => x.GetString() ?? "").ToList()
            : [];

    private static AcceleratorCapability Failed(string id, string title, string error) =>
        new(id, title, CapabilityStatus.Unknown, $"{title}: установлен, но проверка не удалась — {error}");

    private static AcceleratorCapability Missing(string id, string title, string package) =>
        new(id, title, CapabilityStatus.NotInstalled, $"{package} не установлен в выбранном окружении.");

    private static IEnumerable<AcceleratorCapability> DescribeTorch(JsonElement? r, bool nvidia, bool rocm, bool metal)
    {
        const string cudaTitle = "PyTorch: CUDA/ROCm";
        const string mpsTitle = "PyTorch: Metal (MPS)";
        if (IsNotInstalled(r))
        {
            yield return Missing("torch.cuda", cudaTitle, "torch");
            yield return Missing("torch.mps", mpsTitle, "torch");
            yield break;
        }
        if (ErrorOf(r) is { } err)
        {
            yield return Failed("torch.cuda", cudaTitle, err);
            yield return Failed("torch.mps", mpsTitle, err);
            yield break;
        }
        JsonElement e = r!.Value;
        string version = Str(e, "version");
        string cudaVersion = Str(e, "cuda_version");
        string hipVersion = Str(e, "hip_version");
        bool cudaOk = Bool(e, "cuda_available");
        bool mps = Bool(e, "mps");

        if (cudaOk)
        {
            string backend = hipVersion.Length > 0 ? $"ROCm/HIP {hipVersion}" : $"CUDA {cudaVersion}";
            yield return new("torch.cuda", cudaTitle, CapabilityStatus.Available, $"torch {version}: GPU доступен через {backend}.");
        }
        else
        {
            bool builtWithGpu = cudaVersion.Length > 0 || hipVersion.Length > 0;
            string msg;
            if ((nvidia || rocm) && !builtWithGpu)
                msg = $"GPU найден, но установленный torch {version} собран без CUDA (CPU-сборка). Установите сборку torch с поддержкой вашего GPU.";
            else if (builtWithGpu)
                msg = $"torch {version} собран с поддержкой {(cudaVersion.Length > 0 ? "CUDA " + cudaVersion : "ROCm/HIP " + hipVersion)}, но GPU недоступен: проверьте драйвер и совместимость версий.";
            else
                msg = $"torch {version}: GPU (CUDA/ROCm) недоступен; вычисления идут на CPU.";
            yield return new("torch.cuda", cudaTitle, CapabilityStatus.NotAvailable, msg);
        }

        yield return mps
            ? new("torch.mps", mpsTitle, CapabilityStatus.Available, $"torch {version}: устройство «mps» (Metal) доступно.")
            : new("torch.mps", mpsTitle, CapabilityStatus.NotAvailable,
                metal ? $"Metal на этой машине есть, но torch {version} сообщает, что MPS недоступен (сборка без MPS или устаревшая macOS)."
                      : $"torch {version}: MPS недоступен (это не Apple Silicon или сборка без MPS).");
    }

    private static AcceleratorCapability DescribeTensorflow(JsonElement? r, bool nvidia)
    {
        const string id = "tensorflow";
        const string title = "TensorFlow";
        if (IsNotInstalled(r)) return Missing(id, title, "tensorflow");
        if (ErrorOf(r) is { } err) return Failed(id, title, err);
        JsonElement e = r!.Value;
        IReadOnlyList<string> gpus = StrList(e, "gpus");
        string version = Str(e, "version");
        if (gpus.Count > 0)
            return new(id, title, CapabilityStatus.Available, $"tensorflow {version}: видит GPU ({string.Join(", ", gpus)}).");
        return new(id, title, CapabilityStatus.NotAvailable, nvidia
            ? $"GPU найден, но tensorflow {version} его не видит (нужна сборка с CUDA и совместимые драйвер/cuDNN)."
            : $"tensorflow {version}: GPU не обнаружен; вычисления идут на CPU.");
    }

    private static AcceleratorCapability DescribeJax(JsonElement? r)
    {
        const string id = "jax";
        const string title = "JAX";
        if (IsNotInstalled(r)) return Missing(id, title, "jax");
        if (ErrorOf(r) is { } err) return Failed(id, title, err);
        JsonElement e = r!.Value;
        IReadOnlyList<string> platforms = StrList(e, "platforms");
        string version = Str(e, "version");
        string[] accel = platforms.Where(p => !p.Equals("cpu", StringComparison.OrdinalIgnoreCase)).ToArray();
        return accel.Length > 0
            ? new(id, title, CapabilityStatus.Available, $"jax {version}: устройства {string.Join(", ", StrList(e, "devices"))}.")
            : new(id, title, CapabilityStatus.NotAvailable, $"jax {version}: доступен только CPU ({string.Join(", ", StrList(e, "devices"))}).");
    }

    private static AcceleratorCapability DescribeOnnx(JsonElement? r)
    {
        const string id = "onnxruntime";
        const string title = "ONNX Runtime";
        if (IsNotInstalled(r)) return Missing(id, title, "onnxruntime");
        if (ErrorOf(r) is { } err) return Failed(id, title, err);
        JsonElement e = r!.Value;
        IReadOnlyList<string> providers = StrList(e, "providers");
        string version = Str(e, "version");
        string[] accel = providers.Where(p => !p.StartsWith("CPU", StringComparison.OrdinalIgnoreCase) && !p.StartsWith("Azure", StringComparison.OrdinalIgnoreCase)).ToArray();
        return accel.Length > 0
            ? new(id, title, CapabilityStatus.Available, $"onnxruntime {version}: провайдеры {string.Join(", ", providers)}.")
            : new(id, title, CapabilityStatus.NotAvailable, $"onnxruntime {version}: только CPUExecutionProvider (ускорители не подключены).");
    }

    // ---------------------------------------------------------------- pre-install notes

    private static readonly HashSet<string> SensitiveExact = new(StringComparer.OrdinalIgnoreCase)
    {
        "torch", "torchvision", "torchaudio", "tensorflow", "tensorflow-gpu", "tensorflow-cpu", "tensorflow-macos", "tensorflow-metal",
        "jax", "jaxlib", "onnxruntime", "onnxruntime-gpu", "onnxruntime-directml", "onnxruntime-rocm", "cupy", "xformers", "bitsandbytes",
        "flash-attn", "mxnet", "paddlepaddle", "paddlepaddle-gpu",
    };

    public static bool IsAcceleratorSensitive(string packageName)
    {
        string n = packageName.Trim().Replace('_', '-');
        return SensitiveExact.Contains(n) || n.StartsWith("cupy-", StringComparison.OrdinalIgnoreCase) || n.StartsWith("nvidia-", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Informational notes for the install confirmation: which of these packages depend on the accelerator, and what this machine offers.
    /// Deliberately has no version table (which wheel fits which driver changes monthly); it points to the vendor's own selector.
    /// </summary>
    public IReadOnlyList<PackageAcceleratorNote> DescribePackages(IEnumerable<string> packageNames, HardwareReport? hardware)
    {
        string machine = DescribeMachine(hardware);
        return packageNames.Where(IsAcceleratorSensitive).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(p => new PackageAcceleratorNote(p, $"{p}: сборка зависит от ускорителя. {machine} Совместимую сборку выбирайте по официальному селектору на сайте проекта; приложение версию не подбирает. "
                .TrimEnd(' ', ' ')))
            .ToList();
    }

    private string DescribeMachine(HardwareReport? hw)
    {
        if (hw is null) return "Оборудование не проверялось (раздел «ML-ускорение»).";
        var found = hw.Capabilities.Where(c => c.IsAvailable).Select(c => c.Title).ToList();
        return found.Count > 0
            ? "На этой машине доступно: " + string.Join(", ", found) + "."
            : "Ускорители на этой машине не обнаружены — будет использоваться CPU.";
    }

    private static string Short(string s) => s.Length > 300 ? s[..300] + "…" : s;
}
