using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using ImGuiNET;

namespace DalamudCodeEditor.TextEditor;

using Line = List<Glyph>;

public partial class Editor
{
    public long mStartTime;

    public bool IsOverwrite = false;

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

        State.SetSelection(pos, pos);
        Cursor.SetPosition(pos);
        Colorizer.Colorize(start.Line - 1, totalLines + 2);
    }

    public void MoveUp(int aAmount = 1, bool aSelect = false)
    {
        var oldPos = State.CursorPosition;

        State.CursorPosition.Line = Math.Max(0, State.CursorPosition.Line - aAmount);

        if (oldPos != State.CursorPosition)
        {
            if (aSelect)
            {
                if (oldPos == Selection.Start)
                {
                    // Selection.Start = mState.CursorPosition;
                    Selection.SetStart(Cursor.GetPosition());
                }
                else if (oldPos == Selection.End)
                {
                    // Selection.End = mState.CursorPosition;
                    Selection.SetEnd(Cursor.GetPosition());
                }
                else
                {
                    // Selection.Start = mState.CursorPosition;
                    Selection.SetStart(Cursor.GetPosition());
                    Selection.SetEnd(oldPos);
                }
            }
            else
            {
                // Selection.Start = Selection.End = mState.CursorPosition;
                Selection.SetToPoint(Cursor.GetPosition());
            }

            State.SetSelection(Selection.Start, Selection.End);
            Cursor.EnsureVisible();
        }
    }


    public void MoveDown(int aAmount = 1, bool aSelect = false)
    {
        var oldPos = State.CursorPosition;
        State.CursorPosition.Line = Math.Max(0, Math.Min(Buffer.GetLines().Count - 1, State.CursorPosition.Line + aAmount));

        if (aSelect)
        {
            if (oldPos == Selection.End)
            {
                Selection.SetEnd(State.CursorPosition);
            }
            else if (oldPos == Selection.Start)
            {
                Selection.SetStart(State.CursorPosition);
            }
            else
            {
                Selection.SetStart(oldPos);
                Selection.SetEnd(State.CursorPosition);
            }
        }
        else
        {
            Selection.SetToPoint(State.CursorPosition);
        }

        State.SetSelection(Selection.Start, Selection.End);
        Cursor.EnsureVisible();
    }


    public void MoveLeft(int aAmount = 1, bool aSelect = false, bool aWordMode = false)
    {
        if (Buffer.GetLines().Count == 0)
        {
            return;
        }

        var oldPos = State.CursorPosition;
        State.CursorPosition = Cursor.GetPosition();
        var line = State.CursorPosition.Line;
        var cindex = Buffer.GetCharacterIndex(State.CursorPosition);

        while (aAmount-- > 0)
        {
            if (cindex == 0)
            {
                if (line > 0)
                {
                    --line;
                    if (Buffer.GetLines().Count > line)
                    {
                        cindex = Buffer.GetLines()[line].Count;
                    }
                    else
                    {
                        cindex = 0;
                    }
                }
            }
            else
            {
                --cindex;
                if (cindex > 0)
                {
                    if (Buffer.GetLines().Count > line)
                    {
                        while (cindex > 0 && Utf8Helper.IsUTFSequence(Buffer.GetLines()[line][cindex].Character))
                        {
                            --cindex;
                        }
                    }
                }
            }

            State.CursorPosition = new Coordinate(line, Buffer.GetCharacterColumn(line, cindex));
            if (aWordMode)
            {
                State.CursorPosition = Buffer.FindWordStart(State.CursorPosition);
                cindex = Buffer.GetCharacterIndex(State.CursorPosition);
            }
        }

        State.CursorPosition = new Coordinate(line, Buffer.GetCharacterColumn(line, cindex));

        // assert(mState.mCursorPosition.mColumn >= 0);
        if (aSelect)
        {
            if (oldPos == Selection.Start)
            {
                Selection.SetStart(State.CursorPosition);
            }
            else if (oldPos == Selection.End)
            {
                Selection.SetEnd(State.CursorPosition);
            }
            else
            {
                Selection.SetStart(State.CursorPosition);
                Selection.SetEnd(oldPos);
            }
        }
        else
        {
            Selection.SetToPoint(State.CursorPosition);
        }

        State.SetSelection(Selection.Start, Selection.End,
            aSelect && aWordMode ? SelectionMode.Word : SelectionMode.Normal);

        Cursor.EnsureVisible();
    }

    public void MoveRight(int aAmount = 1, bool aSelect = false, bool aWordMode = false)
    {
        var oldPos = State.CursorPosition;

        if (Buffer.GetLines().Count == 0 || oldPos.Line >= Buffer.GetLines().Count)
        {
            return;
        }

        var cindex = Buffer.GetCharacterIndex(State.CursorPosition);
        while (aAmount-- > 0)
        {
            var lindex = State.CursorPosition.Line;
            var line = Buffer.GetLines()[lindex];

            if (cindex >= line.Count)
            {
                if (State.CursorPosition.Line < Buffer.GetLines().Count - 1)
                {
                    State.CursorPosition.Line =
                        Math.Max(0, Math.Min(Buffer.GetLines().Count - 1, State.CursorPosition.Line + 1));
                    State.CursorPosition.Column = 0;
                }
                else
                {
                    return;
                }
            }
            else
            {
                cindex += Utf8Helper.UTF8CharLength(line[cindex].Character);
                State.CursorPosition = new Coordinate(lindex, Buffer.GetCharacterColumn(lindex, cindex));
                if (aWordMode)
                {
                    State.CursorPosition = Buffer.FindNextWord(State.CursorPosition);
                }
            }
        }

        if (aSelect)
        {
            if (oldPos == Selection.End)
            {
                Selection.SetEnd(Cursor.GetPosition());
            }
            else if (oldPos == Selection.Start)
            {
                Selection.SetStart(State.CursorPosition);
            }
            else
            {
                Selection.SetStart(oldPos);
                Selection.SetEnd(State.CursorPosition);
            }
        }
        else
        {
            Selection.SetToPoint(State.CursorPosition);
        }

        State.SetSelection(Selection.Start, Selection.End,
            aSelect && aWordMode ? SelectionMode.Word : SelectionMode.Normal);

        Cursor.EnsureVisible();
    }

    public void MoveTop(bool aSelect = false)
    {
        var oldPos = State.CursorPosition;
        Cursor.SetPosition(new Coordinate(0, 0));

        if (State.CursorPosition != oldPos)
        {
            if (aSelect)
            {
                Selection.SetEnd(oldPos);
                Selection.SetStart(State.CursorPosition);
            }
            else
            {
                Selection.SetToPoint(State.CursorPosition);
            }

            State.SetSelection(Selection.Start, Selection.End);
        }
    }

    public void MoveBottom(bool aSelect = false)
    {
        var oldPos = Cursor.GetPosition();
        var newPos = new Coordinate(Buffer.GetLines().Count - 1, 0);
        Cursor.SetPosition(newPos);
        if (aSelect)
        {
            Selection.SetStart(oldPos);
            Selection.SetEnd(newPos);
        }
        else
        {
            Selection.SetToPoint(newPos);
        }

        State.SetSelection(Selection.Start, Selection.End);
    }

    public void MoveHome(bool aSelect = false)
    {
        var oldPos = State.CursorPosition;
        Cursor.SetPosition(new Coordinate(State.CursorPosition.Line, 0));

        if (State.CursorPosition != oldPos)
        {
            if (aSelect)
            {
                if (oldPos == Selection.Start)
                {
                    Selection.SetStart(State.CursorPosition);
                }
                else if (oldPos == Selection.End)
                {
                    Selection.SetEnd(State.CursorPosition);
                }
                else
                {
                    Selection.SetStart(State.CursorPosition);
                    Selection.SetEnd(oldPos);
                }
            }
            else
            {
                Selection.SetToPoint(State.CursorPosition);
            }

            State.SetSelection(Selection.Start, Selection.End);
        }
    }

    public void MoveEnd(bool aSelect = false)
    {
        var oldPos = State.CursorPosition;
        Cursor.SetPosition(new Coordinate(State.CursorPosition.Line, Buffer.GetLineMaxColumn(oldPos.Line)));

        if (State.CursorPosition != oldPos)
        {
            if (aSelect)
            {
                if (oldPos == Selection.End)
                {
                    Selection.SetEnd(State.CursorPosition);
                }
                else if (oldPos == Selection.Start)
                {
                    Selection.SetStart(State.CursorPosition);
                }
                else
                {
                    Selection.SetStart(oldPos);
                    Selection.SetEnd(State.CursorPosition);
                }
            }
            else
            {
                Selection.SetToPoint(State.CursorPosition);
            }

            State.SetSelection(Selection.Start, Selection.End);
        }
    }


    public void SelectAll()
    {
        State.SetSelection(new Coordinate(0, 0), new Coordinate(Buffer.GetLines().Count, 0));
    }

    public void Delete()
    {
        if (Buffer.GetLines().Count == 0)
        {
            return;
        }

        UndoRecord u = new();
        u.Before = State.Clone();

        if (Selection.HasSelection)
        {
            u.Removed = Selection.Text;
            u.RemovedStart = State.SelectionStart;
            u.RemovedEnd = State.SelectionEnd;

            DeleteSelection();
        }
        else
        {
            var pos = Cursor.GetPosition();
            Cursor.SetPosition(pos);
            var line = Buffer.GetLines()[pos.Line];

            if (pos.Column == Buffer.GetLineMaxColumn(pos.Line))
            {
                if (pos.Line == Buffer.GetLines().Count - 1)
                {
                    return;
                }

                u.Removed = "\n";
                u.RemovedStart = u.RemovedEnd = Cursor.GetPosition();
                Advance(u.RemovedEnd);

                var nextLine = Buffer.GetLines()[pos.Line + 1];
                line.AddRange(nextLine);
                RemoveLine(pos.Line + 1);
            }
            else
            {
                var cindex = Buffer.GetCharacterIndex(pos);
                u.RemovedStart = u.RemovedEnd = Cursor.GetPosition();
                u.RemovedEnd.Column++;
                u.Removed = Buffer.GetText(u.RemovedStart, u.RemovedEnd);

                var d = Utf8Helper.UTF8CharLength(line[cindex].Character);
                while (d-- > 0 && cindex < line.Count)
                {
                    line.RemoveAt(cindex);
                }
            }

            Buffer.MarkDirty();

            Colorizer.Colorize(pos.Line, 1);
        }

        u.After = State.Clone();
        UndoManager.AddUndo(u);
    }

    internal void ColorizeRange(int fromLine = 0, int toLine = 0)
    {
        if (Buffer.GetLines().Count == 0 || fromLine >= toLine)
        {
            return;
        }

        var endLine = Math.Min(toLine, Buffer.GetLines().Count);

        for (var lineIndex = fromLine; lineIndex < endLine; lineIndex++)
        {
            var line = Buffer.GetLines()[lineIndex];
            if (line.Count == 0)
            {
                continue;
            }

            // Build plain string from glyphs
            var lineText = new StringBuilder(line.Count);
            for (var i = 0; i < line.Count; i++)
            {
                lineText.Append(line[i].Character);
                var glyph = line[i];
                glyph.ColorIndex = PaletteIndex.Default;
                line[i] = glyph;
            }

            var buffer = lineText.ToString();

            if (languageDefinition.TokenizeLine != null)
            {
                foreach (var token in languageDefinition.TokenizeLine(buffer))
                {
                    for (var i = token.Start; i < token.End && i < line.Count; i++)
                    {
                        var glyph = line[i];
                        glyph.ColorIndex = token.Color;
                        line[i] = glyph;
                    }
                }
            }
            else
            {
                // Regex fallback
                foreach (var (regex, color) in languageDefinition.RegexTokens)
                {
                    foreach (Match match in regex.Matches(buffer))
                    {
                        var start = match.Index;
                        var end = start + match.Length;

                        for (var i = start; i < end && i < line.Count; i++)
                        {
                            var glyph = line[i];
                            glyph.ColorIndex = color;
                            line[i] = glyph;
                        }
                    }
                }

                // Post-process for keywords or known identifiers
                for (var i = 0; i < line.Count;)
                {
                    if (char.IsLetter(line[i].Character) || line[i].Character == '_')
                    {
                        var start = i;
                        var end = i;

                        while (end < line.Count && (char.IsLetterOrDigit(line[end].Character) || line[end].Character == '_'))
                        {
                            end++;
                        }

                        var id = buffer.Substring(start, end - start);
                        if (!languageDefinition.CaseSensitive)
                        {
                            id = id.ToLower();
                        }

                        var color = PaletteIndex.Identifier;
                        if (languageDefinition.Keywords.Contains(id))
                        {
                            color = PaletteIndex.Keyword;
                        }
                        else if (languageDefinition.Identifiers.ContainsKey(id))
                        {
                            color = PaletteIndex.KnownIdentifier;
                        }
                        else if (languageDefinition.PreprocIdentifiers.ContainsKey(id))
                        {
                            color = PaletteIndex.PreprocessorIdentifier;
                        }

                        for (var j = start; j < end && j < line.Count; j++)
                        {
                            var glyph = line[j];
                            glyph.ColorIndex = color;
                            line[j] = glyph;
                        }

                        i = end;
                    }
                    else
                    {
                        i++;
                    }
                }
            }
        }
    }


    public void ColorizeInternal()
    {
        if (Buffer.GetLines().Count == 0 || !Colorizer.Enabled)
        {
            return;
        }

        if (Colorizer.CheckComments)
        {
            var endLine = Buffer.GetLines().Count;
            var endIndex = 0;
            var commentStartLine = endLine;
            var commentStartIndex = endIndex;
            var withinString = false;
            var withinSingleLineComment = false;
            var withinPreproc = false;
            var firstChar = true; // there is no other non-whitespace characters in the line before
            var concatenate = false; // '\' on the very end of the line
            var currentLine = 0;
            var currentIndex = 0;
            while (currentLine < endLine || currentIndex < endIndex)
            {
                var line = Buffer.GetLines()[currentLine];

                if (currentIndex == 0 && !concatenate)
                {
                    withinSingleLineComment = false;
                    withinPreproc = false;
                    firstChar = true;
                }

                concatenate = false;

                if (line.Count != 0)
                {
                    var g = line[currentIndex];
                    var c = g.Character;

                    if (c != languageDefinition.PreprocChar && !char.IsWhiteSpace(c))
                    {
                        firstChar = false;
                    }

                    if (currentIndex == line.Count - 1 && line[line.Count - 1].Character == '\\')
                    {
                        concatenate = true;
                    }

                    var inComment = commentStartLine < currentLine ||
                                    commentStartLine == currentLine && commentStartIndex <= currentIndex;

                    if (withinString)
                    {
                        var mod = line[currentIndex];
                        mod.MultiLineComment = inComment;
                        line[currentIndex] = mod;

                        if (c == '\"')
                        {
                            if (currentIndex + 1 < line.Count && line[currentIndex + 1].Character == '\"')
                            {
                                currentIndex += 1;
                                if (currentIndex < line.Count)
                                {
                                    var nlc = line[currentIndex];
                                    nlc.MultiLineComment = inComment;
                                    line[currentIndex] = nlc;
                                }
                            }
                            else
                            {
                                withinString = false;
                            }
                        }
                        else if (c == '\\')
                        {
                            currentIndex += 1;
                            if (currentIndex < line.Count)
                            {
                                var nlc = line[currentIndex];
                                nlc.MultiLineComment = inComment;
                                line[currentIndex] = nlc;
                            }
                        }
                    }
                    else
                    {
                        if (firstChar && c == languageDefinition.PreprocChar)
                        {
                            withinPreproc = true;
                        }

                        if (c == '\"')
                        {
                            withinString = true;
                            var nlc = line[currentIndex];
                            nlc.MultiLineComment = inComment;
                            line[currentIndex] = nlc;
                        }
                        else
                        {
                            var pred = (char a, Glyph b) => a == b.Character;
                            //var pred = [](const char& a, const Glyph& b) { return a == b.mChar; };
                            var from = currentIndex;

                            var startStr = languageDefinition.CommentStart;
                            var singleStartStr = languageDefinition.SingleLineComment;

                            bool equals(string a, Line line, int count)
                            {
                                var eq = true;
                                for (var i = 0; i < a.Length && i < count; i++)
                                {
                                    eq &= pred(a[i], line[i]);
                                }

                                return eq;
                            }

                            if (singleStartStr.Length > 0 &&
                                currentIndex + singleStartStr.Length <= line.Count &&
                                equals(singleStartStr, line, singleStartStr.Length))
                            {
                                withinSingleLineComment = true;
                            }
                            else if (!withinSingleLineComment && currentIndex + startStr.Length <= line.Count &&
                                     equals(startStr, line, startStr.Length))
                            {
                                commentStartLine = currentLine;
                                commentStartIndex = currentIndex;
                            }

                            inComment = inComment = commentStartLine < currentLine ||
                                                    commentStartLine == currentLine &&
                                                    commentStartIndex <= currentIndex;

                            var lt = line[currentIndex];
                            lt.MultiLineComment = inComment;
                            lt.Comment = withinSingleLineComment;
                            line[currentIndex] = lt;

                            var endStr = languageDefinition.CommentEnd;
                            if (currentIndex + 1 >= endStr.Length &&
                                equals(endStr, line, endStr.Length))
                            {
                                commentStartIndex = endIndex;
                                commentStartLine = endLine;
                            }
                        }
                    }

                    var t = line[currentIndex];
                    t.Preprocessor = withinPreproc;
                    line[currentIndex] = t;

                    currentIndex += Utf8Helper.UTF8CharLength(c);
                    if (currentIndex >= line.Count)
                    {
                        currentIndex = 0;
                        ++currentLine;
                    }
                }
                else
                {
                    currentIndex = 0;
                    ++currentLine;
                }
            }

            Colorizer.CheckComments = false;
        }

        if (Colorizer.MinLine < Colorizer.MaxLine)
        {
            var increment = languageDefinition.TokenizeLine == null ? 10 : 10000;
            var to = Math.Min(Colorizer.MinLine + increment, Colorizer.MaxLine);
            ColorizeRange(Colorizer.MinLine, to);
            Colorizer.SetMinLine(to);

            if (Colorizer.MaxLine == Colorizer.MinLine)
            {
                Colorizer.Reset();
            }
        }
    }

    public float TextDistanceToLineStart(Coordinate aFrom)
    {
        var line = Buffer.GetLines()[aFrom.Line];
        var distance = 0.0f;
        //float spaceSize = ImGui::GetFont()->CalcTextSizeA(ImGui::GetFontSize(), FLT_MAX, -1.0f, " ", nullptr, nullptr).x;
        var spaceSize = ImGui.CalcTextSize(" ").X; // Not sure if that's correct
        var colIndex = Buffer.GetCharacterIndex(aFrom);
        for (var it = 0; it < line.Count && it < colIndex;)
        {
            if (line[it].Character == '\t')
            {
                distance = (float)(1.0f + Math.Floor((1.0f + distance) / (Style.TabSize * spaceSize))) *
                           (Style.TabSize * spaceSize);
                ++it;
            }
            else
            {
                var d = Utf8Helper.UTF8CharLength(line[it].Character);
                var l = d;
                var tempCString = new char[7];
                var i = 0;
                for (; i < 6 && d-- > 0 && it < line.Count; i++, it++)
                {
                    tempCString[i] = line[it].Character;
                }

                tempCString[i] = '\0';
                if (l > 0)
                {
                    distance += ImGui.CalcTextSize(new string(tempCString, 0, l)).X;
                }
            }
        }

        return distance;
    }

    public void Advance(Coordinate aCoordinates)
    {
        if (aCoordinates.Line < Buffer.GetLines().Count)
        {
            var line = Buffer.GetLines()[aCoordinates.Line];
            var cindex = Buffer.GetCharacterIndex(aCoordinates);

            if (cindex + 1 < line.Count)
            {
                var delta = Utf8Helper.UTF8CharLength(line[cindex].Character);
                cindex = Math.Min(cindex + delta, line.Count - 1);
            }
            else
            {
                ++aCoordinates.Line;
                cindex = 0;
            }

            aCoordinates.Column = Buffer.GetCharacterColumn(aCoordinates.Line, cindex);
        }
    }

    internal void DeleteRange(Coordinate aStart, Coordinate aEnd)
    {
        if (aStart.Line >= Buffer.GetLines().Count || aEnd.Line >= Buffer.GetLines().Count)
        {
            return;
        }

        if (aEnd == aStart)
        {
            return;
        }

        var start = Buffer.GetCharacterIndex(aStart);
        var end = Buffer.GetCharacterIndex(aEnd);

        if (aStart.Line == aEnd.Line)
        {
            var line = Buffer.GetLines()[aStart.Line];
            var n = Buffer.GetLineMaxColumn(aStart.Line);
            if (aEnd.Column >= n)
            {
                line = line.Take(start).ToList();
            }
            else
            {
                line = line.Take(start).Take(end - start).ToList();
            }

            Buffer.GetLines()[aStart.Line] = line;
        }
        else
        {
            var firstLine = Buffer.GetLines()[aStart.Line];
            var lastLine = Buffer.GetLines()[aEnd.Line];

            lastLine = lastLine.TakeLast(lastLine.Count - end).ToList();
            firstLine = firstLine.Take(start).ToList();

            firstLine.AddRange(lastLine);
            Buffer.GetLines()[aStart.Line] = firstLine;
            Buffer.GetLines()[aEnd.Line] = lastLine;

            if (aStart.Line < aEnd.Line)
            {
                firstLine.AddRange(lastLine);
            }

            if (aStart.Line < aEnd.Line)
            {
                RemoveLine(aStart.Line + 1, aEnd.Line + 1);
            }
        }

        Buffer.MarkDirty();
    }

    public int InsertTextAt(Coordinate where, string value)
    {
        return Buffer.InsertTextAt(where, value);
    }

    public void RemoveLine(int aStart, int aEnd)
    {
        for (var i = aStart; i < aEnd; i++)
        {
            RemoveLine(aStart);
        }
    }

    public void RemoveLine(int aIndex)
    {
        Buffer.GetLines().RemoveAt(aIndex);
        Buffer.MarkDirty();
    }

    public void InsertLine(int aIndex)
    {
        Buffer.GetLines().Insert(aIndex, new Line());
    }

    public void EnterCharacter(char aChar, bool aShift)
    {
        // assert(!mReadOnly);

        UndoRecord u = new();

        u.Before = State.Clone();

        if (Selection.HasSelection)
        {
            if (aChar == '\t' && State.SelectionStart.Line != State.SelectionEnd.Line)
            {
                var start = State.SelectionStart;
                var end = State.SelectionEnd;
                var originalEnd = end;

                if (start > end)
                {
                    var tmp = start;
                    start = end;
                    end = tmp;
                }

                start.Column = 0;
                //			end.mColumn = end.mLine < Buffer.GetLines().size() ? Buffer.GetLines()[end.mLine].size() : 0;
                if (end.Column == 0 && end.Line > 0)
                {
                    --end.Line;
                }

                if (end.Line >= Buffer.GetLines().Count)
                {
                    end.Line = Buffer.GetLines().Count == 0 ? 0 : Buffer.GetLines().Count - 1;
                }

                end.Column = Buffer.GetLineMaxColumn(end.Line);

                //if (end.mColumn >= Buffer.GetLineMaxColumn(end.mLine))
                //	end.mColumn = Buffer.GetLineMaxColumn(end.mLine) - 1;

                u.RemovedStart = start;
                u.RemovedEnd = end;
                u.Removed = Buffer.GetText(start, end);

                var modified = false;

                for (var i = start.Line; i <= end.Line; i++)
                {
                    var line = Buffer.GetLines()[i];
                    if (aShift)
                    {
                        if (line.Count != 0)
                        {
                            if (line[0].Character == '\t')
                            {
                                line.RemoveAt(0);
                                modified = true;
                            }
                            else
                            {
                                for (var j = 0; j < Style.TabSize && line.Count != 0 && line[0].Character == ' '; j++)
                                {
                                    line.RemoveAt(0);
                                    modified = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        line.Insert(0, new Glyph('\t', PaletteIndex.Background));
                        modified = true;
                    }
                }

                if (modified)
                {
                    start = new Coordinate(start.Line, Buffer.GetCharacterColumn(start.Line, 0));
                    Coordinate rangeEnd = new();
                    if (originalEnd.Column != 0)
                    {
                        end = new Coordinate(end.Line, Buffer.GetLineMaxColumn(end.Line));
                        rangeEnd = end;
                        u.Added = Buffer.GetText(start, end);
                    }
                    else
                    {
                        end = new Coordinate(originalEnd.Line, 0);
                        rangeEnd = new Coordinate(end.Line - 1, Buffer.GetLineMaxColumn(end.Line - 1));
                        u.Added = Buffer.GetText(start, rangeEnd);
                    }

                    u.AddedStart = start;
                    u.AddedEnd = rangeEnd;
                    u.After = State.Clone();

                    State.SelectionStart = start;
                    State.SelectionEnd = end;
                    UndoManager.AddUndo(u);

                    Buffer.MarkDirty();

                    Cursor.EnsureVisible();
                }

                return;
            } // c == '\t'

            u.Removed = Selection.Text;
            u.RemovedStart = State.SelectionStart;
            u.RemovedEnd = State.SelectionEnd;
            DeleteSelection();
        } // HasSelection

        var coord = Cursor.GetPosition();
        u.AddedStart = coord;

        //assert(!Buffer.GetLines().empty());

        if (aChar == '\n')
        {
            InsertLine(coord.Line + 1);
            var line = Buffer.GetLines()[coord.Line];
            var newLine = Buffer.GetLines()[coord.Line + 1];

            if (languageDefinition.AutoIndentation)
            {
                for (var it = 0;
                     it < line.Count && char.IsAscii(line[it].Character) && char.IsWhiteSpace(line[it].Character);
                     ++it)
                {
                    newLine.Add(line[it]);
                }
            }

            var whitespaceSize = newLine.Count;
            var cindex = Buffer.GetCharacterIndex(coord);
            newLine.AddRange(line.TakeLast(line.Count - cindex));
            line.RemoveRange(cindex, line.Count - cindex);
            Cursor.SetPosition(new Coordinate(coord.Line + 1, Buffer.GetCharacterColumn(coord.Line + 1, whitespaceSize)));
            u.Added = aChar.ToString();
        }
        else
        {
            var buf = new char[7];
            var e = Utf8Helper.ImTextCharToUtf8(ref buf, 7, aChar);
            if (e > 0)
            {
                buf[e] = '\0';
                var line = Buffer.GetLines()[coord.Line];
                var cindex = Buffer.GetCharacterIndex(coord);

                if (IsOverwrite && cindex < line.Count)
                {
                    var d = Utf8Helper.UTF8CharLength(line[cindex].Character);

                    u.RemovedStart = State.CursorPosition;
                    u.RemovedEnd = new Coordinate(coord.Line, Buffer.GetCharacterColumn(coord.Line, cindex + d));

                    while (d-- > 0 && cindex < line.Count)
                    {
                        u.Removed += line[cindex].Character;
                        line.RemoveAt(cindex);
                    }
                }

                var added = 0;
                foreach (var c in buf)
                {
                    if (c == '\0')
                    {
                        break;
                    }

                    line.Insert(cindex, new Glyph(c, PaletteIndex.Default));
                    added++;
                    cindex++;
                }

                u.Added = aChar.ToString();

                Cursor.SetPosition(new Coordinate(coord.Line, Buffer.GetCharacterColumn(coord.Line, cindex)));
            }
            else
            {
                return;
            }
        }

        Buffer.MarkDirty();

        u.AddedEnd = Cursor.GetPosition();
        u.After = State.Clone();

        UndoManager.AddUndo(u);

        Colorizer.Colorize(coord.Line - 1, 3);
        Cursor.EnsureVisible();
    }

    public void Backspace()
    {
        // assert(!mReadOnly);

        if (Buffer.GetLines().Count == 0)
        {
            return;
        }

        UndoRecord u = new();
        u.Before = State.Clone();

        if (Selection.HasSelection)
        {
            u.Removed = Selection.Text;
            u.RemovedStart = State.SelectionStart;
            u.RemovedEnd = State.SelectionEnd;

            DeleteSelection();
        }
        else
        {
            var pos = Cursor.GetPosition();
            Cursor.SetPosition(pos);

            if (State.CursorPosition.Column == 0)
            {
                if (State.CursorPosition.Line == 0)
                {
                    return;
                }

                u.Removed = "\n";
                u.RemovedStart = u.RemovedEnd = new Coordinate(pos.Line - 1, Buffer.GetLineMaxColumn(pos.Line - 1));
                Advance(u.RemovedEnd);

                var prevSize = Buffer.GetLineMaxColumn(State.CursorPosition.Line - 1);

                RemoveLine(State.CursorPosition.Line);
                --State.CursorPosition.Line;
                State.CursorPosition.Column = prevSize;
            }
            else
            {
                var line = Buffer.GetLines()[State.CursorPosition.Line];
                var cindex = Buffer.GetCharacterIndex(pos) - 1;
                var cend = cindex + 1;
                while (cindex > 0 && Utf8Helper.IsUTFSequence(line[cindex].Character))
                {
                    --cindex;
                }

                //if (cindex > 0 && Utf8Helper.UTF8CharLength(line[cindex].mChar) > 1)
                //	--cindex;

                u.RemovedStart = u.RemovedEnd = Cursor.GetPosition();
                --u.RemovedStart.Column;
                --State.CursorPosition.Column;

                while (cindex < line.Count && cend-- > cindex)
                {
                    u.Removed += line[cindex].Character;
                    line.RemoveAt(cindex);
                }
            }

            Buffer.MarkDirty();

            Cursor.EnsureVisible();
            Colorizer.Colorize(State.CursorPosition.Line, 1);
        }

        u.After = State.Clone();
        UndoManager.AddUndo(u);
    }

    public void DeleteSelection()
    {
        if (State.SelectionEnd == State.SelectionStart)
        {
            return;
        }

        DeleteRange(State.SelectionStart, State.SelectionEnd);

        State.SetSelection(State.SelectionStart, State.SelectionStart);
        Cursor.SetPosition(State.SelectionStart);
        Colorizer.Colorize(State.SelectionStart.Line, 1);
    }

    public string GetWordAt(Coordinate aCoords)
    {
        var start = Buffer.FindWordStart(aCoords);
        var end = Buffer.FindWordEnd(aCoords);

        StringBuilder r = new();

        var istart = Buffer.GetCharacterIndex(start);
        var iend = Buffer.GetCharacterIndex(end);

        for (var it = istart; it < iend; ++it)
        {
            r.Append(Buffer.GetLines()[aCoords.Line][it].Character);
        }

        return r.ToString();
    }

    public uint GetGlyphColor(Glyph aGlyph)
    {
        if (!Colorizer.Enabled)
        {
            return mPalette[(int)PaletteIndex.Default];
        }

        if (aGlyph.Comment)
        {
            return mPalette[(int)PaletteIndex.Comment];
        }

        if (aGlyph.MultiLineComment)
        {
            return mPalette[(int)PaletteIndex.MultiLineComment];
        }

        var color = mPalette[(int)aGlyph.ColorIndex];
        if (aGlyph.Preprocessor)
        {
            var ppcolor = mPalette[(int)PaletteIndex.Preprocessor];
            var c0 = (int)((ppcolor & 0xff) + (color & 0xff)) / 2;
            var c1 = (int)((ppcolor >> 8 & 0xff) + (color >> 8 & 0xff)) / 2;
            var c2 = (int)((ppcolor >> 16 & 0xff) + (color >> 16 & 0xff)) / 2;
            var c3 = (int)((ppcolor >> 24 & 0xff) + (color >> 24 & 0xff)) / 2;
            return (uint)(c0 | c1 << 8 | c2 << 16 | c3 << 24);
        }

        return color;
    }

    public void HandleKeyboardInputs()
    {
        var io = ImGui.GetIO();
        var shift = io.KeyShift;
        var ctrl = io.ConfigMacOSXBehaviors ? io.KeySuper : io.KeyCtrl;
        var alt = io.ConfigMacOSXBehaviors ? io.KeyCtrl : io.KeyAlt;

        if (ImGui.IsWindowFocused())
        {
            if (ImGui.IsWindowHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.TextInput);
            }
            //ImGui::CaptureKeyboardFromApp(true);

            io.WantCaptureKeyboard = true;
            io.WantTextInput = true;

            if (!IsReadOnly && ctrl && !shift && !alt && ImGui.IsKeyPressed(ImGuiKey.Z))
            {
                UndoManager.Undo();
            }
            else if (!IsReadOnly && !ctrl && !shift && alt && ImGui.IsKeyPressed(ImGuiKey.Backspace))
            {
                UndoManager.Undo();
            }
            else if (!IsReadOnly && ctrl && !shift && !alt && ImGui.IsKeyPressed(ImGuiKey.Y))
            {
                UndoManager.Redo();
            }
            else if (!ctrl && !alt && ImGui.IsKeyPressed(ImGuiKey.UpArrow))
            {
                MoveUp(1, shift);
            }
            else if (!ctrl && !alt && ImGui.IsKeyPressed(ImGuiKey.DownArrow))
            {
                MoveDown(1, shift);
            }
            else if (!alt && ImGui.IsKeyPressed(ImGuiKey.LeftArrow))
            {
                MoveLeft(1, shift, ctrl);
            }
            else if (!alt && ImGui.IsKeyPressed(ImGuiKey.RightArrow))
            {
                MoveRight(1, shift, ctrl);
            }
            else if (!alt && ImGui.IsKeyPressed(ImGuiKey.PageUp))
            {
                MoveUp(Renderer.GetPageSize() - 4, shift);
            }
            else if (!alt && ImGui.IsKeyPressed(ImGuiKey.PageDown))
            {
                MoveDown(Renderer.GetPageSize() - 4, shift);
            }
            else if (!alt && ctrl && ImGui.IsKeyPressed(ImGuiKey.Home))
            {
                MoveTop(shift);
            }
            else if (ctrl && !alt && ImGui.IsKeyPressed(ImGuiKey.End))
            {
                MoveBottom(shift);
            }
            else if (!ctrl && !alt && ImGui.IsKeyPressed(ImGuiKey.Home))
            {
                MoveHome(shift);
            }
            else if (!ctrl && !alt && ImGui.IsKeyPressed(ImGuiKey.End))
            {
                MoveEnd(shift);
            }
            else if (!IsReadOnly && !ctrl && !shift && !alt && ImGui.IsKeyPressed(ImGuiKey.Delete))
            {
                Delete();
            }
            else if (!IsReadOnly && !ctrl && !shift && !alt && ImGui.IsKeyPressed(ImGuiKey.Backspace))
            {
                Backspace();
            }
            else if (!ctrl && !shift && !alt && ImGui.IsKeyPressed(ImGuiKey.Insert))
            {
                IsOverwrite ^= true;
            }
            else if (ctrl && !shift && !alt && ImGui.IsKeyPressed(ImGuiKey.Insert))
            {
                Clipboard.Copy();
            }
            else if (ctrl && !shift && !alt && ImGui.IsKeyPressed(ImGuiKey.C))
            {
                Clipboard.Copy();
            }
            else if (!IsReadOnly && !ctrl && shift && !alt && ImGui.IsKeyPressed(ImGuiKey.Insert))
            {
                Clipboard.Paste();
            }
            else if (!IsReadOnly && ctrl && !shift && !alt && ImGui.IsKeyPressed(ImGuiKey.V))
            {
                Clipboard.Paste();
            }
            else if (ctrl && !shift && !alt && ImGui.IsKeyPressed(ImGuiKey.X))
            {
                Clipboard.Cut();
            }
            else if (!ctrl && shift && !alt && ImGui.IsKeyPressed(ImGuiKey.Delete))
            {
                Clipboard.Cut();
            }
            else if (ctrl && !shift && !alt && ImGui.IsKeyPressed(ImGuiKey.A))
            {
                SelectAll();
            }
            else if (!IsReadOnly && !ctrl && !shift && !alt && ImGui.IsKeyPressed(ImGuiKey.Enter))
            {
                EnterCharacter('\n', false);
            }
            else if (!IsReadOnly && !ctrl && !alt && ImGui.IsKeyPressed(ImGuiKey.Tab))
            {
                EnterCharacter('\t', shift);
            }

            if (!IsReadOnly && io.InputQueueCharacters.Size != 0)
            {
                for (var i = 0; i < io.InputQueueCharacters.Size; i++)
                {
                    var c = io.InputQueueCharacters[i];
                    if (c != 0 && (c == '\n' || c >= 32))
                    {
                        EnterCharacter((char)c, shift);
                    }
                }
            }
            // This line seems to be untranslatable to C#
            //io.InputQueueCharacters.resize(0);
        }
    }

    public void Render()
    {
        /* Compute mCharAdvance regarding to scaled font size (Ctrl + mouse wheel)*/
        //float fontSize = ImGui::GetFont()->CalcTextSizeA(ImGui::GetFontSize(), FLT_MAX, -1.0f, "#", nullptr, nullptr).x;
        var fontSize = ImGui.CalcTextSize("#").X; // Again, not sure if equal
        // mCharAdvance = new Vector2(fontSize, ImGui.GetTextLineHeightWithSpacing() * Style.LineSpacing);

        /* Update palette with the current alpha from style */
        for (var i = 0; i < (int)PaletteIndex.Max; ++i)
        {
            var color = ImGui.ColorConvertU32ToFloat4(mPaletteBase[i]);
            color.W *= ImGui.GetStyle().Alpha;
            mPalette[i] = ImGui.ColorConvertFloat4ToU32(color);
        }

        var contentSize = ImGui.GetWindowContentRegionMax();
        var drawList = ImGui.GetWindowDrawList();
        var longest = Renderer.GutterWidth;

        Scroll.ScrollToTop();

        var cursorScreenPos = ImGui.GetCursorScreenPos();
        var scrollX = ImGui.GetScrollX();
        var scrollY = ImGui.GetScrollY();

        var lineNo = (int)Math.Floor(scrollY / Renderer.LineHeight);
        var globalLineMax = Buffer.GetLines().Count;
        var lineMax = Math.Max(0,
            Math.Min(Buffer.GetLines().Count - 1, lineNo + (int)Math.Floor((scrollY + contentSize.Y) / Renderer.LineHeight)));

        var buf = Style.ShowLineNumbers ? " " + globalLineMax + " " : "";
        Renderer.SetGutterWidth(ImGui.CalcTextSize(buf).X);

        if (Buffer.GetLines().Count != 0)
        {
            //float spaceSize = ImGui::GetFont()->CalcTextSizeA(ImGui::GetFontSize(), FLT_MAX, -1.0f, " ", nullptr, nullptr).x;
            var spaceSize = ImGui.CalcTextSize(" ").X;
            while (lineNo <= lineMax)
            {
                var lineStartScreenPos = new Vector2(cursorScreenPos.X, cursorScreenPos.Y + lineNo * Renderer.LineHeight);
                var textScreenPos = new Vector2(lineStartScreenPos.X + Renderer.GutterWidth, lineStartScreenPos.Y);

                var line = Buffer.GetLines()[lineNo];
                longest = Math.Max(
                    Renderer.GutterWidth + TextDistanceToLineStart(new Coordinate(lineNo, Buffer.GetLineMaxColumn(lineNo))), longest);
                var columnNo = 0;
                Coordinate lineStartCoord = new(lineNo, 0);
                Coordinate lineEndCoord = new(lineNo, Buffer.GetLineMaxColumn(lineNo));

                // Draw selection for the current line
                var sstart = -1.0f;
                var ssend = -1.0f;

                // assert(mState.mSelectionStart <= mState.mSelectionEnd);
                if (State.SelectionStart <= lineEndCoord)
                {
                    sstart = State.SelectionStart > lineStartCoord
                        ? TextDistanceToLineStart(State.SelectionStart)
                        : 0.0f;
                }

                if (State.SelectionEnd > lineStartCoord)
                {
                    ssend = TextDistanceToLineStart(State.SelectionEnd < lineEndCoord
                        ? State.SelectionEnd
                        : lineEndCoord);
                }

                if (State.SelectionEnd.Line > lineNo)
                {
                    ssend += Renderer.CharacterWidth;
                }

                if (sstart != -1 && ssend != -1 && sstart < ssend)
                {
                    Vector2 vstart = new(lineStartScreenPos.X + Renderer.GutterWidth + sstart, lineStartScreenPos.Y);
                    Vector2 vend = new(lineStartScreenPos.X + Renderer.GutterWidth + ssend,
                        lineStartScreenPos.Y + Renderer.LineHeight);
                    drawList.AddRectFilled(vstart, vend, mPalette[(int)PaletteIndex.Selection]);
                }

                var start = new Vector2(lineStartScreenPos.X + scrollX, lineStartScreenPos.Y);

                // Draw line number (right aligned)
                if (Style.ShowLineNumbers)
                {
                    buf = lineNo + 1 + "  ";

                    var lineNoWidth = ImGui.CalcTextSize(buf).X;
                    drawList.AddText(new Vector2(lineStartScreenPos.X + Renderer.GutterWidth - lineNoWidth, lineStartScreenPos.Y),
                        mPalette[(int)PaletteIndex.LineNumber], buf);
                }

                if (State.CursorPosition.Line == lineNo)
                {
                    var focused = ImGui.IsWindowFocused();

                    // Highlight the current line (where the cursor is)
                    if (!Selection.HasSelection)
                    {
                        var end = new Vector2(start.X + contentSize.X + scrollX, start.Y + Renderer.LineHeight);
                        drawList.AddRectFilled(start, end,
                            mPalette[
                                (int)(focused ? PaletteIndex.CurrentLineFill : PaletteIndex.CurrentLineFillInactive)]);
                        drawList.AddRect(start, end, mPalette[(int)PaletteIndex.CurrentLineEdge], 1.0f);
                    }

                    // Render the cursor
                    if (focused)
                    {
                        var timeEnd = DateTime.Now.Ticks;
                        var elapsed = timeEnd - mStartTime;
                        if (elapsed > 400)
                        {
                            var width = 1.0f;
                            var cindex = Buffer.GetCharacterIndex(State.CursorPosition);
                            var cx = TextDistanceToLineStart(State.CursorPosition);

                            if (IsOverwrite && cindex < line.Count)
                            {
                                var c = line[cindex].Character;
                                if (c == '\t')
                                {
                                    var x = (1.0f + Math.Floor((1.0f + cx) / (Style.TabSize * spaceSize))) *
                                            (Style.TabSize * spaceSize);
                                    width = (float)(x - cx);
                                }
                                else
                                {
                                    var buf2 = new char[2];
                                    buf2[0] = line[cindex].Character;
                                    buf2[1] = '\0';
                                    //width = ImGui.GetFont()->CalcTextSizeA(ImGui.GetFontSize(), FLT_MAX, -1.0f, buf2).x;
                                    width = ImGui.CalcTextSize(new string(buf2)).X;
                                }
                            }

                            Vector2 cstart = new(textScreenPos.X + cx, lineStartScreenPos.Y);
                            Vector2 cend = new(textScreenPos.X + cx + width, lineStartScreenPos.Y + Renderer.LineHeight);
                            drawList.AddRectFilled(cstart, cend, mPalette[(int)PaletteIndex.Cursor]);
                            if (elapsed > 800)
                            {
                                mStartTime = timeEnd;
                            }
                        }
                    }
                }

                // Render colorized text
                var prevColor = line.Count == 0 ? mPalette[(int)PaletteIndex.Default] : GetGlyphColor(line[0]);
                Vector2 bufferOffset = new();

                for (var i = 0; i < line.Count;)
                {
                    var glyph = line[i];
                    var color = GetGlyphColor(glyph);

                    if ((color != prevColor || glyph.Character == '\t' || glyph.Character == ' ') && Renderer.LineRenderer.TextRun.Length != 0)
                    {
                        Vector2 newOffset = new(textScreenPos.X + bufferOffset.X, textScreenPos.Y + bufferOffset.Y);
                        drawList.AddText(newOffset, prevColor, Renderer.LineRenderer.TextRun);

                        var textSize = ImGui.CalcTextSize(Renderer.LineRenderer.TextRun).X;
                        bufferOffset.X += textSize;
                        Renderer.LineRenderer.TextRun = "";
                    }

                    prevColor = color;

                    if (glyph.Character == '\t')
                    {
                        var oldX = bufferOffset.X;
                        bufferOffset.X = (float)(1.0f + Math.Floor((1.0f + bufferOffset.X) / (Style.TabSize * spaceSize))) *
                                         (Style.TabSize * spaceSize);
                        ++i;

                        if (Style.ShowWhitespace)
                        {
                            var s = ImGui.GetFontSize();
                            var x1 = textScreenPos.X + oldX + 1.0f;
                            var x2 = textScreenPos.X + bufferOffset.X - 1.0f;
                            var y = textScreenPos.Y + bufferOffset.Y + s * 0.5f;
                            Vector2 p1 = new(x1, y);
                            Vector2 p2 = new(x2, y);
                            Vector2 p3 = new(x2 - s * 0.2f, y - s * 0.2f);
                            Vector2 p4 = new(x2 - s * 0.2f, y + s * 0.2f);
                            drawList.AddLine(p1, p2, 0x90909090);
                            drawList.AddLine(p2, p3, 0x90909090);
                            drawList.AddLine(p2, p4, 0x90909090);
                        }
                    }
                    else if (glyph.Character == ' ')
                    {
                        if (Style.ShowWhitespace)
                        {
                            var s = ImGui.GetFontSize();
                            var x = textScreenPos.X + bufferOffset.X + spaceSize * 0.5f;
                            var y = textScreenPos.Y + bufferOffset.Y + s * 0.5f;
                            drawList.AddCircleFilled(new Vector2(x, y), 1.5f, 0x80808080, 4);
                        }

                        bufferOffset.X += spaceSize;
                        i++;
                    }
                    else
                    {
                        var l = Utf8Helper.UTF8CharLength(glyph.Character);
                        while (l-- > 0)
                        {
                            Renderer.LineRenderer.TextRun += line[i++].Character;
                        }
                    }

                    ++columnNo;
                }

                if (Renderer.LineRenderer.TextRun.Count() != 0)
                {
                    Vector2 newOffset = new(textScreenPos.X + bufferOffset.X, textScreenPos.Y + bufferOffset.Y);
                    drawList.AddText(newOffset, prevColor, Renderer.LineRenderer.TextRun);
                    Renderer.LineRenderer.TextRun = "";
                }

                ++lineNo;
            }
        }


        ImGui.Dummy(new Vector2(longest + 2, Buffer.GetLines().Count * Renderer.LineHeight));
    }
}
