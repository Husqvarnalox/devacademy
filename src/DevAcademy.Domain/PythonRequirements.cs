using System.Globalization;
using System.Text.RegularExpressions;

namespace DevAcademy.Domain;

/// <summary>Whether an exercise needs a hardware accelerator. CPU-first: the default is None.</summary>
public enum AcceleratorNeed { None, Optional, Required }

/// <summary>
/// Python requirements of an exercise: an interpreter version specifier (<c>&gt;=3.11</c>) and package requirements
/// (<c>numpy&gt;=2</c>). Plain syntax checks live in <see cref="PythonPackageSpec"/> / <see cref="PythonSpecifierSet"/>.
/// </summary>
public sealed record ExerciseRequirements(string? Python, IReadOnlyList<string> Packages)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Python) && Packages.Count == 0;
}

/// <summary>
/// A PEP 440 version, simplified: epoch, release segments, a/b/rc pre-release, post and dev releases. Local versions are
/// ignored for ordering. Good enough to decide "is the installed numpy &gt;= 2"; it is not a packaging library.
/// </summary>
public sealed partial class PythonVersion : IComparable<PythonVersion>
{
    [GeneratedRegex(@"^v?(?:(?<epoch>\d+)!)?(?<rel>\d+(?:\.\d+)*)(?:[-_.]?(?<pre>alpha|a|beta|b|preview|pre|rc|c)[-_.]?(?<pren>\d*))?(?:(?:-(?<postn1>\d+))|(?:[-_.]?(?<post>post|rev|r)[-_.]?(?<postn>\d*)))?(?:[-_.]?(?<dev>dev)[-_.]?(?<devn>\d*))?(?:\+(?<local>[a-z0-9]+(?:[-_.][a-z0-9]+)*))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    private PythonVersion(string text, int epoch, int[] release, int preKind, int preNumber, int? post, int? dev)
    {
        Text = text;
        Epoch = epoch;
        Release = release;
        PreKind = preKind;
        PreNumber = preNumber;
        Post = post;
        Dev = dev;
    }

    public string Text { get; }
    public int Epoch { get; }
    public IReadOnlyList<int> Release { get; }
    /// <summary>0 = none, 1 = alpha, 2 = beta, 3 = release candidate.</summary>
    public int PreKind { get; }
    public int PreNumber { get; }
    public int? Post { get; }
    public int? Dev { get; }

    public bool IsPreRelease => PreKind != 0 || Dev is not null;

    public static bool TryParse(string? text, out PythonVersion version)
    {
        version = null!;
        if (string.IsNullOrWhiteSpace(text)) return false;
        Match m = Pattern().Match(text.Trim());
        if (!m.Success) return false;
        try
        {
            int[] release = m.Groups["rel"].Value.Split('.').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            int epoch = m.Groups["epoch"].Success ? int.Parse(m.Groups["epoch"].Value, CultureInfo.InvariantCulture) : 0;
            int kind = 0;
            if (m.Groups["pre"].Success)
                kind = char.ToLowerInvariant(m.Groups["pre"].Value[0]) switch { 'a' => 1, 'b' => 2, _ => 3 };
            int preN = Number(m.Groups["pren"]);
            int? post = m.Groups["postn1"].Success ? int.Parse(m.Groups["postn1"].Value, CultureInfo.InvariantCulture)
                : m.Groups["post"].Success ? Number(m.Groups["postn"]) : null;
            int? dev = m.Groups["dev"].Success ? Number(m.Groups["devn"]) : null;
            version = new PythonVersion(text.Trim(), epoch, release, kind, preN, post, dev);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static int Number(Group g) => g.Success && g.Value.Length > 0 ? int.Parse(g.Value, CultureInfo.InvariantCulture) : 0;

    public int CompareTo(PythonVersion? other)
    {
        if (other is null) return 1;
        int c = Epoch.CompareTo(other.Epoch);
        if (c != 0) return c;
        c = CompareRelease(Release, other.Release);
        if (c != 0) return c;
        c = PreKey().CompareTo(other.PreKey());
        if (c != 0) return c;
        c = PostKey().CompareTo(other.PostKey());
        if (c != 0) return c;
        return DevKey().CompareTo(other.DevKey());
    }

    internal static int CompareRelease(IReadOnlyList<int> a, IReadOnlyList<int> b)
    {
        int n = Math.Max(a.Count, b.Count);
        for (int i = 0; i < n; i++)
        {
            int x = i < a.Count ? a[i] : 0, y = i < b.Count ? b[i] : 0;
            if (x != y) return x.CompareTo(y);
        }
        return 0;
    }

    // dev-only releases (1.0.dev1) sort before every pre-release; final releases after all of them.
    private long PreKey() => PreKind != 0 ? PreKind * 1_000_000L + PreNumber : Post is null && Dev is not null ? -1 : long.MaxValue;
    private long PostKey() => Post is null ? -1 : Post.Value;
    private long DevKey() => Dev is null ? long.MaxValue : Dev.Value;

    public override string ToString() => Text;
}

public enum SpecifierOperator { Equal, NotEqual, GreaterOrEqual, LessOrEqual, Greater, Less, Compatible, Arbitrary }

public sealed record SpecifierClause(SpecifierOperator Operator, string Version, bool Wildcard)
{
    public override string ToString() => Operator switch
    {
        SpecifierOperator.Equal => "==",
        SpecifierOperator.NotEqual => "!=",
        SpecifierOperator.GreaterOrEqual => ">=",
        SpecifierOperator.LessOrEqual => "<=",
        SpecifierOperator.Greater => ">",
        SpecifierOperator.Less => "<",
        SpecifierOperator.Compatible => "~=",
        _ => "===",
    } + Version + (Wildcard ? ".*" : "");

    public bool Satisfies(PythonVersion installed)
    {
        if (Operator == SpecifierOperator.Arbitrary) return string.Equals(installed.Text, Version, StringComparison.OrdinalIgnoreCase);
        if (!PythonVersion.TryParse(Version, out PythonVersion spec)) return false;
        switch (Operator)
        {
            case SpecifierOperator.Equal:
                return Wildcard ? PrefixMatch(installed, spec) : installed.CompareTo(spec) == 0;
            case SpecifierOperator.NotEqual:
                return Wildcard ? !PrefixMatch(installed, spec) : installed.CompareTo(spec) != 0;
            case SpecifierOperator.GreaterOrEqual:
                return installed.CompareTo(spec) >= 0;
            case SpecifierOperator.LessOrEqual:
                return installed.CompareTo(spec) <= 0;
            case SpecifierOperator.Greater:
                return installed.CompareTo(spec) > 0 && !(installed.Post is not null && spec.Post is null && PythonVersion.CompareRelease(installed.Release, spec.Release) == 0);
            case SpecifierOperator.Less:
                // "<3.0" must not match 3.0rc1 (a pre-release of the excluded version).
                return installed.CompareTo(spec) < 0 && !(installed.IsPreRelease && !spec.IsPreRelease && PythonVersion.CompareRelease(installed.Release, spec.Release) == 0);
            case SpecifierOperator.Compatible:
                if (spec.Release.Count < 2) return false;
                var upper = spec.Release.Take(spec.Release.Count - 1).ToList();
                return installed.CompareTo(spec) >= 0 && PrefixMatch(installed, upper);
            default:
                return false;
        }
    }

    private static bool PrefixMatch(PythonVersion installed, PythonVersion spec) => PrefixMatch(installed, spec.Release);

    private static bool PrefixMatch(PythonVersion installed, IReadOnlyList<int> prefix)
    {
        for (int i = 0; i < prefix.Count; i++)
        {
            int have = i < installed.Release.Count ? installed.Release[i] : 0;
            if (have != prefix[i]) return false;
        }
        return true;
    }
}

/// <summary>A comma-separated list of version clauses, e.g. <c>&gt;=2,&lt;3</c>. Empty set = any version.</summary>
public sealed partial class PythonSpecifierSet
{
    [GeneratedRegex(@"^(?<op>===|==|!=|~=|>=|<=|>|<)\s*(?<ver>[A-Za-z0-9][A-Za-z0-9.!+_*-]*)$", RegexOptions.CultureInvariant)]
    private static partial Regex ClausePattern();

    public static PythonSpecifierSet Any { get; } = new([]);

    private PythonSpecifierSet(IReadOnlyList<SpecifierClause> clauses) => Clauses = clauses;

    public IReadOnlyList<SpecifierClause> Clauses { get; }

    public static bool TryParse(string? text, out PythonSpecifierSet set, out string? error)
    {
        set = Any;
        error = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        var clauses = new List<SpecifierClause>();
        foreach (string raw in text.Split(','))
        {
            string part = raw.Trim();
            Match m = ClausePattern().Match(part);
            if (!m.Success) { error = $"'{part}' is not a version clause (expected e.g. >=3.11 or <2)"; return false; }
            var op = m.Groups["op"].Value switch
            {
                "===" => SpecifierOperator.Arbitrary,
                "==" => SpecifierOperator.Equal,
                "!=" => SpecifierOperator.NotEqual,
                "~=" => SpecifierOperator.Compatible,
                ">=" => SpecifierOperator.GreaterOrEqual,
                "<=" => SpecifierOperator.LessOrEqual,
                ">" => SpecifierOperator.Greater,
                _ => SpecifierOperator.Less,
            };
            string ver = m.Groups["ver"].Value;
            bool wildcard = ver.EndsWith(".*", StringComparison.Ordinal);
            if (wildcard)
            {
                if (op is not (SpecifierOperator.Equal or SpecifierOperator.NotEqual)) { error = $"'{part}': a .* suffix is only allowed with == and !="; return false; }
                ver = ver[..^2];
            }
            if (op != SpecifierOperator.Arbitrary && !PythonVersion.TryParse(ver, out _)) { error = $"'{part}': '{ver}' is not a valid version"; return false; }
            if (op == SpecifierOperator.Compatible && ver.Split('.').Length < 2) { error = $"'{part}': ~= needs at least two release segments"; return false; }
            clauses.Add(new SpecifierClause(op, ver, wildcard));
        }
        set = new PythonSpecifierSet(clauses);
        return true;
    }

    public bool Satisfies(PythonVersion version) => Clauses.All(c => c.Satisfies(version));

    public override string ToString() => string.Join(",", Clauses);
}

/// <summary>One package requirement: name, optional extras, version clauses. URL references and environment markers are rejected.</summary>
public sealed partial class PythonPackageSpec
{
    [GeneratedRegex(@"^(?<name>[A-Za-z0-9](?:[A-Za-z0-9._-]*[A-Za-z0-9])?)\s*(?:\[(?<extras>[A-Za-z0-9._,\s-]*)\])?\s*(?:\(?(?<spec>[^()]*)\)?)?$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    [GeneratedRegex(@"[-_.]+")]
    private static partial Regex Separators();

    private PythonPackageSpec(string text, string name, IReadOnlyList<string> extras, PythonSpecifierSet versions)
    {
        Text = text;
        Name = name;
        Extras = extras;
        Versions = versions;
    }

    public string Text { get; }
    public string Name { get; }
    /// <summary>PEP 503 normalised name (lower case, runs of - _ . become -).</summary>
    public string NormalizedName => Normalize(Name);
    public IReadOnlyList<string> Extras { get; }
    public PythonSpecifierSet Versions { get; }

    public static string Normalize(string name) => Separators().Replace(name, "-").ToLowerInvariant();

    public static bool TryParse(string? text, out PythonPackageSpec spec, out string? error)
    {
        spec = null!;
        error = null;
        if (string.IsNullOrWhiteSpace(text)) { error = "package requirement is empty"; return false; }
        string t = text.Trim();
        if (t.StartsWith('-')) { error = $"'{t}': options are not allowed in a package list"; return false; }
        if (t.Contains('@') || t.Contains("://", StringComparison.Ordinal) || t.Contains(';'))
        { error = $"'{t}': URL references and environment markers are not allowed (use name and version clauses only)"; return false; }
        if (t.Any(char.IsControl)) { error = "package requirement contains control characters"; return false; }
        Match m = Pattern().Match(t);
        if (!m.Success) { error = $"'{t}' is not a valid requirement (expected e.g. numpy>=2)"; return false; }
        if (!PythonSpecifierSet.TryParse(m.Groups["spec"].Value, out PythonSpecifierSet versions, out string? vErr)) { error = $"'{t}': {vErr}"; return false; }
        var extras = m.Groups["extras"].Success
            ? m.Groups["extras"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];
        spec = new PythonPackageSpec(t, m.Groups["name"].Value, extras, versions);
        return true;
    }
}
