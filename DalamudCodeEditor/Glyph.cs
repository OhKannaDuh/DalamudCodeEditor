namespace DalamudCodeEditor;

public struct Glyph(char character, PaletteIndex colorIndex = PaletteIndex.Default)
{
    public readonly char Character = character;

    public PaletteIndex ColorIndex = colorIndex;

    public bool Comment = false;

    public bool MultiLineComment = false;

    public bool Preprocessor = false;
}
