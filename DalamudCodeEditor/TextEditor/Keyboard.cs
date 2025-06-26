using ImGuiNET;

namespace DalamudCodeEditor.TextEditor;

public class Keyboard(Editor editor) : EditorComponent(editor)
{
    public readonly record struct KeyBinding(ImGuiKey Key, bool Ctrl = false, bool Shift = false, bool Alt = false);

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

    private readonly Dictionary<KeyBinding, InputAction> keyBindings = new();

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
        // Helper for createing quick keybinds
        void bind(ImGuiKey key, InputAction action, bool ctrl = false, bool shift = false, bool alt = false)
        {
            keyBindings[new KeyBinding(key, ctrl, shift, alt)] = action;
        }

        bind(ImGuiKey.Z, UndoManager.Undo, true);
        bind(ImGuiKey.Y, UndoManager.Redo, true);
        bind(ImGuiKey.C, Clipboard.Copy, true);
        bind(ImGuiKey.V, Clipboard.Paste, true);
        bind(ImGuiKey.X, Clipboard.Cut, true);
        bind(ImGuiKey.A, editor.SelectAll, true);
        bind(ImGuiKey.Delete, editor.Delete);
        // bind(ImGuiKey.Backspace, editor.Backspace);
        // bind(ImGuiKey.Insert, () => editor.ToggleOverwrite(), ctrl: false, shift: false, alt: false);
        // bind(ImGuiKey.Enter, () => editor.EnterCharacter('\n', false));
        // bind(ImGuiKey.Tab, () => editor.EnterCharacter('\t', Shift), shift: Shift);
        // Navigation
        // bind(ImGuiKey.UpArrow, () => editor.MoveUp(1, Shift));
        // bind(ImGuiKey.DownArrow, () => editor.MoveDown(1, Shift));
        // bind(ImGuiKey.LeftArrow, () => editor.MoveLeft(1, Shift, Ctrl));
        // bind(ImGuiKey.RightArrow, () => editor.MoveRight(1, Shift, Ctrl));
        // bind(ImGuiKey.Home, () => editor.MoveHome(Shift), ctrl: false);
        // bind(ImGuiKey.End, () => editor.MoveEnd(Shift), ctrl: false);
        // bind(ImGuiKey.Home, () => editor.MoveTop(Shift), ctrl: true);
        // bind(ImGuiKey.End, () => editor.MoveBottom(Shift), ctrl: true);
        // bind(ImGuiKey.PageUp, () => editor.MoveUp(editor.GetPageSize() - 4, Shift));
        // bind(ImGuiKey.PageDown, () => editor.MoveDown(editor.GetPageSize() - 4, Shift));
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
    }
}
