using ImGuiNET;

namespace DalamudCodeEditor.TextEditor;

public class Keyboard(Editor editor) : EditorComponent(editor)
{
    public delegate void InputAction();

    private InputAction RequireWritable(InputAction action)
    {
        return () =>
        {
            if (!editor.IsReadOnly)
            {
                action();
            }
        };
    }

    private readonly List<(KeyBinding binding, InputAction action)> KeyBindings = new();

    public ImGuiIOPtr IO
    {
        get => ImGui.GetIO();
    }

    public bool Shift
    {
        get => IO.KeyShift;
    }

    public bool Ctrl
    {
        get => IO.ConfigMacOSXBehaviors ? IO.KeySuper : IO.KeyCtrl;
    }

    public bool Alt
    {
        get => IO.ConfigMacOSXBehaviors ? IO.KeyCtrl : IO.KeyAlt;
    }

    public void InitializeKeyboardBindings()
    {
        // Undo/Redo
        KeyBindings.Add((new KeyBinding(ImGuiKey.Z).CtrlDown(), RequireWritable(UndoManager.Undo)));
        KeyBindings.Add((new KeyBinding(ImGuiKey.Backspace).AltDown(), RequireWritable(UndoManager.Undo)));
        KeyBindings.Add((new KeyBinding(ImGuiKey.Y).CtrlDown(), RequireWritable(UndoManager.Redo)));

        // Cursor Movement
        KeyBindings.Add((new KeyBinding(ImGuiKey.UpArrow).ShiftIgnored(), () => Cursor.MoveUp()));
        KeyBindings.Add((new KeyBinding(ImGuiKey.DownArrow).ShiftIgnored(), () => Cursor.MoveDown()));
        KeyBindings.Add((new KeyBinding(ImGuiKey.LeftArrow).ShiftIgnored().CtrlIgnored(), () => Cursor.MoveLeft()));
        KeyBindings.Add((new KeyBinding(ImGuiKey.RightArrow).ShiftIgnored().CtrlIgnored(), () => Cursor.MoveRight()));
        KeyBindings.Add((new KeyBinding(ImGuiKey.PageUp).ShiftIgnored().CtrlIgnored(), Cursor.PageUp));
        KeyBindings.Add((new KeyBinding(ImGuiKey.PageDown).ShiftIgnored().CtrlIgnored(), Cursor.PageDown));
        KeyBindings.Add((new KeyBinding(ImGuiKey.Home).CtrlDown().ShiftIgnored(), Cursor.MoveTop));
        KeyBindings.Add((new KeyBinding(ImGuiKey.End).CtrlDown().ShiftIgnored(), Cursor.MoveBottom));
        KeyBindings.Add((new KeyBinding(ImGuiKey.Home).ShiftIgnored(), Cursor.MoveHome));
        KeyBindings.Add((new KeyBinding(ImGuiKey.End).ShiftIgnored(), Cursor.MoveEnd));

        // Other
        KeyBindings.Add((new KeyBinding(ImGuiKey.Delete), RequireWritable(editor.Delete)));
        KeyBindings.Add((new KeyBinding(ImGuiKey.Backspace), RequireWritable(editor.Backspace)));
        KeyBindings.Add((new KeyBinding(ImGuiKey.Insert), editor.ToggleOverwrite));
        KeyBindings.Add((new KeyBinding(ImGuiKey.A).CtrlDown(), Selection.SelectAll));
        KeyBindings.Add((new KeyBinding(ImGuiKey.Enter), RequireWritable(() =>
        {
            Buffer.EnterCharacter('\n');
            var pos = Cursor.GetPosition();
            pos.Column = 0;
            Cursor.SetPosition(pos);
        })));
        KeyBindings.Add((new KeyBinding(ImGuiKey.Tab).ShiftIgnored(), RequireWritable(() => Buffer.EnterCharacter('\t'))));

        // Clipboard
        KeyBindings.Add((new KeyBinding(ImGuiKey.C).CtrlDown(), Clipboard.Copy));
        KeyBindings.Add((new KeyBinding(ImGuiKey.Insert).CtrlDown(), Clipboard.Copy));
        KeyBindings.Add((new KeyBinding(ImGuiKey.V).CtrlDown(), Clipboard.Paste));
        KeyBindings.Add((new KeyBinding(ImGuiKey.Insert).ShiftDown(), Clipboard.Paste));
        KeyBindings.Add((new KeyBinding(ImGuiKey.X).CtrlDown(), Clipboard.Cut));
        KeyBindings.Add((new KeyBinding(ImGuiKey.Delete).ShiftDown(), Clipboard.Cut));
    }

    public void HandleInput()
    {
        if (!ImGui.IsWindowFocused())
        {
            return;
        }

        if (ImGui.IsWindowHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.TextInput);
        }

        IO.WantCaptureKeyboard = true;
        IO.WantTextInput = true;

        foreach (var (binding, action) in KeyBindings)
        {
            if (ImGui.IsKeyPressed(binding.Key) && binding.Matches(Ctrl, Shift, Alt))
            {
                action();
                break;
            }
        }

        if (editor.IsReadOnly)
        {
            return;
        }

        for (var i = 0; i < ImGui.GetIO().InputQueueCharacters.Size; i++)
        {
            var c = ImGui.GetIO().InputQueueCharacters[i];
            if (c != 0 && (c == '\n' || c >= 32))
            {
                Buffer.EnterCharacter((char)c);
            }
        }
    }
}
