namespace DalamudCodeEditor;

public struct Glyph(char character, PaletteIndex color = PaletteIndex.Default)
{
    public readonly char Character = character;

    public PaletteIndex Color = color;

    public bool Comment = false;
}
