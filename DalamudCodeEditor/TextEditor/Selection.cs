namespace DalamudCodeEditor.TextEditor;

public class Selection(Editor editor) : EditorComponent(editor)
{
    public Coordinate Start { get; private set; } = new();

    public Coordinate End { get; private set; } = new();

    public SelectionMode Mode { get; private set; } = SelectionMode.Normal;

    public bool HasSelection
    {
        get => End > Start;
    }

    public string Text
    {
        get => Buffer.GetText(Start, End);
    }

    public void SetStart(Coordinate start)
    {
        Start = start;
    }

    public void SetEnd(Coordinate end)
    {
        End = end;
    }

    public void SetToPoint(Coordinate point)
    {
        SetStart(point);
        SetEnd(point);
    }

    public void SetMode(SelectionMode mode)
    {
        Mode = mode;
    }
}
