namespace DalamudCodeEditor.TextEditor;

public static class Utf8Helper
{
    public static int UTF8CharLength(char c)
    {
        if ((c & 0xFE) == 0xFC)
        {
            return 6;
        }

        if ((c & 0xFC) == 0xF8)
        {
            return 5;
        }

        if ((c & 0xF8) == 0xF0)
        {
            return 4;
        }

        if ((c & 0xF0) == 0xE0)
        {
            return 3;
        }

        if ((c & 0xE0) == 0xC0)
        {
            return 2;
        }

        return 1;
    }
}
