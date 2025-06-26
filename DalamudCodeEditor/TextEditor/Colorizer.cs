namespace DalamudCodeEditor.TextEditor;

public class Colorizer(Editor editor) : EditorComponent(editor)
{
    public bool Enabled = true;

    public bool CheckComments = false;

    public int MinLine { get; private set; } = int.MaxValue;

    public int MaxLine { get; private set; } = int.MinValue;

    public void Colorize(int startLine = 0, int lineCount = -1)
    {
        var totalLines = Buffer.LineCount;
        var endLine = lineCount == -1
            ? totalLines
            : Math.Min(totalLines, startLine + lineCount);

        MinLine = Math.Min(MinLine, startLine);
        MaxLine = Math.Min(MaxLine, endLine);

        MinLine = Math.Max(0, MinLine);
        MaxLine = Math.Max(MinLine, MaxLine);


        CheckComments = true;
    }

    public void SetMinLine(int minLine)
    {
        MinLine = minLine;
    }

    public void Reset()
    {
        MinLine = int.MaxValue;
        MaxLine = int.MinValue;
    }
}
