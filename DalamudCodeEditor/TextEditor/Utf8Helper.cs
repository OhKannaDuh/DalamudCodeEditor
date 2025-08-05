using System.Text;

namespace DalamudCodeEditor.TextEditor;

public static class Utf8Helper
{
    public static unsafe string BytePtrToString(byte* ptr)
    {
        if (ptr == null)
        {
            return string.Empty;
        }

        var len = 0;
        while (ptr[len] != 0)
        {
            len++;
        }

        return Encoding.UTF8.GetString(ptr, len);
    }
}
