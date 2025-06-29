using System.Text;

namespace DalamudCodeEditor.TextEditor;

public partial class TextBuffer
{
    public void Delete()
    {
        if (Buffer.GetLines().Count == 0)
        {
            return;
        }

        UndoManager.Create(() =>
        {
            if (Selection.HasSelection)
            {
                DeleteSelection();
            }
            else
            {
                var pos = Cursor.GetPosition();
                Cursor.SetPosition(pos);
                var line = Buffer.GetLines()[pos.Line];

                if (pos.Column == Buffer.GetLineMaxColumn(pos.Line))
                {
                    if (pos.Line == Buffer.GetLines().Count - 1)
                    {
                        return;
                    }

                    var nextLine = Buffer.GetLines()[pos.Line + 1];
                    line.AddRange(nextLine);
                    Buffer.RemoveLine(pos.Line + 1);
                }
                else
                {
                    var cindex = Buffer.GetCharacterIndex(pos);

                    var d = Utf8Helper.UTF8CharLength(line[cindex].Character);
                    while (d-- > 0 && cindex < line.Count)
                    {
                        line.RemoveAt(cindex);
                    }
                }

                Buffer.MarkDirty();
            }
        });
    }


    internal void DeleteRange(Coordinate aStart, Coordinate aEnd)
    {
        if (aStart == aEnd)
        {
            return;
        }

        if (aStart > aEnd)
        {
            (aStart, aEnd) = (aEnd, aStart);
        }

        var lines = Buffer.GetLines();

        if (aStart.Line >= lines.Count || aEnd.Line >= lines.Count)
        {
            return;
        }

        if (aStart.Line == aEnd.Line)
        {
            var line = lines[aStart.Line];
            var startCol = Math.Clamp(aStart.Column, 0, line.Count);
            var endCol = Math.Clamp(aEnd.Column, 0, line.Count);

            line.RemoveRange(startCol, endCol - startCol);
        }
        else
        {
            var firstLine = lines[aStart.Line];
            var lastLine = lines[aEnd.Line];

            var startCol = Math.Clamp(aStart.Column, 0, firstLine.Count);
            var endCol = Math.Clamp(aEnd.Column, 0, lastLine.Count);

            var merged = firstLine.Take(startCol).ToList();

            merged.AddRange(lastLine.Skip(endCol));

            lines[aStart.Line] = merged;

            Buffer.RemoveLine(aStart.Line + 1, aEnd.Line + 1);
        }

        Buffer.MarkDirty();
    }


    public void Backspace()
    {
        if (Buffer.GetLines().Count == 0)
        {
            return;
        }

        UndoManager.Create(() =>
        {
            if (Selection.HasSelection)
            {
                DeleteSelection();
                return;
            }

            var pos = Cursor.GetPosition();

            if (pos.Column == 0)
            {
                if (pos.Line == 0)
                {
                    return;
                }

                var prevLine = pos.Line - 1;
                var prevSize = Buffer.GetLineMaxColumn(prevLine);

                var glyphs = Buffer.GetLine(pos.Line);
                var contents = new StringBuilder();
                foreach (var glyph in glyphs)
                {
                    contents.Append(glyph.Character);
                }

                Buffer.RemoveLine(pos.Line);

                State.CursorPosition.Line = prevLine;
                State.CursorPosition.Column = prevSize;

                Buffer.InsertTextAt(Cursor.GetPosition(), contents.ToString());
            }
            else
            {
                var line = Buffer.GetLines()[pos.Line];
                var cindex = Buffer.GetCharacterIndex(pos);

                if (cindex == 0)
                {
                    return;
                }

                cindex--;


                line.RemoveAt(cindex);

                State.CursorPosition.Column -= GlyphHelper.GetGlyphDisplayWidth(line[cindex >= line.Count ? line.Count - 1 : cindex].Character, Style.TabSize);
                if (State.CursorPosition.Column < 0)
                {
                    State.CursorPosition.Column = 0;
                }
            }

            Buffer.MarkDirty();
            Cursor.EnsureVisible();
            Colorizer.Colorize(State.CursorPosition.Line, 1);
        });
    }


    public void DeleteSelection()
    {
        if (State.SelectionEnd == State.SelectionStart)
        {
            return;
        }

        DeleteRange(State.SelectionStart, State.SelectionEnd);

        Selection.Set(State.SelectionStart);
        Cursor.SetPosition(State.SelectionStart);
        Colorizer.Colorize(State.SelectionStart.Line, 1);
    }
}
