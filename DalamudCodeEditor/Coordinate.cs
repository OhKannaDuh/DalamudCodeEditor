using DalamudCodeEditor.TextEditor;

namespace DalamudCodeEditor;

public class Coordinate(int line = 0, int column = 0)
{
    public int Line = line;

    public int Column = column;

    private static Coordinate Invalid()
    {
        return new Coordinate(-1, -1);
    }

    public static bool operator ==(Coordinate a, Coordinate o)
    {
        return a.Line == o.Line && a.Column == o.Column;
    }

    public static bool operator !=(Coordinate a, Coordinate o)
    {
        return a.Line != o.Line || a.Column != o.Column;
    }

    public static bool operator <(Coordinate a, Coordinate o)
    {
        if (a.Line != o.Line)
        {
            return a.Line < o.Line;
        }

        return a.Column < o.Column;
    }

    public static bool operator >(Coordinate a, Coordinate o)
    {
        if (a.Line != o.Line)
        {
            return a.Line > o.Line;
        }

        return a.Column > o.Column;
    }

    public static bool operator <=(Coordinate a, Coordinate o)
    {
        if (a.Line != o.Line)
        {
            return a.Line < o.Line;
        }

        return a.Column <= o.Column;
    }

    public static bool operator >=(Coordinate a, Coordinate o)
    {
        if (a.Line != o.Line)
        {
            return a.Line > o.Line;
        }

        return a.Column >= o.Column;
    }

    public override bool Equals(object? obj)
    {
        return obj is Coordinate o && Line == o.Line && Column == o.Column;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Line, Column);
    }

    public Coordinate Sanitized(Editor editor)
    {
        var sLine = Line;
        var sColumn = Column;
        var lineCount = editor.Buffer.LineCount;

        if (sLine >= lineCount)
        {
            if (lineCount == 0)
            {
                sLine = 0;
                sColumn = 0;
            }
            else
            {
                sLine = lineCount - 1;
                sColumn = editor.Buffer.GetLineMaxColumn(sLine);
            }

            return new Coordinate(sLine, sColumn);
        }

        sColumn = lineCount == 0 ? 0 : Math.Min(sColumn, editor.Buffer.GetLineMaxColumn(sLine));
        return new Coordinate(sLine, sColumn);
    }
}
