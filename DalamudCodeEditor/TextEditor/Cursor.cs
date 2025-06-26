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

    public void MoveUp(int lines = 1)
    {
        var shift = InputManager.Keyboard.Shift;

        var previous = State.CursorPosition;
        State.CursorPosition.Line = Math.Max(0, State.CursorPosition.Line - lines);
        if (previous == State.CursorPosition)
        {
            return;
        }

        if (shift)
        {
            if (previous == Selection.Start)
            {
                Selection.SetStart(Cursor.GetPosition());
            }
            else if (previous == Selection.End)
            {
                Selection.SetEnd(Cursor.GetPosition());
            }
            else
            {
                Selection.SetStart(Cursor.GetPosition());
                Selection.SetEnd(previous);
            }
        }
        else
        {
            Selection.SetToPoint(Cursor.GetPosition());
        }

        State.SetSelection(Selection.Start, Selection.End);
        Cursor.EnsureVisible();
    }

    public void MoveTop()
    {
        MoveUp(Renderer.GetPageSize() - 4);
    }

    public void MoveDown(int lines = 1)
    {
        var shift = InputManager.Keyboard.Shift;
    }

    public void MoveBottom()
    {
        MoveUp(Renderer.GetPageSize() - 4);
    }
}
