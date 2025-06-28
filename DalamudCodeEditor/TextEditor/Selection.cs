namespace DalamudCodeEditor.TextEditor;

public class Selection(Editor editor) : EditorComponent(editor)
{
    public Coordinate Start { get; private set; } = new();

    public Coordinate End { get; private set; } = new();

    public SelectionMode Mode { get; private set; } = SelectionMode.Normal;

    public bool HasSelection
    {
        get => Start.Line != End.Line || Start.Column != End.Column;
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

    public (Coordinate, Coordinate) GetOrderedPositions()
    {
        var a = State.SelectionStart;
        var b = State.SelectionEnd;
        return a > b ? (b, a) : (a, b);
    }

    public void SelectAll()
    {
        SetStart(new Coordinate(0, 0));
        SetEnd(new Coordinate(Buffer.GetLines().Count, 0));
        State.SetSelection(Start, End);
    }
}
