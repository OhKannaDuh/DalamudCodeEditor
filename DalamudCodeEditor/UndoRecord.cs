using DalamudCodeEditor.TextEditor;

namespace DalamudCodeEditor;

public class UndoRecord()
{
    public string Added = "";

    public Coordinate AddedEnd = new();

    public Coordinate AddedStart = new();

    public State After;

    public State Before;

    public string Removed = "";

    public Coordinate RemovedEnd = new();

    public Coordinate RemovedStart = new();

    public void Undo(Editor editor)
    {
        editor.State = Before;

        if (!string.IsNullOrEmpty(Added))
        {
            editor.DeleteRange(AddedStart, AddedEnd);
        }

        if (!string.IsNullOrEmpty(Removed))
        {
            editor.InsertTextAt(RemovedStart, Removed);
        }

        editor.Colorizer.Colorize(Math.Max(0, RemovedStart.Line - 1),
            Math.Max(1, RemovedEnd.Line - RemovedStart.Line + 2));
        editor.Cursor.EnsureVisible();
    }

    public void Redo(Editor editor)
    {
        editor.State = After;

        if (!string.IsNullOrEmpty(Removed))
        {
            editor.DeleteRange(RemovedStart, RemovedEnd);
        }

        if (!string.IsNullOrEmpty(Added))
        {
            editor.InsertTextAt(AddedStart, Added);
        }

        editor.Colorizer.Colorize(Math.Max(0, AddedStart.Line - 1),
            Math.Max(1, AddedEnd.Line - AddedStart.Line + 2));
        editor.Cursor.EnsureVisible();
    }
}
