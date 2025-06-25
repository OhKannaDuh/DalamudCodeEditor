namespace DalamudCodeEditor;

public class UndoRecord
{
    public string mAdded = "";
    public Coordinates mAddedEnd = new();
    public Coordinates mAddedStart = new();
    public EditorState mAfter = new();

    public EditorState mBefore = new();

    public string mRemoved = "";
    public Coordinates mRemovedEnd = new();
    public Coordinates mRemovedStart = new();

    public UndoRecord()
    {
    }
    //~UndoRecord() {}

    public UndoRecord(
        string aAdded,
        Coordinates aAddedStart,
        Coordinates aAddedEnd,
        string aRemoved,
        Coordinates aRemovedStart,
        Coordinates aRemovedEnd,
        ref EditorState aBefore,
        ref EditorState aAfter)
    {
        mAdded = aAdded;
        mAddedStart = aAddedStart;
        mAddedEnd = aAddedEnd;

        mRemoved = aRemoved;
        mRemovedStart = aRemovedStart;
        mRemovedEnd = aRemovedEnd;

        mBefore = aBefore;
        mAfter = aAfter;
    }

    public void Undo(TextEditor editor)
    {
        editor.mState = mBefore;

        if (!string.IsNullOrEmpty(mAdded))
            editor.DeleteRange(mAddedStart, mAddedEnd);

        if (!string.IsNullOrEmpty(mRemoved))
            editor.InsertTextAt(mRemovedStart, mRemoved);

        editor.Colorize(Math.Max(0, mRemovedStart.mLine - 1),
            Math.Max(1, mRemovedEnd.mLine - mRemovedStart.mLine + 2));
        editor.EnsureCursorVisible();
    }

    public void Redo(TextEditor editor)
    {
        editor.mState = mAfter;

        if (!string.IsNullOrEmpty(mRemoved))
            editor.DeleteRange(mRemovedStart, mRemovedEnd);

        if (!string.IsNullOrEmpty(mAdded))
            editor.InsertTextAt(mAddedStart, mAdded);

        editor.Colorize(Math.Max(0, mAddedStart.mLine - 1),
            Math.Max(1, mAddedEnd.mLine - mAddedStart.mLine + 2));
        editor.EnsureCursorVisible();
    }
}