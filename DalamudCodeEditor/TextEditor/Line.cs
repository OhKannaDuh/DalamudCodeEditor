using System.Collections.ObjectModel;
using System.Text;

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

    public List<Glyph> GetGroupedGlyphsBeforeCursor(Cursor cursor)
    {
        var position = cursor.GetPosition();
        if (position.Column == 0)
        {
            return [];
        }


        var target = GetGlyphBeforeCursor(cursor);
        if (target == null)
        {
            return [];
        }

        List<Glyph> run = [];

        for (var i = position.Column - 1; i >= 0; i--)
        {
            var glyph = this[i];
            if (!target.Value.IsGroupable(glyph))
            {
                break;
            }

            run.Insert(0, glyph);
        }

        return run;
    }

    public List<Glyph> GetGroupedGlyphsAfterCursor(Cursor cursor)
    {
        var position = cursor.GetPosition();
        var line = this;
        if (position.Column >= line.Count)
        {
            return [];
        }

        var target = GetGlyphUnderCursor(cursor);
        if (target == null)
        {
            return [];
        }

        List<Glyph> run = [];

        for (var i = position.Column; i < line.Count; i++)
        {
            var glyph = line[i];
            if (!target.Value.IsGroupable(glyph))
            {
                break;
            }

            run.Add(glyph);
        }

        return run;
    }

    public Glyph? GetGlyphBeforeCursor(Cursor cursor)
    {
        var column = cursor.GetPosition().Column;
        if (column == 0)
        {
            return null;
        }

        return this[column - 1];
    }

    public Glyph? GetGlyphUnderCursor(Cursor cursor)
    {
        return this[cursor.GetPosition().Column];
    }

    public override string ToString()
    {
        return string.Concat(this.Select(g => g.ToString()));
    }
}
