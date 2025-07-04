namespace DalamudCodeEditor;

public struct Glyph
{
    public char mChar;
    public PaletteIndex mColorIndex = PaletteIndex.Default;
    public bool mComment = false;
    public bool mMultiLineComment = false;
    public bool mPreprocessor = false;

    public Glyph(char aChar, PaletteIndex aColorIndex)
    {
        mChar = aChar;
        mColorIndex = aColorIndex;
    }
}