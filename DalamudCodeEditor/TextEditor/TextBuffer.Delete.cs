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
            (aStart, aEnd) = (aEnd, aStart); // Ensure correct ordering
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

            // Keep the left part of the first line
            var merged = firstLine.Take(startCol).ToList();

            // Append the right part of the last line
            merged.AddRange(lastLine.Skip(endCol));

            // Replace the first line with the merged line
            lines[aStart.Line] = merged;

            // Remove intermediate lines
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
            }
            else
            {
                var pos = Cursor.GetPosition();
                Cursor.SetPosition(pos);

                if (State.CursorPosition.Column == 0)
                {
                    if (State.CursorPosition.Line == 0)
                    {
                        return;
                    }

                    var prevLine = State.CursorPosition.Line - 1;
                    var prevSize = Buffer.GetLineMaxColumn(prevLine);

                    var glyphs = Buffer.GetLine(State.CursorPosition.Line);
                    var contents = "";
                    foreach (var glyph in glyphs)
                    {
                        contents += glyph.Character;
                    }

                    Buffer.RemoveLine(State.CursorPosition.Line);
                    State.CursorPosition.Line = prevLine;
                    State.CursorPosition.Column = prevSize;
                    Buffer.InsertTextAt(Cursor.GetPosition(), contents);
                }
                else
                {
                    var line = Buffer.GetLines()[State.CursorPosition.Line];
                    var cindex = Buffer.GetCharacterIndex(pos) - 1;
                    var cend = cindex + 1;

                    while (cindex > 0 && Utf8Helper.IsUTFSequence(line[cindex].Character))
                    {
                        cindex--;
                    }

                    var charCount = cend - cindex;
                    for (var i = 0; i < charCount && cindex < line.Count; i++)
                    {
                        line.RemoveAt(cindex);
                    }

                    State.CursorPosition.Column--;
                }

                Buffer.MarkDirty();
                Cursor.EnsureVisible();
                Colorizer.Colorize(State.CursorPosition.Line, 1);
            }
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
