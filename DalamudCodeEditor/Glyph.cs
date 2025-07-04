using System.Text;

namespace DalamudCodeEditor;

public struct Glyph(Rune rune, PaletteIndex color = PaletteIndex.Default)
{
    public readonly Rune Rune = rune;

    [Obsolete("Use Rune instead")]
    public char Character
    {
        get
        {
            Span<char> buffer = stackalloc char[2];
            Rune.EncodeToUtf16(buffer);
            return buffer[0];
        }
    }

    public PaletteIndex Color = color;

    public bool Comment = false;

    public Glyph(char character, PaletteIndex color = PaletteIndex.Default) : this(new Rune(character), color)
    {
    }

    public override string ToString()
    {
        return Rune.ToString();
    }
}
