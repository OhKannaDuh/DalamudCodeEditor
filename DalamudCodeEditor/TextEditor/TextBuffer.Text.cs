namespace DalamudCodeEditor.TextEditor;

public partial class TextBuffer
{
    public void InsertText(string value)
    {
        if (value.Trim() == "")
        {
            return;
        }

        var pos = Cursor.GetPosition();
        var start = pos < State.SelectionStart ? pos : State.SelectionStart;
        var totalLines = pos.Line - start.Line;

        totalLines += InsertTextAt(pos, value);

        Selection.Set(pos);
        Cursor.SetPosition(pos);
        Colorizer.Colorize(start.Line - 1, totalLines + 2);
    }

    public int InsertTextAt(Coordinate where, string value)
    {
        MarkDirty();
        return TextInsertionHelper.InsertTextAt(lines, where, value, Style.TabSize);
    }
}
