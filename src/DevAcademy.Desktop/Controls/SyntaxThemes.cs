using System.Reflection;
using TextMateSharp.Internal.Themes.Reader;
using TextMateSharp.Themes;

namespace DevAcademy.Desktop.Controls;

/// <summary>
/// The editor's syntax palette: two TextMate themes (docs/design/DESIGN_SYSTEM.md, "Syntax palette") embedded as JSON.
/// They cover the same scopes as VS Code Light+/Dark+ but use the app's colours.
/// </summary>
public static class SyntaxThemes
{
    private static IRawTheme? _light;
    private static IRawTheme? _dark;

    public static IRawTheme Light => _light ??= Load("devacademy-light.json");
    public static IRawTheme Dark => _dark ??= Load("devacademy-dark.json");
    public static IRawTheme For(bool dark) => dark ? Dark : Light;

    private static IRawTheme Load(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DevAcademy.Themes." + name)
            ?? throw new InvalidOperationException($"Embedded syntax theme {name} is missing.");
        using var reader = new StreamReader(stream);
        return ThemeReader.ReadThemeSync(reader);
    }
}
