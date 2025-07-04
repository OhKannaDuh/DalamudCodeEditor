using ImGuiNET;

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
                    lines.Add(new Line());
                }

                var line = lines[where.Line];

                var glyph = new Glyph(rune, PaletteIndex.Default);
                line.Insert(index++, glyph);

                where.Column += GlyphHelper.GetGlyphDisplayWidth(glyph, tabSize);
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
        var visualCol = 0;

        for (var i = 0; i < line.Count; i++)
        {
            if (visualCol >= coords.Column)
            {
                return i;
            }

            var glyph = line[i];
            visualCol += GlyphHelper.GetGlyphDisplayWidth(glyph, tabSize);
        }

        return line.Count;
    }

    public static int GetCharacterIndexByPixel(List<Glyph> line, float pixelOffset, float spaceSize, float tabSize)
    {
        var distance = 0f;

        for (var i = 0; i < line.Count; i++)
        {
            var glyph = line[i];
            var rune = glyph.Rune;

            float width;
            if (rune.Value == '\t')
            {
                width = (float)(Math.Floor(distance / tabSize) + 1) * tabSize - distance;
            }
            else if (rune.Value == ' ')
            {
                width = spaceSize;
            }
            else
            {
                width = ImGui.CalcTextSize(rune.ToString()).X;
            }

            if (distance + width > pixelOffset)
            {
                return i;
            }

            distance += width;
        }

        return line.Count;
    }
}
