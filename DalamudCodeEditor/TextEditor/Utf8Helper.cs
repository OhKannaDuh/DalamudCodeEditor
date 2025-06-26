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

    public static bool IsUTFSequence(char c)
    {
        return (c & 0xC0) == 0x80;
    }

    public static int ImTextCharToUtf8(ref char[] buf, int buf_size, uint c)
    {
        if (c < 0x80)
        {
            buf[0] = (char)c;
            return 1;
        }

        if (c < 0x800)
        {
            if (buf_size < 2)
            {
                return 0;
            }

            buf[0] = (char)(0xc0 + (c >> 6));
            buf[1] = (char)(0x80 + (c & 0x3f));
            return 2;
        }

        if (c >= 0xdc00 && c < 0xe000)
        {
            return 0;
        }

        if (c >= 0xd800 && c < 0xdc00)
        {
            if (buf_size < 4)
            {
                return 0;
            }

            buf[0] = (char)(0xf0 + (c >> 18));
            buf[1] = (char)(0x80 + (c >> 12 & 0x3f));
            buf[2] = (char)(0x80 + (c >> 6 & 0x3f));
            buf[3] = (char)(0x80 + (c & 0x3f));
            return 4;
        }

        //else if (c < 0x10000)
        {
            if (buf_size < 3)
            {
                return 0;
            }

            buf[0] = (char)(0xe0 + (c >> 12));
            buf[1] = (char)(0x80 + (c >> 6 & 0x3f));
            buf[2] = (char)(0x80 + (c & 0x3f));
            return 3;
        }
    }
}
