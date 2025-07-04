using System.Text;
using Dalamud.Game.Inventory.InventoryEventArgTypes;

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

    public void DeleteGroup()
    {
        if (Buffer.GetLines().Count == 0)
        {
            return;
        }

        var pos = Cursor.GetPosition();
        var width = Buffer.GetLineMaxColumn(pos.Line);
        var height = Buffer.LineCount - 1;

        if (pos.Column == width && pos.Line == height)
        {
            return;
        }

        if (pos.Column == width)
        {
            Delete();
            return;
        }

        UndoManager.Create(() =>
        {
            if (Selection.HasSelection)
            {
                DeleteSelection();
                return;
            }

            var line = GetCurrentLine();
            var target = line.GetGroupedGlyphsAfterCursor(Cursor);
            line.RemoveRange(pos.Column, target.Count);
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

            var merged = new Line();
            merged.AddRange(firstLine.Take(startCol));
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

    public void BackspaceGroup()
    {
        if (Buffer.GetLines().Count == 0)
        {
            return;
        }

        var pos = Cursor.GetPosition();
        if (pos.Column == 0 && pos.Line == 0)
        {
            return;
        }

        if (pos.Column == 0)
        {
            Backspace();
            return;
        }

        UndoManager.Create(() =>
        {
            if (Selection.HasSelection)
            {
                DeleteSelection();
                return;
            }

            var line = GetCurrentLine();
            var target = line.GetGroupedGlyphsBeforeCursor(Cursor);
            line.RemoveRange(pos.Column - target.Count, target.Count);
        });
    }


    public void DeleteSelection()
    {
        if (State.SelectionEnd == State.SelectionStart)
        {
            return;
        }

        var (start, end) = Selection.GetOrderedPositions();

        DeleteRange(start, end);

        Selection.Set(start);
        Cursor.SetPosition(start);
        Colorizer.Colorize(start.Line, 1);
    }
}
