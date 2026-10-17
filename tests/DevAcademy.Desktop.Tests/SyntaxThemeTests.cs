using System.Reflection;
using System.Text.Json.Nodes;
using DevAcademy.Desktop.Controls;
using Xunit;

namespace DevAcademy.Desktop.Tests;

/// <summary>The embedded editor palettes load and keep WCAG AA (4.5:1) text contrast on every editor surface.</summary>
public class SyntaxThemeTests
{
    [Fact]
    public void Both_themes_load()
    {
        Assert.NotNull(SyntaxThemes.Light);
        Assert.NotNull(SyntaxThemes.Dark);
        Assert.NotSame(SyntaxThemes.For(false), SyntaxThemes.For(true));
    }

    [Theory]
    [InlineData("devacademy-light.json")]
    [InlineData("devacademy-dark.json")]
    public void Every_token_color_meets_4_5_contrast_on_editor_surfaces(string file)
    {
        using var stream = typeof(SyntaxThemes).Assembly.GetManifestResourceStream("DevAcademy.Themes." + file)!;
        var root = JsonNode.Parse(stream)!.AsObject();
        var colors = root["colors"]!.AsObject();
        string[] surfaces =
        {
            colors["editor.background"]!.GetValue<string>(),
            colors["editor.lineHighlightBackground"]!.GetValue<string>(),
            colors["editor.selectionBackground"]!.GetValue<string>(),
        };
        var foregrounds = new HashSet<string> { colors["editor.foreground"]!.GetValue<string>() };
        foreach (var rule in root["tokenColors"]!.AsArray())
            if (rule!["settings"]!["foreground"]?.GetValue<string>() is { } fg) foregrounds.Add(fg);
        foreach (var surface in surfaces)
            foreach (var fg in foregrounds)
                Assert.True(Contrast(fg, surface) >= 4.5, $"{file}: {fg} on {surface} = {Contrast(fg, surface):0.00}");
    }

    private static double Luminance(string hex)
    {
        double Channel(int i)
        {
            double c = int.Parse(hex.AsSpan(1 + i * 2, 2), System.Globalization.NumberStyles.HexNumber) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(0) + 0.7152 * Channel(1) + 0.0722 * Channel(2);
    }

    private static double Contrast(string a, string b)
    {
        double x = Luminance(a), y = Luminance(b);
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }
}
