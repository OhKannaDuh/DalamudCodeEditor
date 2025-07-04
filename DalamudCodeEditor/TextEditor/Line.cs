using System.Collections.ObjectModel;

namespace DalamudCodeEditor.TextEditor;

public class Line : Collection<Glyph>
{
    public Line() : base(new List<Glyph>())
    {
    }

    public Line(IEnumerable<Glyph> glyphs) : base(new List<Glyph>(glyphs))
    {
    }

    public int VisualLength(int tabSize)
    {
        var length = 0;
        foreach (var g in this)
        {
            length += g.Rune.Value == '\t' ? tabSize : 1;
        }

        return length;
    }

    public void AddRange(IEnumerable<Glyph> glyphs)
    {
        foreach (var g in glyphs)
        {
            Add(g);
        }
    }

    public void RemoveRange(int index, int count)
    {
        for (var i = 0; i < count; i++)
        {
            // Remove at the same index since it shuffles back with each removal
            RemoveAt(index);
        }
    }

    public override string ToString()
    {
        return string.Concat(this.Select(g => g.ToString()));
    }
}
