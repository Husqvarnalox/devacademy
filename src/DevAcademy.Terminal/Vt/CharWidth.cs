using System.Globalization;
using System.Text;

namespace DevAcademy.Terminal.Vt;

/// <summary>Column width of a code point: 0 (combining / format), 1 or 2 (East Asian wide, emoji). A compact wcwidth.</summary>
public static class CharWidth
{
    public static int Of(int cp)
    {
        if (cp < 0x300) return 1;
        if (cp is >= 0x1100 and <= 0x115F
            or >= 0x2E80 and <= 0x303E
            or >= 0x3041 and <= 0x33FF
            or >= 0x3400 and <= 0x4DBF
            or >= 0x4E00 and <= 0xA4CF
            or >= 0xA960 and <= 0xA97F
            or >= 0xAC00 and <= 0xD7A3
            or >= 0xF900 and <= 0xFAFF
            or >= 0xFE30 and <= 0xFE6F
            or >= 0xFF00 and <= 0xFF60
            or >= 0xFFE0 and <= 0xFFE6
            or >= 0x1F300 and <= 0x1F64F
            or >= 0x1F900 and <= 0x1F9FF
            or >= 0x20000 and <= 0x3FFFD) return 2;
        if (cp is >= 0x200B and <= 0x200F or 0xFEFF or >= 0xFE00 and <= 0xFE0F) return 0;
        if (cp > 0x10FFFF || (cp is >= 0xD800 and <= 0xDFFF)) return 1;
        var cat = Rune.GetUnicodeCategory(new Rune(cp));
        return cat is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format ? 0 : 1;
    }
}
