namespace DevAcademy.Terminal.Tests;

public class KeyEncoderTests
{
    [Theory]
    [InlineData(TermKey.Up, false, "\e[A")]
    [InlineData(TermKey.Down, false, "\e[B")]
    [InlineData(TermKey.Right, false, "\e[C")]
    [InlineData(TermKey.Left, false, "\e[D")]
    [InlineData(TermKey.Up, true, "\eOA")]
    [InlineData(TermKey.Home, false, "\e[H")]
    [InlineData(TermKey.End, false, "\e[F")]
    [InlineData(TermKey.Home, true, "\eOH")]
    [InlineData(TermKey.Delete, false, "\e[3~")]
    [InlineData(TermKey.PageUp, false, "\e[5~")]
    [InlineData(TermKey.PageDown, false, "\e[6~")]
    [InlineData(TermKey.Enter, false, "\r")]
    [InlineData(TermKey.Tab, false, "\t")]
    [InlineData(TermKey.Backspace, false, "\x7f")]
    [InlineData(TermKey.Escape, false, "\e")]
    [InlineData(TermKey.F1, false, "\eOP")]
    [InlineData(TermKey.F5, false, "\e[15~")]
    [InlineData(TermKey.F12, false, "\e[24~")]
    public void Plain_keys(TermKey key, bool app, string expected) =>
        Assert.Equal(expected, KeyEncoder.Encode(key, TermMods.None, app));

    [Fact]
    public void Modified_keys()
    {
        Assert.Equal("\e[1;5C", KeyEncoder.Encode(TermKey.Right, TermMods.Ctrl, false));
        Assert.Equal("\e[1;2A", KeyEncoder.Encode(TermKey.Up, TermMods.Shift, true));
        Assert.Equal("\e[3;3~", KeyEncoder.Encode(TermKey.Delete, TermMods.Alt, false));
        Assert.Equal("\e[Z", KeyEncoder.Encode(TermKey.Tab, TermMods.Shift, false));
        Assert.Equal("\b", KeyEncoder.Encode(TermKey.Backspace, TermMods.Ctrl, false));
    }

    [Fact]
    public void Control_characters()
    {
        Assert.Equal("\x03", KeyEncoder.EncodeControl('c'));   // Ctrl+C = ETX
        Assert.Equal("\x04", KeyEncoder.EncodeControl('d'));   // Ctrl+D = EOT
        Assert.Equal("\x1a", KeyEncoder.EncodeControl('Z'));
        Assert.Equal("\0", KeyEncoder.EncodeControl(' '));
        Assert.Equal("\e", KeyEncoder.EncodeControl('['));
        Assert.Null(KeyEncoder.EncodeControl('é'));
    }

    [Fact]
    public void Paste_normalizes_newlines_and_honours_bracketed_mode()
    {
        Assert.Equal("a\rb\rc", KeyEncoder.Paste("a\r\nb\nc", false));
        Assert.Equal("\e[200~x\ry\e[201~", KeyEncoder.Paste("x\ny", true));
        Assert.DoesNotContain("\e[201~x", KeyEncoder.Paste("a\e[201~rm -rf /", true));
    }

    [Fact]
    public void Alt_prefixes_escape() => Assert.Equal("\ef", KeyEncoder.EncodeAlt("f"));
}

public class ShellResolverTests
{
    [Fact]
    public void Unix_prefers_SHELL_then_falls_back()
    {
        var s = ShellResolver.Resolve(k => k == "SHELL" ? "/usr/local/bin/fish" : null, false, p => p == "/usr/local/bin/fish", _ => null);
        Assert.Equal("/usr/local/bin/fish", s.Path);
        Assert.Equal(["-l"], s.Args);

        s = ShellResolver.Resolve(_ => null, false, p => p == "/bin/bash", _ => null);
        Assert.Equal("/bin/bash", s.Path);

        s = ShellResolver.Resolve(k => k == "SHELL" ? "/missing/zsh" : null, false, p => p == "/bin/sh", _ => null);
        Assert.Equal("/bin/sh", s.Path);
        Assert.Empty(s.Args);
    }

    [Fact]
    public void Windows_prefers_pwsh_then_powershell_then_cmd()
    {
        var s = ShellResolver.Resolve(_ => null, true, _ => false, n => n == "pwsh.exe" ? @"C:\pwsh.exe" : n == "powershell.exe" ? @"C:\ps.exe" : null);
        Assert.Equal(@"C:\pwsh.exe", s.Path);
        s = ShellResolver.Resolve(_ => null, true, _ => false, n => n == "powershell.exe" ? @"C:\ps.exe" : null);
        Assert.Equal(@"C:\ps.exe", s.Path);
        s = ShellResolver.Resolve(k => k == "ComSpec" ? @"C:\Windows\cmd.exe" : null, true, _ => false, _ => null);
        Assert.Equal(@"C:\Windows\cmd.exe", s.Path);
    }
}
