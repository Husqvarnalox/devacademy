using System.Text;

namespace DevAcademy.Terminal;

public enum TermKey
{
    None, Up, Down, Left, Right, Home, End, PageUp, PageDown, Insert, Delete, Backspace, Tab, Enter, Escape,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
}

[Flags]
public enum TermMods { None = 0, Shift = 1, Alt = 2, Ctrl = 4 }

/// <summary>Turns keys and text into the byte sequences a terminal application expects (xterm conventions).</summary>
public static class KeyEncoder
{
    public static string? Encode(TermKey key, TermMods mods, bool applicationCursorKeys)
    {
        var m = ModifierParam(mods);
        switch (key)
        {
            case TermKey.Enter: return (mods & TermMods.Alt) != 0 ? "\e\r" : "\r";
            case TermKey.Tab: return (mods & TermMods.Shift) != 0 ? "\e[Z" : "\t";
            case TermKey.Escape: return "\e";
            case TermKey.Backspace:
                if ((mods & TermMods.Ctrl) != 0) return "\b";
                return (mods & TermMods.Alt) != 0 ? "\e\x7f" : "\x7f";
            case TermKey.Up: return Cursor('A', m, applicationCursorKeys);
            case TermKey.Down: return Cursor('B', m, applicationCursorKeys);
            case TermKey.Right: return Cursor('C', m, applicationCursorKeys);
            case TermKey.Left: return Cursor('D', m, applicationCursorKeys);
            case TermKey.Home: return Cursor('H', m, applicationCursorKeys);
            case TermKey.End: return Cursor('F', m, applicationCursorKeys);
            case TermKey.Insert: return Tilde(2, m);
            case TermKey.Delete: return Tilde(3, m);
            case TermKey.PageUp: return Tilde(5, m);
            case TermKey.PageDown: return Tilde(6, m);
            case TermKey.F1: return Ss3('P', m);
            case TermKey.F2: return Ss3('Q', m);
            case TermKey.F3: return Ss3('R', m);
            case TermKey.F4: return Ss3('S', m);
            case TermKey.F5: return Tilde(15, m);
            case TermKey.F6: return Tilde(17, m);
            case TermKey.F7: return Tilde(18, m);
            case TermKey.F8: return Tilde(19, m);
            case TermKey.F9: return Tilde(20, m);
            case TermKey.F10: return Tilde(21, m);
            case TermKey.F11: return Tilde(23, m);
            case TermKey.F12: return Tilde(24, m);
            default: return null;
        }
    }

    private static int ModifierParam(TermMods mods) => 1 + ((mods & TermMods.Shift) != 0 ? 1 : 0) + ((mods & TermMods.Alt) != 0 ? 2 : 0) + ((mods & TermMods.Ctrl) != 0 ? 4 : 0);

    private static string Cursor(char final, int m, bool app) =>
        m > 1 ? $"\e[1;{m}{final}" : app ? $"\eO{final}" : $"\e[{final}";

    private static string Ss3(char final, int m) => m > 1 ? $"\e[1;{m}{final}" : $"\eO{final}";

    private static string Tilde(int code, int m) => m > 1 ? $"\e[{code};{m}~" : $"\e[{code}~";

    /// <summary>Ctrl+letter and friends. Returns null when the character has no control form.</summary>
    public static string? EncodeControl(char c)
    {
        if (c is >= 'a' and <= 'z') return ((char)(c - 'a' + 1)).ToString();
        if (c is >= 'A' and <= 'Z') return ((char)(c - 'A' + 1)).ToString();
        return c switch
        {
            ' ' or '@' or '2' => "\0",
            '[' or '3' => "\e",
            '\\' or '4' => "\x1c",
            ']' or '5' => "\x1d",
            '^' or '6' => "\x1e",
            '_' or '/' or '7' => "\x1f",
            '?' or '8' => "\x7f",
            _ => null,
        };
    }

    /// <summary>Text typed with Alt held: ESC-prefixed (the "meta sends escape" convention).</summary>
    public static string EncodeAlt(string text) => "\e" + text;

    /// <summary>Clipboard text for the PTY: newlines become CR; bracketed-paste markers when the application asked for them.</summary>
    public static string Paste(string text, bool bracketed)
    {
        var body = text.Replace("\r\n", "\r", StringComparison.Ordinal).Replace('\n', '\r');
        if (!bracketed) return body;
        return "\e[200~" + body.Replace("\e[201~", "", StringComparison.Ordinal) + "\e[201~";
    }

    public static byte[] ToBytes(string text) => Encoding.UTF8.GetBytes(text);
}
