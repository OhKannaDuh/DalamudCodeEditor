namespace DalamudCodeEditor.TextEditor;

public abstract class EditorComponent(Editor editor)
{
    public TextBuffer Buffer
    {
        get => editor.Buffer;
    }

    public Style Style
    {
        get => editor.Style;
    }

    public Renderer Renderer
    {
        get => editor.Renderer;
    }

    public Colorizer Colorizer
    {
        get => editor.Colorizer;
    }

    public UndoManager UndoManager
    {
        get => editor.UndoManager;
    }

    public InputManager InputManager
    {
        get => editor.InputManager;
    }

    public Cursor Cursor
    {
        get => editor.Cursor;
    }

    public Scroll Scroll
    {
        get => editor.Scroll;
    }

    public Selection Selection
    {
        get => editor.Selection;
    }

    public Clipboard Clipboard
    {
        get => editor.Clipboard;
    }

    public State State
    {
        get => editor.State;
    }
}
