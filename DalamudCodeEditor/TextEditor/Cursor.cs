using ImGuiNET;

namespace DalamudCodeEditor.TextEditor;

public class Cursor(Editor editor) : DirtyTrackable(editor)
{
    public void SetPosition(Coordinate position)
    {
        if (State.CursorPosition != position)
        {
            editor.State.CursorPosition = position;
            MarkDirty();
            EnsureVisible();
        }
    }

    public Coordinate GetPosition()
    {
        return State.CursorPosition.Sanitized(editor);
    }

    public void EnsureVisible()
    {
        if (Renderer.IsRendering)
        {
            Scroll.RequestScrollToCursor();
            return;
        }

        var scrollX = ImGui.GetScrollX();
        var scrollY = ImGui.GetScrollY();

        var height = ImGui.GetWindowHeight();
        var width = ImGui.GetWindowWidth();

        var top = 1 + (int)Math.Ceiling(scrollY / Renderer.LineHeight);
        var bottom = (int)Math.Ceiling((scrollY + height) / Renderer.LineHeight);

        var left = (int)Math.Ceiling(scrollX / Renderer.CharacterWidth);
        var right = (int)Math.Ceiling((scrollX + width) / Renderer.CharacterWidth);

        var pos = GetPosition();
        var len = Buffer.GetDistanceToLineStart(pos);

        if (pos.Line < top)
        {
            ImGui.SetScrollY(Math.Max(0f, (pos.Line - 1) * Renderer.LineHeight));
        }

        if (pos.Line > bottom - 4)
        {
            ImGui.SetScrollY(Math.Max(0f, (pos.Line + 4) * Renderer.LineHeight - height));
        }

        var lineNumberGutterWidth = Renderer.GutterWidth;
        if (len + lineNumberGutterWidth < left + 4)
        {
            ImGui.SetScrollX(Math.Max(0f, len + lineNumberGutterWidth - 4));
        }

        if (len + lineNumberGutterWidth > right - 4)
        {
            ImGui.SetScrollX(Math.Max(0f, len + lineNumberGutterWidth + 4 - width));
        }
    }

    private void MoveCursor(Func<Coordinate, Coordinate> movementFunc)
    {
        var shift = InputManager.Keyboard.Shift;
        var previous = GetPosition();
        var newPosition = movementFunc(previous);

        if (newPosition == previous)
        {
            return;
        }

        SetPosition(newPosition);

        if (shift)
        {
            if (previous == Selection.Start)
            {
                Selection.SetStart(GetPosition());
            }
            else if (previous == Selection.End)
            {
                Selection.SetEnd(GetPosition());
            }
            else
            {
                Selection.SetStart(previous);
                Selection.SetEnd(GetPosition());
            }
        }
        else
        {
            Selection.SetToPoint(GetPosition());
        }

        State.SetSelection(Selection.Start, Selection.End);

        EnsureVisible();
    }

    public void MoveUp(int lines = 1)
    {
        MoveCursor(pos => new Coordinate(Math.Max(0, pos.Line - lines), pos.Column));
    }

    public void MoveDown(int lines = 1)
    {
        MoveCursor(pos => new Coordinate(
            Math.Min(Buffer.GetLines().Count - 1, pos.Line + lines),
            pos.Column));
    }

    public void PageUp()
    {
        MoveUp(Renderer.GetPageSize() - 4);
    }

    public void PageDown()
    {
        MoveDown(Renderer.GetPageSize() - 4);
    }

    public void MoveTop()
    {
        MoveCursor(pos => new Coordinate(0, 0));
    }

    public void MoveBottom()
    {
        var lastLine = Math.Max(0, Buffer.GetLines().Count - 1);
        var lastCol = Buffer.GetLineMaxColumn(lastLine);
        MoveCursor(pos => new Coordinate(lastLine, lastCol));
    }

    public void MoveLeft(int chars = 1)
    {
        var ctrl = InputManager.Keyboard.Ctrl;
        var shift = InputManager.Keyboard.Shift;

        MoveCursor(pos =>
        {
            var line = pos.Line;
            var cindex = Buffer.GetCharacterIndex(pos);

            var amount = chars;

            while (amount-- > 0)
            {
                if (cindex == 0)
                {
                    if (line > 0)
                    {
                        --line;
                        if (Buffer.GetLines().Count > line)
                        {
                            cindex = Buffer.GetLines()[line].Count;
                        }
                        else
                        {
                            cindex = 0;
                        }
                    }
                    else
                    {
                        // At beginning of buffer: stop moving left
                        break;
                    }
                }
                else
                {
                    --cindex;
                    while (cindex > 0 && Utf8Helper.IsUTFSequence(Buffer.GetLines()[line][cindex].Character))
                    {
                        --cindex;
                    }
                }
            }

            var newPos = new Coordinate(line, Buffer.GetCharacterColumn(line, cindex));

            if (ctrl)
            {
                newPos = Buffer.FindWordStart(newPos);
            }

            return newPos.Sanitized(editor);
        });
    }

    public void MoveRight(int chars = 1)
    {
        var ctrl = InputManager.Keyboard.Ctrl;
        var shift = InputManager.Keyboard.Shift;

        MoveCursor(pos =>
        {
            var line = pos.Line;
            var cindex = Buffer.GetCharacterIndex(pos);
            var amount = chars;

            while (amount-- > 0)
            {
                var currentLine = Buffer.GetLines()[line];

                if (cindex >= currentLine.Count)
                {
                    if (line < Buffer.GetLines().Count - 1)
                    {
                        ++line;
                        cindex = 0;
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    cindex += Utf8Helper.UTF8CharLength(currentLine[cindex].Character);
                }
            }

            var newPos = new Coordinate(line, Buffer.GetCharacterColumn(line, cindex));

            if (ctrl)
            {
                newPos = Buffer.FindWordEnd(newPos);
            }

            return newPos.Sanitized(editor);
        });
    }


    public void MoveHome()
    {
        MoveCursor(pos => new Coordinate(pos.Line, 0));
    }

    public void MoveEnd()
    {
        MoveCursor(pos =>
        {
            var maxCol = Buffer.GetLineMaxColumn(pos.Line);
            return new Coordinate(pos.Line, maxCol);
        });
    }
}
