using System.Text;

namespace DevAcademy.Infrastructure.Workspace;

public sealed record SearchHit(string FullPath, string RelativePath, int Line, int Column, string LineText);

public sealed record SearchOptions(bool CaseSensitive = false, bool ShowIgnored = false, int MaxHits = 2000, long MaxFileBytes = 2 * 1024 * 1024);

/// <summary>File-name and text search over a project. Text search streams hits, can be cancelled, and skips binary and oversized files.</summary>
public static class WorkspaceSearch
{
    public static IReadOnlyList<string> FindFiles(string root, string query, bool showIgnored, int max, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var q = query.Trim();
        var hits = new List<(string Rel, int Rank)>();
        foreach (var file in ProjectTree.EnumerateFiles(root, showIgnored, ct))
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            var name = Path.GetFileName(file);
            var rank = name.Equals(q, StringComparison.OrdinalIgnoreCase) ? 0
                : name.StartsWith(q, StringComparison.OrdinalIgnoreCase) ? 1
                : name.Contains(q, StringComparison.OrdinalIgnoreCase) ? 2
                : rel.Contains(q, StringComparison.OrdinalIgnoreCase) ? 3 : -1;
            if (rank >= 0) hits.Add((rel, rank));
        }
        return hits.OrderBy(h => h.Rank).ThenBy(h => h.Rel.Length).ThenBy(h => h.Rel, StringComparer.Ordinal).Take(max).Select(h => h.Rel).ToList();
    }

    /// <summary>Searches file contents line by line; <paramref name="onHit"/> is called from the worker thread.</summary>
    public static Task<int> SearchTextAsync(string root, string query, SearchOptions options, Action<SearchHit> onHit, CancellationToken ct) =>
        Task.Run(() =>
        {
            if (string.IsNullOrEmpty(query)) return 0;
            var comparison = options.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var count = 0;
            foreach (var file in ProjectTree.EnumerateFiles(root, options.ShowIgnored, ct))
            {
                ct.ThrowIfCancellationRequested();
                if (!IsSearchable(file, options.MaxFileBytes)) continue;
                var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
                try
                {
                    using var reader = new StreamReader(file, new UTF8Encoding(false, throwOnInvalidBytes: true), detectEncodingFromByteOrderMarks: true);
                    string? line;
                    var number = 0;
                    while ((line = reader.ReadLine()) is not null)
                    {
                        number++;
                        if ((number & 0xFF) == 0) ct.ThrowIfCancellationRequested();
                        var idx = line.IndexOf(query, comparison);
                        if (idx < 0) continue;
                        onHit(new SearchHit(file, rel, number, idx + 1, line.Length > 300 ? line[..300] : line));
                        if (++count >= options.MaxHits) return count;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException) { /* unreadable or not UTF-8: skip */ }
            }
            return count;
        }, ct);

    internal static bool IsSearchable(string file, long maxBytes)
    {
        try
        {
            var info = new FileInfo(file);
            if (!info.Exists || info.Length > maxBytes) return false;
            Span<byte> head = stackalloc byte[4096];
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var n = fs.Read(head);
            return !head[..n].Contains((byte)0);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}
