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
            Coordinate anchor;

            if (Selection.End == previous)
            {
                anchor = Selection.Start;
            }
            else if (Selection.Start == previous)
            {
                anchor = Selection.End;
            }
            else
            {
                anchor = previous;
            }

            Selection.Set(anchor, newPosition);
        }
        else
        {
            Selection.SetToPoint(GetPosition());
        }

        EnsureVisible();
    }

    public void MoveUp(int delta = 1)
    {
        MoveCursor(pos =>
        {
            if (pos.Line == 1)
            {
                return pos.ToHome();
            }

            return pos.WithLine(Math.Max(0, pos.Line - delta));
        });
    }

    public void MoveDown(int delta = 1)
    {
        MoveCursor(pos =>
        {
            if (pos.Line == Buffer.LineCount)
            {
                return pos.ToHome();
            }


            return pos.WithLine(Math.Min(Buffer.LineCount - 1, pos.Line + delta));
        });
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
        MoveCursor(pos => pos.ToHome().ToFirstLine());
    }

    public void MoveBottom()
    {
        MoveCursor(pos => pos.ToLastLine(editor));
    }

    public void MoveLeft(int chars = 1)
    {
        var ctrl = InputManager.Keyboard.Ctrl;

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
                        cindex = Buffer.GetLines()[line].Count;
                    }
                    else
                    {
                        break; // At start of buffer
                    }
                }
                else
                {
                    --cindex;
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

        MoveCursor(pos =>
        {
            var line = pos.Line;
            var cindex = Buffer.GetCharacterIndex(pos);
            var amount = chars;

            while (amount-- > 0)
            {
                var lines = Buffer.GetLines();

                if (line >= lines.Count)
                {
                    break;
                }

                var currentLine = lines[line];

                if (cindex >= currentLine.Count)
                {
                    if (line < lines.Count - 1)
                    {
                        ++line;
                        cindex = 0;
                    }
                    else
                    {
                        break; // At end of buffer
                    }
                }
                else
                {
                    ++cindex;
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
        MoveCursor(pos => pos.ToHome());
    }

    public void MoveEnd()
    {
        MoveCursor(pos => pos.ToEnd(editor));
    }
}
