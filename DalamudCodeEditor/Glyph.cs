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

    public bool IsLetter()
    {
        // Include underscores to group words
        return Rune.IsLetter(rune) || rune.Value == '_';
    }

    public bool IsNumber()
    {
        return Rune.IsNumber(rune);
    }

    public bool IsWhiteSpace()
    {
        return char.IsWhiteSpace((char)Rune.Value);
    }

    public bool IsSpecial()
    {
        return !IsLetter() && !IsNumber() && !IsWhiteSpace();
    }

    public bool IsWhiteSpaceOrSpecial()
    {
        return IsWhiteSpace() || IsSpecial();
    }

    public bool IsGroupable(Glyph glyph)
    {
        if (IsLetter())
        {
            return glyph.IsLetter();
        }

        if (IsNumber())
        {
            return glyph.IsNumber();
        }

        return Rune.Value switch
        {
            (int)')' => glyph.Rune.Value == ')' || glyph.Rune.Value == '(',
            (int)']' => glyph.Rune.Value == ']' || glyph.Rune.Value == '{',
            (int)'}' => glyph.Rune.Value == '}' || glyph.Rune.Value == '[',
            _ => Rune.Value == glyph.Rune.Value,
        };
    }
}
