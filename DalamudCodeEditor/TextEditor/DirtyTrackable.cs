namespace DalamudCodeEditor.TextEditor;

public abstract class DirtyTrackable(Editor editor) : EditorComponent(editor)
{
    private bool isDirty = false;

    public bool IsDirty
    {
        get => isDirty;
    }

    public void MarkClean()
    {
        isDirty = false;
    }

    public void MarkDirty()
    {
        isDirty = true;
    }
}
