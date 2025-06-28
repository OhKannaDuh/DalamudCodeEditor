using System.Text;
using ImGuiNET;

namespace DalamudCodeEditor.TextEditor;

public partial class TextBuffer(Editor editor) : DirtyTrackable(editor)
{
    private readonly List<List<Glyph>> lines = [[]];

    public int LineCount
    {
        get => lines.Count;
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

        Colorizer.Colorize(0, LineCount);
        Scroll.RequestScrollToTop();
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

    public void Clear()
    {
        MarkDirty();
        lines.Clear();
        lines.Add(new List<Glyph>());
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

    public void EnterCharacter(char c)
    {
        var shift = InputManager.Keyboard.Shift;
        UndoManager.Create(() =>
        {
            if (c == '\t' && Selection.HasSelection && SelectionSpansMultipleLines())
            {
                HandleTabIndentation(shift);
            }
            else
            {
                if (Selection.HasSelection)
                {
                    editor.DeleteSelection();
                }

                InsertCharacterAtCursor(c);
            }
        });

        Buffer.MarkDirty();
        Colorizer.Colorize(Cursor.GetPosition().Line - 1, 3);
        Cursor.EnsureVisible();
    }

    private void HandleTabIndentation(bool shift)
    {
        var (start, end) = Selection.GetOrderedPositions();
        var originalEnd = end;

        start = new Coordinate(start.Line, 0);
        if (end.Column == 0 && end.Line > 0)
        {
            end = new Coordinate(end.Line - 1, Buffer.GetLineMaxColumn(end.Line - 1));
        }
        else
        {
            end = new Coordinate(end.Line, Buffer.GetLineMaxColumn(end.Line));
        }

        var modified = false;

        for (var i = start.Line; i <= end.Line; i++)
        {
            var line = lines[i];
            if (shift)
            {
                if (line.Count > 0)
                {
                    if (line[0].Character == '\t')
                    {
                        line.RemoveAt(0);
                        modified = true;
                    }
                    else
                    {
                        for (var j = 0; j < Style.TabSize && line.Count > 0 && line[0].Character == ' '; j++)
                        {
                            line.RemoveAt(0);
                            modified = true;
                        }
                    }
                }
            }
            else
            {
                line.Insert(0, new Glyph('\t', PaletteIndex.Background));
                modified = true;
            }
        }

        if (!modified)
        {
            return;
        }

        State.SelectionStart = new Coordinate(start.Line, Buffer.GetCharacterColumn(start.Line, 0));
        State.SelectionEnd = originalEnd.Column != 0
            ? new Coordinate(end.Line, Buffer.GetLineMaxColumn(end.Line))
            : new Coordinate(end.Line - 1, Buffer.GetLineMaxColumn(end.Line - 1));
    }

    private bool SelectionSpansMultipleLines()
    {
        return State.SelectionStart.Line != State.SelectionEnd.Line;
    }

    private void InsertCharacterAtCursor(char c)
    {
        var coord = Cursor.GetPosition();
        var line = lines[coord.Line];

        if (c == '\n')
        {
            InsertLine(coord.Line + 1, []);
            var newLine = lines[coord.Line + 1];

            var splitIndex = Buffer.GetCharacterIndex(coord);
            newLine.AddRange(line.Skip(splitIndex));
            line.RemoveRange(splitIndex, line.Count - splitIndex);

            Cursor.SetPosition(new Coordinate(coord.Line + 1, Buffer.GetCharacterColumn(coord.Line + 1, newLine.Count)));
        }
        else
        {
            var buf = new char[7];
            var len = Utf8Helper.ImTextCharToUtf8(ref buf, buf.Length, c);
            if (len <= 0)
            {
                return;
            }

            var insertIndex = Buffer.GetCharacterIndex(coord);

            for (var i = 0; i < len && buf[i] != '\0'; i++)
            {
                line.Insert(insertIndex++, new Glyph(buf[i]));
            }

            Cursor.SetPosition(new Coordinate(coord.Line, Buffer.GetCharacterColumn(coord.Line, insertIndex)));
        }
    }


    public List<List<Glyph>> GetLines()
    {
        return lines;
    }
}
