namespace DalamudCodeEditor.TextEditor;

public static class TextInsertionHelper
{
    public static int InsertTextAt(List<Line> lines, Coordinate where, string value, int tabSize)
    {
        var totalLines = 0;

        while (where.Line >= lines.Count)
        {
            lines.Add(new Line());
        }

        var index = GetCharacterIndex(lines, where, tabSize);

        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.Value == '\r')
            {
                continue;
            }

            if (rune.Value == '\n')
            {
                var currentLine = lines[where.Line];

                var newLine = new Line();
                if (index < currentLine.Count)
                {
                    newLine.AddRange(currentLine.Skip(index));
                    currentLine.RemoveRange(index, currentLine.Count - index);
                }

                lines.Insert(where.Line + 1, newLine);
                where.Line++;
                where.Column = 0;
                index = 0;
                totalLines++;
            }
            else
            {
                while (where.Line >= lines.Count)
                {
                    lines.Add(new ());
                }

                var line = lines[where.Line];
                var s = rune.ToString();
                foreach (var c in s)
                {
                    line.Insert(index++, new Glyph(c, PaletteIndex.Default));
                }

                where.Column += GlyphHelper.GetGlyphDisplayWidth(s, tabSize);
            }
        }

        return totalLines;
    }

    public static int GetCharacterIndex(List<Line> lines, Coordinate coords, int tabSize)
    {
        if (coords.Line >= lines.Count)
        {
            return 0;
        }

        var line = lines[coords.Line];
        var col = 0;
        var i = 0;

        while (i < line.Count && col < coords.Column)
        {
            var c = line[i].Character;

            col += GlyphHelper.GetGlyphDisplayWidth(c, tabSize);
            i += Utf8Helper.UTF8CharLength(c);
        }

        return i;
    }
}
