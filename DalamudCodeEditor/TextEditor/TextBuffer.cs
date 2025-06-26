using System.Text;
using ImGuiNET;

namespace DalamudCodeEditor.TextEditor;

public class TextBuffer(Editor editor) : DirtyTrackable(editor)
{
    private readonly List<List<Glyph>> lines = [[]];

    public int LineCount
    {
        get => lines.Count;
    }

    public int LongestLine
    {
        get => GetLongestLine();
    }

    private int GetLongestLine()
    {
        var longest = 0;

        foreach (var line in lines)
        {
            var length = 0;

            foreach (var glyph in line)
            {
                length += glyph.Character == '\t' ? Style.TabSize : 1; // Tab counts as 4 visual spaces
            }

            if (length > longest)
            {
                longest = length;
            }
        }

        return longest;
    }

    public void SetText(string text)
    {
        MarkDirty();
        lines.Clear();
        lines.Add([]);

        foreach (var chr in text)
        {
            if (chr == '\r')
            {
                continue;
            }

            if (chr == '\n')
            {
                lines.Add([]);
            }
            else
            {
                lines.Last().Add(new Glyph(chr));
            }
        }

        Scroll.RequestScrollToTop();
        UndoManager.Clear();
    }

    public string GetText()
    {
        StringBuilder sb = new();
        for (var i = 0; i < lines.Count; i++)
        {
            foreach (var glyph in lines[i])
            {
                sb.Append(glyph.Character);
            }

            if (i < lines.Count - 1)
            {
                sb.Append('\n');
            }
        }

        return sb.ToString();
    }

    public List<Glyph> GetLine(int index)
    {
        return lines[index];
    }

    public void Clear()
    {
        MarkDirty();
        lines.Clear();
        lines.Add(new List<Glyph>());
    }

    public void AddLine()
    {
        AddLine([]);
    }

    public void AddLine(List<Glyph> line)
    {
        MarkDirty();
        lines.Add(line);
    }

    public void InsertLine(int index, List<Glyph> line)
    {
        MarkDirty();
        lines.Insert(index, line);
    }

    public void RemoveLine(int index)
    {
        MarkDirty();
        lines.RemoveAt(index);
    }

    public void ReplaceLine(int index, List<Glyph> line)
    {
        MarkDirty();
        lines[index] = line;
    }

    public int InsertTextAt(Coordinate where, string value)
    {
        MarkDirty();
        return TextInsertionHelper.InsertTextAt(lines, where, value, Style.TabSize);
    }

    public int GetLineMaxColumn(int line)
    {
        if (line < 0 || line >= LineCount)
        {
            return 0;
        }

        var glyphs = lines[line];
        var column = 0;

        for (var i = 0; i < glyphs.Count;)
        {
            var c = glyphs[i].Character;

            if (c == '\t')
            {
                column = column / Style.TabSize * Style.TabSize + Style.TabSize;
            }
            else
            {
                column++;
            }

            i += Utf8Helper.UTF8CharLength(c);
        }

        return column;
    }

    public float GetDistanceToLineStart(Coordinate from)
    {
        if (from.Line < 0 || from.Line >= LineCount)
        {
            return 0.0f;
        }

        var line = lines[from.Line];
        var distance = 0.0f;
        var spaceWidth = ImGui.CalcTextSize(" ").X;
        var charIndex = TextInsertionHelper.GetCharacterIndex(lines, from, Style.TabSize);

        for (var i = 0; i < line.Count && i < charIndex;)
        {
            var c = line[i].Character;

            if (c == '\t')
            {
                var tabSizePixels = Style.TabSize * spaceWidth;
                distance = ((float)Math.Floor((distance + 0.5f) / tabSizePixels) + 1) * tabSizePixels;
                i++;
            }
            else
            {
                var charLen = Utf8Helper.UTF8CharLength(c);
                var available = Math.Min(charLen, line.Count - i);

                var text = new string(line.Skip(i).Take(available).Select(g => g.Character).ToArray());
                distance += ImGui.CalcTextSize(text).X;

                i += available;
            }
        }

        return distance;
    }

    public string GetText(Coordinate start, Coordinate end)
    {
        var lineStart = start.Line;
        var lineEnd = end.Line;
        var indexStart = TextInsertionHelper.GetCharacterIndex(lines, start, Style.TabSize);
        var indexEnd = TextInsertionHelper.GetCharacterIndex(lines, end, Style.TabSize);

        var result = new StringBuilder();

        while (indexStart < indexEnd || lineStart < lineEnd)
        {
            if (lineStart >= lines.Count)
            {
                break;
            }

            var line = lines[lineStart];

            if (indexStart < line.Count)
            {
                result.Append(line[indexStart].Character);
                indexStart++;
            }
            else
            {
                indexStart = 0;
                lineStart++;
                result.Append('\n');
            }
        }

        return result.ToString();
    }

    public Coordinate FindWordStart(Coordinate aFrom)
    {
        var at = aFrom;
        if (at.Line >= Buffer.GetLines().Count)
        {
            return at;
        }

        var line = Buffer.GetLines()[at.Line];
        var cindex = GetCharacterIndex(at);

        if (cindex >= line.Count)
        {
            return at;
        }

        while (cindex > 0 && char.IsWhiteSpace(line[cindex].Character))
        {
            --cindex;
        }

        var cstart = line[cindex].ColorIndex;
        while (cindex > 0)
        {
            var c = line[cindex].Character;
            if ((c & 0xC0) != 0x80) // not UTF code sequence 10xxxxxx
            {
                if (c <= 32 && char.IsWhiteSpace(c))
                {
                    cindex++;
                    break;
                }

                if (cstart != line[cindex - 1].ColorIndex)
                {
                    break;
                }
            }

            --cindex;
        }

        return new Coordinate(at.Line, GetCharacterColumn(at.Line, cindex));
    }

    public Coordinate FindWordEnd(Coordinate aFrom)
    {
        var at = aFrom;
        if (at.Line >= Buffer.GetLines().Count)
        {
            return at;
        }

        var line = Buffer.GetLines()[at.Line];
        var cindex = GetCharacterIndex(at);

        if (cindex >= line.Count)
        {
            return at;
        }

        var prevspace = char.IsWhiteSpace(line[cindex].Character);
        var cstart = line[cindex].ColorIndex;
        while (cindex < line.Count)
        {
            var c = line[cindex].Character;
            var d = Utf8Helper.UTF8CharLength(c);
            if (cstart != line[cindex].ColorIndex)
            {
                break;
            }

            if (prevspace != char.IsWhiteSpace(c))
            {
                if (char.IsWhiteSpace(c))
                {
                    while (cindex < line.Count && char.IsWhiteSpace(line[cindex].Character))
                    {
                        ++cindex;
                    }
                }

                break;
            }

            cindex += d;
        }

        return new Coordinate(aFrom.Line, GetCharacterColumn(aFrom.Line, cindex));
    }

    public int GetCharacterIndex(Coordinate aCoordinates)
    {
        if (aCoordinates.Line >= Buffer.GetLines().Count)
        {
            return -1;
        }

        var line = Buffer.GetLines()[aCoordinates.Line];
        var c = 0;
        var i = 0;
        for (; i < line.Count && c < aCoordinates.Column;)
        {
            if (line[i].Character == '\t')
            {
                c = c / Style.TabSize * Style.TabSize + Style.TabSize;
            }
            else
            {
                ++c;
            }

            i += Utf8Helper.UTF8CharLength(line[i].Character);
        }

        return i;
    }

    public int GetCharacterColumn(int aLine, int aIndex)
    {
        if (aLine >= Buffer.GetLines().Count)
        {
            return 0;
        }

        var line = Buffer.GetLines()[aLine];
        var col = 0;
        var i = 0;
        while (i < aIndex && i < line.Count)
        {
            var c = line[i].Character;
            i += Utf8Helper.UTF8CharLength(c);
            if (c == '\t')
            {
                col = col / Style.TabSize * Style.TabSize + Style.TabSize;
            }
            else
            {
                col++;
            }
        }

        return col;
    }

    public Coordinate FindNextWord(Coordinate aFrom)
    {
        var at = aFrom;
        if (at.Line >= Buffer.GetLines().Count)
        {
            return at;
        }

        // skip to the next non-word character
        var cindex = GetCharacterIndex(aFrom);
        var isword = false;
        var skip = false;
        if (cindex < Buffer.GetLines()[at.Line].Count)
        {
            var line = Buffer.GetLines()[at.Line];
            isword = char.IsLetterOrDigit(line[cindex].Character);
            skip = isword;
        }

        while (!isword || skip)
        {
            if (at.Line >= Buffer.GetLines().Count)
            {
                var l = Math.Max(0, Buffer.GetLines().Count - 1);
                return new Coordinate(l, Buffer.GetLineMaxColumn(l));
            }

            var line = Buffer.GetLines()[at.Line];
            if (cindex < line.Count)
            {
                isword = char.IsLetterOrDigit(line[cindex].Character);

                if (isword && !skip)
                {
                    return new Coordinate(at.Line, GetCharacterColumn(at.Line, cindex));
                }

                if (!isword)
                {
                    skip = false;
                }

                cindex++;
            }
            else
            {
                cindex = 0;
                ++at.Line;
                skip = false;
                isword = false;
            }
        }

        return at;
    }

    public int GetLineCharacterCount(int aLine)
    {
        if (aLine >= Buffer.GetLines().Count)
        {
            return 0;
        }

        var line = Buffer.GetLines()[aLine];
        var c = 0;
        for (var i = 0; i < line.Count; c++)
        {
            i += Utf8Helper.UTF8CharLength(line[i].Character);
        }

        return c;
    }

    public bool IsOnWordBoundary(Coordinate aAt)
    {
        if (aAt.Line >= Buffer.GetLines().Count || aAt.Column == 0)
        {
            return true;
        }

        var line = Buffer.GetLines()[aAt.Line];
        var cindex = GetCharacterIndex(aAt);
        if (cindex >= line.Count)
        {
            return true;
        }

        if (Colorizer.Enabled)
        {
            return line[cindex].ColorIndex != line[cindex - 1].ColorIndex;
        }

        return char.IsWhiteSpace(line[cindex].Character) != char.IsWhiteSpace(line[cindex - 1].Character);
    }

    public List<List<Glyph>> GetLines()
    {
        return lines;
    }
}
