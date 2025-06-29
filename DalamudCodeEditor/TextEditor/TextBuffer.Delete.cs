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
        if (aStart.Line >= Buffer.GetLines().Count || aEnd.Line >= Buffer.GetLines().Count)
        {
            return;
        }

        if (aEnd == aStart)
        {
            return;
        }

        var start = Buffer.GetCharacterIndex(aStart);
        var end = Buffer.GetCharacterIndex(aEnd);

        if (aStart.Line == aEnd.Line)
        {
            var line = Buffer.GetLines()[aStart.Line];
            var n = Buffer.GetLineMaxColumn(aStart.Line);
            if (aEnd.Column >= n)
            {
                line = line.Take(start).ToList();
            }
            else
            {
                line = line.Take(start).Take(end - start).ToList();
            }

            Buffer.GetLines()[aStart.Line] = line;
        }
        else
        {
            var firstLine = Buffer.GetLines()[aStart.Line];
            var lastLine = Buffer.GetLines()[aEnd.Line];

            lastLine = lastLine.TakeLast(lastLine.Count - end).ToList();
            firstLine = firstLine.Take(start).ToList();

            firstLine.AddRange(lastLine);
            Buffer.GetLines()[aStart.Line] = firstLine;
            Buffer.GetLines()[aEnd.Line] = lastLine;

            if (aStart.Line < aEnd.Line)
            {
                firstLine.AddRange(lastLine);
            }

            if (aStart.Line < aEnd.Line)
            {
                Buffer.RemoveLine(aStart.Line + 1, aEnd.Line + 1);
            }
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
