using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace DevAcademy.Application.Evaluation;

public enum ReportedOutcome { Passed, Failed, Skipped }

/// <param name="Name">The test's own name (JUnit <c>name</c>, Go <c>Test</c> including "/subtest").</param>
/// <param name="FullName">Enclosing suites and the name joined with " &gt; " (JUnit); equals <paramref name="Name"/> for Go.</param>
/// <param name="ClassName">JUnit <c>classname</c> (or the Go package); may be empty.</param>
public sealed record ReportedTest(string Name, string FullName, string ClassName, ReportedOutcome Outcome, string? Message);

/// <param name="BuildFailed">The tool reported that the code did not build (go: build-fail / FailedBuild).</param>
/// <param name="BuildOutput">Compiler output collected from the report, when the format carries it.</param>
public sealed record ParsedReport(IReadOnlyList<ReportedTest> Tests, bool BuildFailed = false, string? BuildOutput = null);

/// <summary>
/// Reads JUnit XML as written by <c>node --test</c>, pytest, Gradle and others: every <c>testcase</c> anywhere in the tree
/// (robust to <c>testsuites</c>/<c>testsuite</c> nesting); a <c>failure</c> or <c>error</c> child (or Node's <c>failure</c>
/// attribute) fails it, a <c>skipped</c> child skips it. Returns null for text that is not a JUnit document.
/// </summary>
public static class JUnitReportParser
{
    public static ParsedReport? Parse(string xml)
    {
        XDocument doc;
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml.TrimStart('﻿')),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersFromEntities = 0 });
            doc = XDocument.Load(reader);
        }
        catch (XmlException) { return null; }

        if (doc.Root is not { } root || root.Name.LocalName is not ("testsuites" or "testsuite" or "testcase")) return null;

        var tests = new List<ReportedTest>();
        foreach (var tc in root.DescendantsAndSelf().Where(e => e.Name.LocalName == "testcase"))
        {
            var name = (string?)tc.Attribute("name") ?? "";
            var suites = tc.Ancestors().Where(a => a.Name.LocalName == "testsuite").Select(a => (string?)a.Attribute("name"))
                .Where(n => !string.IsNullOrEmpty(n)).Reverse().ToList();
            var fullName = string.Join(" > ", suites.Append(name));

            var problem = tc.Elements().FirstOrDefault(e => e.Name.LocalName is "failure" or "error");
            var skipped = tc.Elements().Any(e => e.Name.LocalName == "skipped");
            var failureAttribute = (string?)tc.Attribute("failure");
            var outcome = problem is not null || failureAttribute is not null ? ReportedOutcome.Failed
                : skipped ? ReportedOutcome.Skipped : ReportedOutcome.Passed;

            string? message = null;
            if (outcome == ReportedOutcome.Failed)
            {
                message = (string?)problem?.Attribute("message") ?? failureAttribute;
                var body = problem?.Value.Trim();
                // Vitest repeats the message at the head of the body ("Error: <message>" plus a stack or a diff): keep the longer text once.
                if (!string.IsNullOrEmpty(body) && body != message)
                    message = string.IsNullOrEmpty(message) || Collapse(body).Contains(Collapse(message), StringComparison.Ordinal) ? body : message + "\n" + body;
            }
            tests.Add(new ReportedTest(name, fullName, (string?)tc.Attribute("classname") ?? "", outcome, message));
        }
        return new ParsedReport(tests);
    }

    // XML attribute normalization turns the message's line breaks into spaces while the element body keeps them, so compare ignoring whitespace layout.
    private static string Collapse(string text) => System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
}

/// <summary>
/// Reads the event stream of <c>go test -json</c> (one JSON object per line; non-JSON lines are ignored). A test is
/// reported by the last <c>pass</c>/<c>fail</c>/<c>skip</c> event carrying its name; <c>build-output</c>/<c>build-fail</c>
/// and package events with <c>FailedBuild</c> mark a build failure.
/// </summary>
public static class GoTestJsonParser
{
    private const int MaxOutputPerTest = 4000;

    public static ParsedReport Parse(string stdout)
    {
        var order = new List<(string Package, string Test)>();
        var outcomes = new Dictionary<(string, string), ReportedOutcome>();
        var output = new Dictionary<(string, string), StringBuilder>();
        var build = new StringBuilder();
        var buildFailed = false;

        foreach (var raw in stdout.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] != '{') continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var e = doc.RootElement;
                if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("Action", out var a) || a.ValueKind != JsonValueKind.String) continue;
                var action = a.GetString();
                var text = e.TryGetProperty("Output", out var o) && o.ValueKind == JsonValueKind.String ? o.GetString() : null;
                if (action == "build-output") { build.Append(text); continue; }
                if (action == "build-fail") { buildFailed = true; continue; }
                if (e.TryGetProperty("FailedBuild", out var fb) && fb.ValueKind == JsonValueKind.String && fb.GetString()!.Length > 0) buildFailed = true;

                var package = e.TryGetProperty("Package", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString()! : "";
                if (!e.TryGetProperty("Test", out var t) || t.ValueKind != JsonValueKind.String) continue; // package-level event
                var key = (package, t.GetString()!);
                switch (action)
                {
                    case "run":
                        if (!order.Contains(key)) order.Add(key);
                        break;
                    case "output" when text is not null:
                        if (!output.TryGetValue(key, out var sb)) output[key] = sb = new StringBuilder();
                        if (sb.Length < MaxOutputPerTest) sb.Append(text);
                        break;
                    case "pass" or "fail" or "skip":
                        if (!order.Contains(key)) order.Add(key);
                        outcomes[key] = action == "pass" ? ReportedOutcome.Passed : action == "fail" ? ReportedOutcome.Failed : ReportedOutcome.Skipped;
                        break;
                }
            }
            catch (JsonException) { /* not an event: ignore */ }
        }

        var tests = new List<ReportedTest>();
        foreach (var key in order)
        {
            if (!outcomes.TryGetValue(key, out var outcome)) continue; // started but never finished (crash, timeout)
            var message = outcome == ReportedOutcome.Failed && output.TryGetValue(key, out var sb) ? Clean(sb.ToString()) : null;
            tests.Add(new ReportedTest(key.Item2, key.Item2, key.Item1, outcome, message));
        }
        return new ParsedReport(tests, buildFailed, build.Length > 0 ? build.ToString() : null);
    }

    /// <summary>Drops the "=== RUN"/"--- FAIL" bookkeeping lines and keeps the assertion messages.</summary>
    private static string? Clean(string raw)
    {
        var lines = raw.Split('\n')
            .Where(l => !l.StartsWith("=== ", StringComparison.Ordinal) && !l.TrimStart().StartsWith("--- ", StringComparison.Ordinal))
            .Select(l => l.TrimEnd())
            .Where(l => l.Length > 0);
        var text = string.Join("\n", lines).Trim();
        return text.Length == 0 ? null : text;
    }

}
