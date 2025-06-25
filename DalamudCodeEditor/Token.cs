namespace DalamudCodeEditor;

public readonly struct Token
{
    public int Start { get; }
    public int End { get; }
    public PaletteIndex Color { get; }

    public Token(int start, int end, PaletteIndex color)
    {
        Start = start;
        End = end;
        Color = color;
    }
}