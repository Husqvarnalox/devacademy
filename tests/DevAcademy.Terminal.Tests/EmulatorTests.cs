using DevAcademy.Terminal.Vt;

namespace DevAcademy.Terminal.Tests;

public class EmulatorTests
{
    private static TerminalEmulator Term(int cols = 20, int rows = 6, int scrollback = 100) => new(cols, rows, scrollback);

    [Fact]
    public void Plain_text_and_crlf()
    {
        var t = Term();
        t.Feed("hello\r\nworld");
        Assert.Equal("hello", t.GetScreenLineText(0));
        Assert.Equal("world", t.GetScreenLineText(1));
        Assert.Equal((1, 5), (t.CursorRow, t.CursorCol));
    }

    [Fact]
    public void Bare_linefeed_keeps_the_column()
    {
        var t = Term();
        t.Feed("ab\ncd");
        Assert.Equal("ab", t.GetScreenLineText(0));
        Assert.Equal("  cd", t.GetScreenLineText(1));
    }

    [Fact]
    public void Cursor_positioning_and_relative_moves()
    {
        var t = Term();
        t.Feed("\e[3;5HX\e[2AY\e[3DZ\e[B\e[1GA");
        Assert.Equal('X', (char)t.ScreenLine(2).Cells[4].CodePoint);
        Assert.Equal('Y', (char)t.ScreenLine(0).Cells[5].CodePoint);
        Assert.Equal('Z', (char)t.ScreenLine(0).Cells[3].CodePoint);
        Assert.Equal('A', (char)t.ScreenLine(1).Cells[0].CodePoint);
    }

    [Fact]
    public void Cursor_is_clamped_to_the_screen()
    {
        var t = Term(10, 4);
        t.Feed("\e[99;99H");
        Assert.Equal((3, 9), (t.CursorRow, t.CursorCol));
        t.Feed("\e[99A\e[99D");
        Assert.Equal((0, 0), (t.CursorRow, t.CursorCol));
    }

    [Fact]
    public void Erase_in_line_and_display()
    {
        var t = Term(10, 3);
        t.Feed("aaaaaaaaaa\r\nbbbbbbbbbb\r\ncccccccccc");
        t.Feed("\e[2;4H\e[K");
        Assert.Equal("bbb", t.GetScreenLineText(1));
        t.Feed("\e[2;2H\e[1K");
        Assert.Equal("  b", t.GetScreenLineText(1));
        t.Feed("\e[1;5H\e[0J");
        Assert.Equal("aaaa", t.GetScreenLineText(0));
        Assert.Equal("", t.GetScreenLineText(1));
        Assert.Equal("", t.GetScreenLineText(2));
        t.Feed("\e[2J");
        Assert.Equal("", t.GetScreenText().Replace("\n", ""));
    }

    [Fact]
    public void Ansi_16_colors_and_bold_like_ls_color()
    {
        var t = Term(40, 3);
        // GNU ls --color: reset, bold blue "dir", reset, two spaces, bold green "run.sh", reset
        t.Feed("\e[0m\e[01;34mdir\e[0m  \e[01;32mrun.sh\e[0m\r\n");
        var cells = t.ScreenLine(0).Cells;
        Assert.Equal('d', (char)cells[0].CodePoint);
        Assert.True(cells[0].Fg.IsIndexed);
        Assert.Equal(4, cells[0].Fg.Index);
        Assert.True(cells[0].Flags.HasFlag(CellFlags.Bold));
        Assert.True(cells[3].Fg.IsDefault);
        Assert.Equal(CellFlags.None, cells[3].Flags);
        Assert.Equal(2, cells[5].Fg.Index);
        Assert.Equal("dir  run.sh", t.GetScreenLineText(0));
    }

    [Fact]
    public void Bright_and_background_colors()
    {
        var t = Term();
        t.Feed("\e[91;102mx\e[39;49my");
        Assert.Equal(9, t.ScreenLine(0).Cells[0].Fg.Index);
        Assert.Equal(10, t.ScreenLine(0).Cells[0].Bg.Index);
        Assert.True(t.ScreenLine(0).Cells[1].Fg.IsDefault);
        Assert.True(t.ScreenLine(0).Cells[1].Bg.IsDefault);
    }

    [Fact]
    public void Sgr_256_and_truecolor_semicolon_and_colon_forms()
    {
        var t = Term();
        t.Feed("\e[38;5;202ma\e[48;5;17mb\e[38;2;10;20;30mc\e[48;2;1;2;3md\e[38:2::200:100:50me\e[38:5:99mf\e[38:2:7:8:9mg");
        var c = t.ScreenLine(0).Cells;
        Assert.Equal(202, c[0].Fg.Index);
        Assert.Equal(17, c[1].Bg.Index);
        Assert.True(c[2].Fg.IsRgb);
        Assert.Equal((10, 20, 30), (c[2].Fg.R, c[2].Fg.G, c[2].Fg.B));
        Assert.Equal((1, 2, 3), (c[3].Bg.R, c[3].Bg.G, c[3].Bg.B));
        Assert.Equal((200, 100, 50), (c[4].Fg.R, c[4].Fg.G, c[4].Fg.B));
        Assert.Equal(99, c[5].Fg.Index);
        Assert.Equal((7, 8, 9), (c[6].Fg.R, c[6].Fg.G, c[6].Fg.B));
    }

    [Fact]
    public void Sgr_attributes_set_and_clear()
    {
        var t = Term();
        t.Feed("\e[1;3;4;7ma\e[22;23;24;27mb\e[9mc\e[0md");
        var c = t.ScreenLine(0).Cells;
        Assert.Equal(CellFlags.Bold | CellFlags.Italic | CellFlags.Underline | CellFlags.Inverse, c[0].Flags);
        Assert.Equal(CellFlags.None, c[1].Flags);
        Assert.Equal(CellFlags.Strike, c[2].Flags);
        Assert.Equal(CellFlags.None, c[3].Flags);
    }

    [Fact]
    public void Erase_uses_current_background_color()
    {
        var t = Term(10, 2);
        t.Feed("\e[44m\e[2K");
        Assert.Equal(4, t.ScreenLine(0).Cells[5].Bg.Index);
    }

    [Fact]
    public void Autowrap_is_deferred_until_the_next_character()
    {
        var t = Term(5, 3);
        t.Feed("abcde");
        Assert.Equal((0, 4), (t.CursorRow, t.CursorCol));
        t.Feed("f");
        Assert.Equal("abcde", t.GetScreenLineText(0));
        Assert.Equal("f", t.GetScreenLineText(1));
        Assert.True(t.ScreenLine(0).Wrapped);
        // CR after a full line must not scroll or wrap
        t.Feed("\rg");
        Assert.Equal("g", t.GetScreenLineText(1));
    }

    [Fact]
    public void Autowrap_off_overwrites_the_last_column()
    {
        var t = Term(5, 2);
        t.Feed("\e[?7labcdefg");
        Assert.Equal("abcdg", t.GetScreenLineText(0));
        Assert.Equal("", t.GetScreenLineText(1));
    }

    [Fact]
    public void Scrolling_pushes_lines_into_scrollback()
    {
        var t = Term(10, 3, 50);
        for (var i = 1; i <= 10; i++) t.Feed($"line{i}\r\n");
        Assert.Equal(8, t.ScrollbackCount);
        Assert.Equal("line1", t.GetLine(0).Cells.Take(5).Aggregate("", (s, c) => s + (char)c.CodePoint));
        Assert.Equal("line10", t.GetScreenLineText(1));
    }

    [Fact]
    public void Scrollback_is_bounded_and_absolute_rows_stay_stable()
    {
        var t = Term(10, 3, 20);
        for (var i = 0; i < 100; i++) t.Feed($"n{i}\r\n");
        Assert.Equal(20, t.ScrollbackCount);
        Assert.True(t.TrimmedCount > 0);
        Assert.Equal(100 - 2 + 0, t.TrimmedCount + t.ScrollbackCount); // 100 lines + prompt row minus the 3 visible rows (2 content rows + cursor row)
        Assert.Equal(t.TotalScrolled, t.TrimmedCount + t.ScrollbackCount);
    }

    [Fact]
    public void Default_scrollback_keeps_at_least_5000_lines()
    {
        var t = new TerminalEmulator(80, 24);
        for (var i = 0; i < 6000; i++) t.Feed($"{i}\r\n");
        Assert.Equal(5000, t.ScrollbackCount);
    }

    [Fact]
    public void Scroll_region_scrolls_only_inside_the_margins()
    {
        var t = Term(10, 5, 50);
        t.Feed("h\r\n1\r\n2\r\n3\r\nfoot");
        t.Feed("\e[2;4r");          // region rows 2..4, cursor homes
        t.Feed("\e[4;1H\n");         // LF on the bottom margin scrolls the region
        Assert.Equal("h", t.GetScreenLineText(0));
        Assert.Equal("2", t.GetScreenLineText(1));
        Assert.Equal("3", t.GetScreenLineText(2));
        Assert.Equal("", t.GetScreenLineText(3));
        Assert.Equal("foot", t.GetScreenLineText(4));
        Assert.Equal(0, t.ScrollbackCount);
    }

    [Fact]
    public void Reverse_index_scrolls_down_at_the_top_margin()
    {
        var t = Term(10, 3);
        t.Feed("a\r\nb\r\nc\e[1;1H\eM");
        Assert.Equal("", t.GetScreenLineText(0));
        Assert.Equal("a", t.GetScreenLineText(1));
        Assert.Equal("b", t.GetScreenLineText(2));
    }

    [Fact]
    public void Insert_and_delete_lines_and_characters()
    {
        var t = Term(10, 4);
        t.Feed("abcdef\r\nline2\r\nline3");
        t.Feed("\e[1;3H\e[2P");     // delete 2 chars at col 3
        Assert.Equal("abef", t.GetScreenLineText(0));
        t.Feed("\e[1;3H\e[2@");     // insert 2 blanks
        Assert.Equal("ab  ef", t.GetScreenLineText(0));
        t.Feed("\e[2;1H\e[L");      // insert a line at row 2
        Assert.Equal("", t.GetScreenLineText(1));
        Assert.Equal("line2", t.GetScreenLineText(2));
        t.Feed("\e[M");             // delete it again
        Assert.Equal("line2", t.GetScreenLineText(1));
    }

    [Fact]
    public void Erase_characters_does_not_move_the_cursor()
    {
        var t = Term();
        t.Feed("abcdef\e[1;2H\e[3X");
        Assert.Equal("a   ef", t.GetScreenLineText(0));
        Assert.Equal(1, t.CursorCol);
    }

    [Fact]
    public void Alternate_screen_switch_like_a_full_screen_app()
    {
        var t = Term(20, 4);
        t.Feed("$ vim file\r\nprompt-line");
        var mainText = t.GetScreenText();
        t.Feed("\e[?1049h\e[2J\e[H\e[7m -- INSERT -- \e[0m\e[3;1Hbody\e[?25l");
        Assert.True(t.IsAlternateScreen);
        Assert.False(t.CursorVisible);
        Assert.Contains("INSERT", t.GetScreenLineText(0));
        Assert.Equal("body", t.GetScreenLineText(2));
        Assert.DoesNotContain("prompt-line", t.GetScreenText());
        t.Feed("\e[?25h\e[?1049l");
        Assert.False(t.IsAlternateScreen);
        Assert.True(t.CursorVisible);
        Assert.Equal(mainText, t.GetScreenText());
        Assert.Equal((1, 11), (t.CursorRow, t.CursorCol));
        Assert.Equal(0, t.ScrollbackCount);
    }

    [Fact]
    public void Alternate_screen_never_feeds_scrollback()
    {
        var t = Term(10, 3, 50);
        t.Feed("\e[?1049h");
        for (var i = 0; i < 20; i++) t.Feed($"x{i}\r\n");
        t.Feed("\e[?1049l");
        Assert.Equal(0, t.ScrollbackCount);
    }

    [Fact]
    public void Dec_line_drawing_charset()
    {
        var t = Term();
        t.Feed("\e(0lqqk\e(Bq");
        Assert.Equal("┌──┐q", t.GetScreenLineText(0));
    }

    [Fact]
    public void Wide_characters_take_two_cells()
    {
        var t = Term(6, 2);
        t.Feed("a漢b");
        var c = t.ScreenLine(0).Cells;
        Assert.Equal(2, c[1].Width);
        Assert.Equal(0, c[2].Width);
        Assert.Equal('b', (char)c[3].CodePoint);
        Assert.Equal("a漢b", t.GetScreenLineText(0));
    }

    [Fact]
    public void Utf8_split_across_chunks_and_surrogate_pairs()
    {
        var t = Term();
        var bytes = System.Text.Encoding.UTF8.GetBytes("é😀x");
        t.Feed(bytes.AsSpan(0, 1));
        t.Feed(bytes.AsSpan(1, 4));
        t.Feed(bytes.AsSpan(5));
        Assert.Equal("é😀x", t.GetScreenLineText(0));
    }

    [Fact]
    public void Tabs_advance_to_stops_of_eight()
    {
        var t = Term(40, 2);
        t.Feed("a\tb\t\tc");
        Assert.Equal('b', (char)t.ScreenLine(0).Cells[8].CodePoint);
        Assert.Equal('c', (char)t.ScreenLine(0).Cells[24].CodePoint);
    }

    [Fact]
    public void Backspace_moves_left_without_erasing()
    {
        var t = Term();
        t.Feed("abc\b\bX");
        Assert.Equal("aXc", t.GetScreenLineText(0));
    }

    [Fact]
    public void Osc_title_with_bel_or_st_terminator()
    {
        var t = Term();
        var titles = new List<string>();
        t.TitleChanged += titles.Add;
        t.Feed("\e]0;first\a\e]2;second\e\\ok");
        Assert.Equal(["first", "second"], titles);
        Assert.Equal("ok", t.GetScreenLineText(0));
    }

    [Fact]
    public void Device_status_and_attribute_reports_are_answered()
    {
        var t = Term();
        var replies = new List<string>();
        t.Reply += replies.Add;
        t.Feed("ab\e[6n\e[5n\e[c");
        Assert.Equal(["\e[1;3R", "\e[0n", "\e[?1;2c"], replies);
    }

    [Fact]
    public void Mode_flags_are_tracked()
    {
        var t = Term();
        Assert.False(t.ApplicationCursorKeys);
        t.Feed("\e[?1h\e[?2004h");
        Assert.True(t.ApplicationCursorKeys);
        Assert.True(t.BracketedPaste);
        t.Feed("\e[?1l\e[?2004l");
        Assert.False(t.ApplicationCursorKeys);
        Assert.False(t.BracketedPaste);
    }

    [Fact]
    public void Save_and_restore_cursor()
    {
        var t = Term();
        t.Feed("\e[2;3H\e7\e[1;1Hzz\e8Q");
        Assert.Equal('Q', (char)t.ScreenLine(1).Cells[2].CodePoint);
    }

    [Fact]
    public void Unknown_sequences_are_ignored_without_corrupting_output()
    {
        var t = Term();
        t.Feed("a\e[?9999h\e[>5q\eP1$r0m\e\\\e]52;c;AAAA\a\e[=1cb");
        Assert.Equal("ab", t.GetScreenLineText(0));
    }

    [Fact]
    public void Repeat_character()
    {
        var t = Term();
        t.Feed("x\e[4b");
        Assert.Equal("xxxxx", t.GetScreenLineText(0));
    }

    [Fact]
    public void Resize_keeps_content_and_pulls_history_back()
    {
        var t = Term(10, 3, 50);
        for (var i = 1; i <= 6; i++) t.Feed($"r{i}\r\n");
        // rows: r5, r6, (cursor row); scrollback r1..r4
        t.Resize(10, 5);
        Assert.Equal(5, t.Rows);
        Assert.Equal(2, t.ScrollbackCount);
        Assert.Equal("r3", t.GetScreenLineText(0));
        Assert.Equal("r6", t.GetScreenLineText(3));
        t.Resize(6, 2);
        Assert.Equal(6, t.Cols);
        Assert.Equal(2, t.Rows);
        Assert.InRange(t.CursorRow, 0, 1);
    }

    [Fact]
    public void Line_versions_change_only_for_dirty_rows()
    {
        var t = Term(10, 4);
        t.Feed("one\r\ntwo\r\nthree");
        var before = Enumerable.Range(0, 4).Select(i => t.ScreenLine(i).Version).ToArray();
        t.Feed("\e[3;1HX");
        var after = Enumerable.Range(0, 4).Select(i => t.ScreenLine(i).Version).ToArray();
        Assert.Equal(before[0], after[0]);
        Assert.Equal(before[1], after[1]);
        Assert.NotEqual(before[2], after[2]);
        Assert.Equal(before[3], after[3]);
    }

    [Fact]
    public void Text_extraction_joins_wrapped_rows_and_trims()
    {
        var t = Term(5, 4, 20);
        t.Feed("abcdefgh\r\nxyz");
        Assert.Equal("abcdefgh\nxyz", t.GetText(0, 0, 2, 4));
        Assert.Equal("cdefgh", t.GetText(0, 2, 1, 4));
    }
}
