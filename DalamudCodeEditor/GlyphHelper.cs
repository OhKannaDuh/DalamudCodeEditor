namespace DalamudCodeEditor;

public static class GlyphHelper
{
    public static int GetGlyphDisplayWidth(char c, int tabWidth)
    {
        return GetGlyphDisplayWidth(c.ToString(), tabWidth);
    }

    public static int GetGlyphDisplayWidth(string s, int tabWidth)
    {
        if (string.IsNullOrEmpty(s))
        {
            return 0;
        }

        if (s == "\t")
        {
            return tabWidth;
        }

        var codePoint = char.ConvertToUtf32(s, 0);

        if (
            codePoint >= 0x1100 && codePoint <= 0x115F || // Hangul Jamo
            codePoint >= 0x2E80 && codePoint <= 0xA4CF || // CJK
            codePoint >= 0xAC00 && codePoint <= 0xD7A3 || // Hangul
            codePoint >= 0xF900 && codePoint <= 0xFAFF || // Compatibility Ideographs
            codePoint >= 0xFE10 && codePoint <= 0xFE19 || // Vertical forms
            codePoint >= 0x1F300 && codePoint <= 0x1F64F || // Emoji
            codePoint >= 0x1F900 && codePoint <= 0x1F9FF || // Emoji
            codePoint >= 0x20000 && codePoint <= 0x2FFFD || // CJK Extension B-D
            codePoint >= 0x30000 && codePoint <= 0x3FFFD // CJK Extension E-G
        )
        {
            return 2;
        }

        return 1;
    }
}
