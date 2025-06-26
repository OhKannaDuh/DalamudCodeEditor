using System.Numerics;
using ImGuiNET;

namespace DalamudCodeEditor.TextEditor;

public class Mouse(Editor editor) : EditorComponent(editor)
{
    private float LastClick = -1f;

    private ImGuiIOPtr IO
    {
        get => ImGui.GetIO();
    }

    private Keyboard Keyboard
    {
        get => InputManager.Keyboard;
    }

    public Vector2 Position
    {
        get => ImGui.GetMousePos();
    }

    public bool LeftDown
    {
        get => ImGui.IsMouseDown(ImGuiMouseButton.Left);
    }

    public bool LeftDrag
    {
        get => ImGui.IsMouseDragging(ImGuiMouseButton.Left);
    }

    public class MouseState
    {
        public bool Click { get; init; } = false;

        public bool DoubleClick { get; init; } = false;

        public bool TripleClick { get; init; } = false;
    }

    public MouseState GetState()
    {
        var time = ImGui.GetTime();

        var click = ImGui.IsMouseClicked(ImGuiMouseButton.Left);

        var doubleClick = ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left);

        var tripleClick = click && !doubleClick && LastClick != -1f &&
                          time - LastClick < IO.MouseDoubleClickTime;

        var state = new MouseState
        {
            Click = click,
            DoubleClick = doubleClick,
            TripleClick = tripleClick,
        };

        if (state.TripleClick)
        {
            LastClick = -1f;
        }
        else if (state.DoubleClick || state.Click)
        {
            LastClick = (float)time;
        }

        return state;
    }

    public void HandleInput()
    {
        if (!ImGui.IsWindowHovered() || Keyboard.Shift || Keyboard.Alt)
        {
            return;
        }

        var state = GetState();
        var position = Screen.ScreenPositionToCoordinates(Position, editor);

        if (!Keyboard.Ctrl)
        {
            if (state.TripleClick)
            {
                Cursor.SetPosition(position);
                Selection.SetToPoint(position);
                Selection.SetMode(SelectionMode.Normal);

                State.SetSelection(Selection.Start, Selection.End, Selection.Mode);

                return;
            }

            if (state.DoubleClick)
            {
                Cursor.SetPosition(position);
                Selection.SetToPoint(position);

                var isLine = Selection.Mode == SelectionMode.Line;
                Selection.SetMode(isLine ? SelectionMode.Normal : SelectionMode.Word);

                State.SetSelection(Selection.Start, Selection.End, Selection.Mode);

                return;
            }
        }

        if (state.Click)
        {
            Cursor.SetPosition(position);
            Selection.SetToPoint(position);
            Selection.SetMode(Keyboard.Ctrl ? SelectionMode.Word : SelectionMode.Normal);

            State.SetSelection(Selection.Start, Selection.End, Selection.Mode);

            return;
        }

        if (LeftDown && LeftDrag)
        {
            IO.WantCaptureMouse = true;
            Cursor.SetPosition(position);
            Selection.SetEnd(position);

            State.SetSelection(Selection.Start, Selection.End, Selection.Mode);
        }
    }
}
