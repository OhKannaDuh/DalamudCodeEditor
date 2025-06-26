namespace DalamudCodeEditor.TextEditor;

public static class TextInsertionHelper
{
    public static int InsertTextAt(List<List<Glyph>> lines, Coordinate where, string value, int tabSize)
    {
        var totalLines = 0;

        while (where.Line >= lines.Count)
        {
            lines.Add([]);
        }

        var index = GetCharacterIndex(lines, where, tabSize);

        for (var i = 0; i < value.Length; i++)
        {
            var chr = value[i];
            if (chr == '\r')
            {
                continue;
            }

            if (chr == '\n')
            {
                var currentLine = lines[where.Line];

                var newLine = new List<Glyph>();
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
                    lines.Add(new List<Glyph>());
                }

                var line = lines[where.Line];
                var d = Utf8Helper.UTF8CharLength(chr);
                for (var j = 0; j < d && i < value.Length && value[i] != '\0'; j++, i++)
                {
                    line.Insert(index++, new Glyph(value[i], PaletteIndex.Default));
                }

                i--; // Adjust for the outer loop increment
                where.Column++;
            }
        }

        return totalLines;
    }

    public static int GetCharacterIndex(List<List<Glyph>> lines, Coordinate coords, int tabSize)
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
            if (line[i].Character == '\t')
            {
                col = col / tabSize * tabSize + tabSize;
            }
            else
            {
                col++;
            }

            i += Utf8Helper.UTF8CharLength(line[i].Character);
        }

        return i;
    }
}
