using ImGuiNET;

namespace DalamudCodeEditor.TextEditor;

public partial class TextBuffer
{
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

    public Coordinate FindWordStart(Coordinate aFrom)
    {
        var at = aFrom;
        if (at.Line >= lines.Count)
        {
            return at;
        }

        var line = lines[at.Line];
        var cindex = GetCharacterIndex(at);

        if (cindex >= line.Count)
        {
            return at;
        }

        while (cindex > 0 && char.IsWhiteSpace(line[cindex].Character))
        {
            --cindex;
        }

        var cstart = line[cindex].Color;
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

                if (cstart != line[cindex - 1].Color)
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
        if (at.Line >= lines.Count)
        {
            return at;
        }

        var line = lines[at.Line];
        var cindex = GetCharacterIndex(at);

        if (cindex >= line.Count)
        {
            return at;
        }

        var prevspace = char.IsWhiteSpace(line[cindex].Character);
        var cstart = line[cindex].Color;
        while (cindex < line.Count)
        {
            var c = line[cindex].Character;
            var d = Utf8Helper.UTF8CharLength(c);
            if (cstart != line[cindex].Color)
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
        if (aCoordinates.Line >= lines.Count)
        {
            return -1;
        }

        var line = lines[aCoordinates.Line];
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
        if (aLine >= lines.Count)
        {
            return 0;
        }

        var line = lines[aLine];
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

    public bool IsOnWordBoundary(Coordinate aAt)
    {
        if (aAt.Line >= lines.Count || aAt.Column == 0)
        {
            return true;
        }

        var line = lines[aAt.Line];
        var cindex = GetCharacterIndex(aAt);
        if (cindex >= line.Count)
        {
            return true;
        }

        if (Colorizer.Enabled)
        {
            return line[cindex].Color != line[cindex - 1].Color;
        }

        return char.IsWhiteSpace(line[cindex].Character) != char.IsWhiteSpace(line[cindex - 1].Character);
    }

    public float TextDistanceToLineStart(Coordinate aFrom)
    {
        var line = Buffer.GetLines()[aFrom.Line];
        var distance = 0.0f;
        //float spaceSize = ImGui::GetFont()->CalcTextSizeA(ImGui::GetFontSize(), FLT_MAX, -1.0f, " ", nullptr, nullptr).x;
        var spaceSize = ImGui.CalcTextSize(" ").X; // Not sure if that's correct
        var colIndex = Buffer.GetCharacterIndex(aFrom);
        for (var it = 0; it < line.Count && it < colIndex;)
        {
            if (line[it].Character == '\t')
            {
                distance = (float)(1.0f + Math.Floor((1.0f + distance) / (Style.TabSize * spaceSize))) *
                           (Style.TabSize * spaceSize);
                ++it;
            }
            else
            {
                var d = Utf8Helper.UTF8CharLength(line[it].Character);
                var l = d;
                var tempCString = new char[7];
                var i = 0;
                for (; i < 6 && d-- > 0 && it < line.Count; i++, it++)
                {
                    tempCString[i] = line[it].Character;
                }

                tempCString[i] = '\0';
                if (l > 0)
                {
                    distance += ImGui.CalcTextSize(new string(tempCString, 0, l)).X;
                }
            }
        }

        return distance;
    }
}
