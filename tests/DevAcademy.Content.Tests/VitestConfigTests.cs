namespace DevAcademy.Content.Tests;

public class VitestConfigTests
{
    /// <summary>
    /// Exercises run with the node content environment mounted read-only as node_modules. Vitest's defaults write the Vite cache and
    /// its module cache into node_modules, which the OS sandbox refuses (EPERM) on a freshly installed environment. Every shipped
    /// config must keep both caches inside the work directory.
    /// </summary>
    [Fact]
    public void Every_vitest_config_keeps_caches_out_of_node_modules()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "content"));
        var configs = Directory.EnumerateFiles(root, "vitest.config.*", SearchOption.AllDirectories).ToList();
        Assert.NotEmpty(configs);
        var bad = configs
            .Where(f => File.ReadAllText(f) is var text && !(text.Contains("fsModuleCache: false") && text.Contains("cacheDir:")))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();
        Assert.True(bad.Count == 0, "vitest configs that write into node_modules: " + string.Join(", ", bad));
    }
}
