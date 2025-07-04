using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using ImGuiNET;

namespace DalamudCodeEditor;

using UndoBuffer = List<UndoRecord>;
using Line = List<Glyph>;
using Lines = List<List<Glyph>>;
using RegexList = List<Tuple<Regex, PaletteIndex>>;
using Palette = uint[];

public class TextEditor
{
    private Vector2 mCharAdvance;

    private bool mCheckComments = true;
    private bool mColorizerEnabled = true;
    private int mColorRangeMin, mColorRangeMax;

    private bool mCursorPositionChanged;
    private bool mHandleKeyboardInputs = true;
    private bool mHandleMouseInputs = true;
    private bool mHasSelectionAnchor = false;
    private bool mIgnoreImGuiChild;
    private Coordinates mInteractiveStart = new(), mInteractiveEnd = new();
    private LanguageDefinition mLanguageDefinition = new();

    private float mLastClick = -1.0f;

    // This is directly from original code
#pragma warning disable CS0414
    private int mLeftMargin = 10;
#pragma warning restore CS0414
    private string mLineBuffer = "";
    private Lines mLines = new();

    private readonly float mLineSpacing = 1.0f;
    private readonly Palette mPalette = [];

    private Palette mPaletteBase = [];
    private readonly RegexList mRegexList = new();
    private bool mScrollToCursor;
    private bool mScrollToTop;
    private SelectionMode mSelectionMode = SelectionMode.Normal;

    private bool mShowLineNumbers = true;
    private bool mShowWhitespaces = true;
    private long mStartTime;
    internal EditorState mState = new();
    private bool mSuppressUndo;

    private int mTabSize = 4;
    private bool mTextChanged;

    private float
        mTextStart = 20.0f; // position (in pixels) where a code line starts relative to the left of the TextEditor.

    private readonly UndoBuffer mUndoBuffer = new();
    private int mUndoIndex;
    private bool mWithinRender;

    public TextEditor(LanguageDefinition def)
    {
        mPalette = new uint[(int)PaletteIndex.Max];
        Palette = PaletteBuilder.GetDarkPalette();
        Language = def;
        mLines.Add(new Line());
    }

    public LanguageDefinition Language
    {
        get => mLanguageDefinition;
        set
        {
            mLanguageDefinition = value;
            mRegexList.Clear();

            foreach (var r in mLanguageDefinition.RegexTokens)
                mRegexList.Add(new Tuple<Regex, PaletteIndex>(r.regex, r.color));

            Colorize();
        }
    }

    public Palette Palette
    {
        get => mPaletteBase;
        set
        {
            mPaletteBase = value;
            /* Update palette with the current alpha from style */
            for (var i = 0; i < (int)PaletteIndex.Max; ++i)
            {
                var color = ImGui.ColorConvertU32ToFloat4(mPaletteBase[i]);
                color.W *= ImGui.GetStyle().Alpha;
                mPalette[i] = ImGui.ColorConvertFloat4ToU32(color);
            }
        }
    }

    public bool IsOverwrite { get; private set; }

    public bool IsReadOnly { get; private set; }

    public bool HasSelection => mState.mSelectionEnd > mState.mSelectionStart;

    public void SetShowLineNumbers(bool value)
    {
        mShowLineNumbers = value;
    }

    public bool IsShowingLineNumbers()
    {
        return mShowLineNumbers;
    }

    public void Render(string aTitle, Vector2 aSize = new(), bool aBorder = false)
    {
        mWithinRender = true;
        mTextChanged = false;
        mCursorPositionChanged = false;

        var id = mPalette[(int)PaletteIndex.Background];

        ImGui.PushStyleColor(ImGuiCol.ChildBg, ImGui.ColorConvertU32ToFloat4(id));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0.0f, 0.0f));
        if (!mIgnoreImGuiChild)
            ImGui.BeginChild(aTitle, aSize, aBorder,
                ImGuiWindowFlags.HorizontalScrollbar | ImGuiWindowFlags.AlwaysHorizontalScrollbar |
                ImGuiWindowFlags.NoMove);

        if (mHandleKeyboardInputs)
            HandleKeyboardInputs();

        if (mHandleMouseInputs)
            HandleMouseInputs();

        ColorizeInternal();

        Render();

        if (!mIgnoreImGuiChild)
            ImGui.EndChild();

        ImGui.PopStyleVar();
        ImGui.PopStyleColor();

        mWithinRender = false;
    }

    public void SetText(string aText)
    {
        mLines.Clear();
        mLines.Add(new Line());
        foreach (var chr in aText)
            if (chr == '\r')
            {
                // ignore the carriage return character
            }
            else if (chr == '\n')
            {
                mLines.Add(new Line());
            }
            else
            {
                mLines.Last().Add(new Glyph(chr, PaletteIndex.Default));
            }

        mTextChanged = true;
        mScrollToTop = true;

        mUndoBuffer.Clear();
        mUndoIndex = 0;

        ColorizeInternal();
    }

    public string GetText()
    {
        return GetText(new Coordinates(), new Coordinates(mLines.Count, 0));
    }

    public void SetTextLines(List<string> aLines)
    {
        mLines.Clear();

        if (aLines.Count == 0)
        {
            mLines.Add(new Line());
        }
        else
        {
            mLines = new Lines(aLines.Count);

            for (var i = 0; i < aLines.Count; ++i)
            {
                var aLine = aLines[i];

                mLines[i] = new Line(aLine.Count());
                for (var j = 0; j < aLine.Count(); ++j)
                    mLines[i].Add(new Glyph(aLine[j], PaletteIndex.Default));
            }
        }

        mTextChanged = true;
        mScrollToTop = true;

        mUndoBuffer.Clear();
        mUndoIndex = 0;

        Colorize();
    }

    public List<string> GetTextLines()
    {
        List<string> result = new(mLines.Count);

        foreach (var line in mLines)
        {
            StringBuilder text = new(line.Count);

            for (var i = 0; i < line.Count; ++i)
                text[i] = line[i].mChar;

            result.Add(text.ToString());
        }

        return result;
    }

    public string GetSelectedText()
    {
        return GetText(mState.mSelectionStart, mState.mSelectionEnd);
    }

    public string GetCurrentLineText()
    {
        var lineLength = GetLineMaxColumn(mState.mCursorPosition.mLine);
        return GetText(
            new Coordinates(mState.mCursorPosition.mLine, 0),
            new Coordinates(mState.mCursorPosition.mLine, lineLength));
    }

    public int TotalLines()
    {
        return mLines.Count;
    }

    public void SetReadOnly(bool aValue)
    {
        IsReadOnly = aValue;
    }

    public bool IsTextChanged()
    {
        return mTextChanged;
    }

    public bool IsCursorPositionChanged()
    {
        return mCursorPositionChanged;
    }

    public bool IsColorizerEnabled()
    {
        return mColorizerEnabled;
    }

    public void SetColorizerEnable(bool aValue)
    {
        mColorizerEnabled = aValue;
    }

    public Coordinates GetCursorPosition()
    {
        return GetActualCursorCoordinates();
    }

    public void SetCursorPosition(Coordinates aPosition)
    {
        if (mState.mCursorPosition != aPosition)
        {
            mState.mCursorPosition = aPosition;
            mCursorPositionChanged = true;
            mHasSelectionAnchor = false;
            EnsureCursorVisible();
        }
    }

    public void SetHandleMouseInputs(bool aValue)
    {
        mHandleMouseInputs = aValue;
    }

    public bool IsHandleMouseInputsEnabled()
    {
        return mHandleKeyboardInputs;
    }

    public void SetHandleKeyboardInputs(bool aValue)
    {
        mHandleKeyboardInputs = aValue;
    }

    public bool IsHandleKeyboardInputsEnabled()
    {
        return mHandleKeyboardInputs;
    }

    public void SetImGuiChildIgnored(bool aValue)
    {
        mIgnoreImGuiChild = aValue;
    }

    public bool IsImGuiChildIgnored()
    {
        return mIgnoreImGuiChild;
    }

    public void SetShowWhitespaces(bool aValue)
    {
        mShowWhitespaces = aValue;
    }

    public bool IsShowingWhitespaces()
    {
        return mShowWhitespaces;
    }

    public void SetTabSize(int aValue)
    {
        mTabSize = Math.Max(0, Math.Min(32, aValue));
    }

    public int GetTabSize()
    {
        return mTabSize;
    }

    public void InsertText(string aValue)
    {
        if (aValue == null)
            return;

        var pos = GetActualCursorCoordinates();
        var start = pos < mState.mSelectionStart ? pos : mState.mSelectionStart;
        var totalLines = pos.mLine - start.mLine;

        totalLines += InsertTextAt(pos, aValue);

        SetSelection(pos, pos);
        SetCursorPosition(pos);
        Colorize(start.mLine - 1, totalLines + 2);
    }
    //public void InsertText(string aValue);

    public void MoveUp(int aAmount = 1, bool aSelect = false)
    {
        var oldPos = mState.mCursorPosition;

        mState.mCursorPosition.mLine = Math.Max(0, mState.mCursorPosition.mLine - aAmount);

        if (oldPos != mState.mCursorPosition)
        {
            if (aSelect)
            {
                if (oldPos == mInteractiveStart)
                    mInteractiveStart = mState.mCursorPosition;
                else if (oldPos == mInteractiveEnd)
                    mInteractiveEnd = mState.mCursorPosition;
                else
                {
                    mInteractiveStart = mState.mCursorPosition;
                    mInteractiveEnd = oldPos;
                }
            }
            else
            {
                mInteractiveStart = mInteractiveEnd = mState.mCursorPosition;
            }

            SetSelection(mInteractiveStart, mInteractiveEnd);
            EnsureCursorVisible();
        }
    }


    public void MoveDown(int aAmount = 1, bool aSelect = false)
    {
        var oldPos = mState.mCursorPosition;
        mState.mCursorPosition.mLine = Math.Max(0, Math.Min(mLines.Count - 1, mState.mCursorPosition.mLine + aAmount));

        if (aSelect)
        {
            if (oldPos == mInteractiveEnd)
            {
                mInteractiveEnd = mState.mCursorPosition;
            }
            else if (oldPos == mInteractiveStart)
            {
                mInteractiveStart = mState.mCursorPosition;
            }
            else
            {
                mInteractiveStart = oldPos;
                mInteractiveEnd = mState.mCursorPosition;
            }
        }
        else
        {
            mInteractiveStart = mInteractiveEnd = mState.mCursorPosition;
        }

        SetSelection(mInteractiveStart, mInteractiveEnd);
        EnsureCursorVisible();
    }


    public void MoveLeft(int aAmount = 1, bool aSelect = false, bool aWordMode = false)
    {
        if (mLines.Count == 0)
            return;

        var oldPos = mState.mCursorPosition;
        mState.mCursorPosition = GetActualCursorCoordinates();
        var line = mState.mCursorPosition.mLine;
        var cindex = GetCharacterIndex(mState.mCursorPosition);

        while (aAmount-- > 0)
        {
            if (cindex == 0)
            {
                if (line > 0)
                {
                    --line;
                    if (mLines.Count > line)
                        cindex = mLines[line].Count;
                    else
                        cindex = 0;
                }
            }
            else
            {
                --cindex;
                if (cindex > 0)
                    if (mLines.Count > line)
                        while (cindex > 0 && IsUTFSequence(mLines[line][cindex].mChar))
                            --cindex;
            }

            mState.mCursorPosition = new Coordinates(line, GetCharacterColumn(line, cindex));
            if (aWordMode)
            {
                mState.mCursorPosition = FindWordStart(mState.mCursorPosition);
                cindex = GetCharacterIndex(mState.mCursorPosition);
            }
        }

        mState.mCursorPosition = new Coordinates(line, GetCharacterColumn(line, cindex));

        // assert(mState.mCursorPosition.mColumn >= 0);
        if (aSelect)
        {
            if (oldPos == mInteractiveStart)
            {
                mInteractiveStart = mState.mCursorPosition;
            }
            else if (oldPos == mInteractiveEnd)
            {
                mInteractiveEnd = mState.mCursorPosition;
            }
            else
            {
                mInteractiveStart = mState.mCursorPosition;
                mInteractiveEnd = oldPos;
            }
        }
        else
        {
            mInteractiveStart = mInteractiveEnd = mState.mCursorPosition;
        }

        SetSelection(mInteractiveStart, mInteractiveEnd,
            aSelect && aWordMode ? SelectionMode.Word : SelectionMode.Normal);

        EnsureCursorVisible();
    }

    public void MoveRight(int aAmount = 1, bool aSelect = false, bool aWordMode = false)
    {
        var oldPos = mState.mCursorPosition;

        if (mLines.Count == 0 || oldPos.mLine >= mLines.Count)
            return;

        var cindex = GetCharacterIndex(mState.mCursorPosition);
        while (aAmount-- > 0)
        {
            var lindex = mState.mCursorPosition.mLine;
            var line = mLines[lindex];

            if (cindex >= line.Count)
            {
                if (mState.mCursorPosition.mLine < mLines.Count - 1)
                {
                    mState.mCursorPosition.mLine =
                        Math.Max(0, Math.Min(mLines.Count - 1, mState.mCursorPosition.mLine + 1));
                    mState.mCursorPosition.mColumn = 0;
                }
                else
                {
                    return;
                }
            }
            else
            {
                cindex += UTF8CharLength(line[cindex].mChar);
                mState.mCursorPosition = new Coordinates(lindex, GetCharacterColumn(lindex, cindex));
                if (aWordMode)
                    mState.mCursorPosition = FindNextWord(mState.mCursorPosition);
            }
        }

        if (aSelect)
        {
            if (oldPos == mInteractiveEnd)
            {
                mInteractiveEnd = SanitizeCoordinates(mState.mCursorPosition);
            }
            else if (oldPos == mInteractiveStart)
            {
                mInteractiveStart = mState.mCursorPosition;
            }
            else
            {
                mInteractiveStart = oldPos;
                mInteractiveEnd = mState.mCursorPosition;
            }
        }
        else
        {
            mInteractiveStart = mInteractiveEnd = mState.mCursorPosition;
        }

        SetSelection(mInteractiveStart, mInteractiveEnd,
            aSelect && aWordMode ? SelectionMode.Word : SelectionMode.Normal);

        EnsureCursorVisible();
    }

    public void MoveTop(bool aSelect = false)
    {
        var oldPos = mState.mCursorPosition;
        SetCursorPosition(new Coordinates(0, 0));

        if (mState.mCursorPosition != oldPos)
        {
            if (aSelect)
            {
                mInteractiveEnd = oldPos;
                mInteractiveStart = mState.mCursorPosition;
            }
            else
            {
                mInteractiveStart = mInteractiveEnd = mState.mCursorPosition;
            }

            SetSelection(mInteractiveStart, mInteractiveEnd);
        }
    }

    public void MoveBottom(bool aSelect = false)
    {
        var oldPos = GetCursorPosition();
        var newPos = new Coordinates(mLines.Count - 1, 0);
        SetCursorPosition(newPos);
        if (aSelect)
        {
            mInteractiveStart = oldPos;
            mInteractiveEnd = newPos;
        }
        else
        {
            mInteractiveStart = mInteractiveEnd = newPos;
        }

        SetSelection(mInteractiveStart, mInteractiveEnd);
    }

    public void MoveHome(bool aSelect = false)
    {
        var oldPos = mState.mCursorPosition;
        SetCursorPosition(new Coordinates(mState.mCursorPosition.mLine, 0));

        if (mState.mCursorPosition != oldPos)
        {
            if (aSelect)
            {
                if (oldPos == mInteractiveStart)
                {
                    mInteractiveStart = mState.mCursorPosition;
                }
                else if (oldPos == mInteractiveEnd)
                {
                    mInteractiveEnd = mState.mCursorPosition;
                }
                else
                {
                    mInteractiveStart = mState.mCursorPosition;
                    mInteractiveEnd = oldPos;
                }
            }
            else
            {
                mInteractiveStart = mInteractiveEnd = mState.mCursorPosition;
            }

            SetSelection(mInteractiveStart, mInteractiveEnd);
        }
    }

    public void MoveEnd(bool aSelect = false)
    {
        var oldPos = mState.mCursorPosition;
        SetCursorPosition(new Coordinates(mState.mCursorPosition.mLine, GetLineMaxColumn(oldPos.mLine)));

        if (mState.mCursorPosition != oldPos)
        {
            if (aSelect)
            {
                if (oldPos == mInteractiveEnd)
                {
                    mInteractiveEnd = mState.mCursorPosition;
                }
                else if (oldPos == mInteractiveStart)
                {
                    mInteractiveStart = mState.mCursorPosition;
                }
                else
                {
                    mInteractiveStart = oldPos;
                    mInteractiveEnd = mState.mCursorPosition;
                }
            }
            else
            {
                mInteractiveStart = mInteractiveEnd = mState.mCursorPosition;
            }

            SetSelection(mInteractiveStart, mInteractiveEnd);
        }
    }

    public void SetSelectionStart(Coordinates aPosition)
    {
        mState.mSelectionStart = SanitizeCoordinates(aPosition);
        mState.SwapEndsIfNeeded();
    }

    public void SetSelectionEnd(Coordinates aPosition)
    {
        mState.mSelectionEnd = SanitizeCoordinates(aPosition);
        mState.SwapEndsIfNeeded();
    }

    public void SetSelection(Coordinates aStart, Coordinates aEnd, SelectionMode aMode = SelectionMode.Normal)
    {
        var oldSelStart = mState.mSelectionStart;
        var oldSelEnd = mState.mSelectionEnd;

        mState.mSelectionStart = SanitizeCoordinates(aStart);
        mState.mSelectionEnd = SanitizeCoordinates(aEnd);
        mState.SwapEndsIfNeeded();

        switch (aMode)
        {
            case SelectionMode.Normal:
                break;
            case SelectionMode.Word:
            {
                mState.mSelectionStart = FindWordStart(mState.mSelectionStart);
                if (!IsOnWordBoundary(mState.mSelectionEnd))
                    mState.mSelectionEnd = FindWordEnd(FindWordStart(mState.mSelectionEnd));
                break;
            }
            case SelectionMode.Line:
            {
                var lineNo = mState.mSelectionEnd.mLine;
                var lineSize = lineNo < mLines.Count ? mLines[lineNo].Count : 0;
                mState.mSelectionStart = new Coordinates(mState.mSelectionStart.mLine, 0);
                mState.mSelectionEnd = new Coordinates(lineNo, GetLineMaxColumn(lineNo));
                break;
            }
        }

        if (mState.mSelectionStart != oldSelStart ||
            mState.mSelectionEnd != oldSelEnd)
            mCursorPositionChanged = true;
    }

    public void SelectWordUnderCursor()
    {
        var c = GetCursorPosition();
        SetSelection(FindWordStart(c), FindWordEnd(c));
    }

    public void SelectAll()
    {
        SetSelection(new Coordinates(0, 0), new Coordinates(mLines.Count, 0));
    }

    public void Copy()
    {
        if (HasSelection)
        {
            ImGui.SetClipboardText(GetSelectedText());
        }
        else
        {
            if (mLines.Count != 0)
            {
                StringBuilder str = new();
                var line = mLines[GetActualCursorCoordinates().mLine];
                foreach (var g in line)
                    str.Append(g.mChar);
                ImGui.SetClipboardText(str.ToString());
            }
        }
    }

    public void Cut()
    {
        if (IsReadOnly)
        {
            Copy();
        }
        else
        {
            if (HasSelection)
            {
                UndoRecord u = new();
                u.mBefore = mState;
                u.mRemoved = GetSelectedText();
                u.mRemovedStart = mState.mSelectionStart;
                u.mRemovedEnd = mState.mSelectionEnd;

                Copy();
                DeleteSelection();

                u.mAfter = mState;
                AddUndo(u);
            }
        }
    }

    public void Paste()
    {
        if (IsReadOnly)
            return;

        var clipText = ImGui.GetClipboardText();
        if (clipText != "" && clipText.Length > 0)
        {
            UndoRecord u = new();
            u.mBefore = mState;

            if (HasSelection)
            {
                u.mRemoved = GetSelectedText();
                u.mRemovedStart = mState.mSelectionStart;
                u.mRemovedEnd = mState.mSelectionEnd;
                DeleteSelection();
            }

            u.mAdded = clipText;
            u.mAddedStart = GetActualCursorCoordinates();

            InsertText(clipText);

            u.mAddedEnd = GetActualCursorCoordinates();
            u.mAfter = mState;
            AddUndo(u);
        }
    }

    public void Delete()
    {
        if (mLines.Count == 0)
            return;

        UndoRecord u = new();
        u.mBefore = mState;

        if (HasSelection)
        {
            u.mRemoved = GetSelectedText();
            u.mRemovedStart = mState.mSelectionStart;
            u.mRemovedEnd = mState.mSelectionEnd;

            DeleteSelection();
        }
        else
        {
            var pos = GetActualCursorCoordinates();
            SetCursorPosition(pos);
            var line = mLines[pos.mLine];

            if (pos.mColumn == GetLineMaxColumn(pos.mLine))
            {
                if (pos.mLine == mLines.Count - 1)
                    return;

                u.mRemoved = "\n";
                u.mRemovedStart = u.mRemovedEnd = GetActualCursorCoordinates();
                Advance(u.mRemovedEnd);

                var nextLine = mLines[pos.mLine + 1];
                line.AddRange(nextLine);
                RemoveLine(pos.mLine + 1);
            }
            else
            {
                var cindex = GetCharacterIndex(pos);
                u.mRemovedStart = u.mRemovedEnd = GetActualCursorCoordinates();
                u.mRemovedEnd.mColumn++;
                u.mRemoved = GetText(u.mRemovedStart, u.mRemovedEnd);

                var d = UTF8CharLength(line[cindex].mChar);
                while (d-- > 0 && cindex < line.Count)
                    line.RemoveAt(cindex);
            }

            mTextChanged = true;

            Colorize(pos.mLine, 1);
        }

        u.mAfter = mState;
        AddUndo(u);
    }

    public bool CanUndo()
    {
        return !IsReadOnly && mUndoIndex > 0;
    }

    public bool CanRedo()
    {
        return !IsReadOnly && mUndoIndex < mUndoBuffer.Count;
    }

    public void Undo(int aSteps = 1)
    {
        mSuppressUndo = true;
        while (CanUndo() && aSteps-- > 0)
        {
            --mUndoIndex;
            mUndoBuffer[mUndoIndex].Undo(this);
        }

        mSuppressUndo = false;
    }

    public void Redo(int aSteps = 1)
    {
        mSuppressUndo = true;
        while (CanRedo() && aSteps-- > 0)
        {
            mUndoBuffer[mUndoIndex].Redo(this);
            ++mUndoIndex;
        }

        mSuppressUndo = false;
    }

    private void ProcessInputs()
    {
    }

    internal void Colorize(int aFromLine = 0, int aLines = -1)
    {
        var toLine = aLines == -1 ? mLines.Count : Math.Max(mLines.Count, aFromLine + aLines);
        mColorRangeMin = Math.Min(mColorRangeMin, aFromLine);
        mColorRangeMax = Math.Max(mColorRangeMax, toLine);
        mColorRangeMin = Math.Max(0, mColorRangeMin);
        mColorRangeMax = Math.Max(mColorRangeMin, mColorRangeMax);
        mCheckComments = true;
    }

    internal void ColorizeRange(int fromLine = 0, int toLine = 0)
    {
        if (mLines.Count == 0 || fromLine >= toLine)
            return;

        var endLine = Math.Min(toLine, mLines.Count);

        for (var lineIndex = fromLine; lineIndex < endLine; lineIndex++)
        {
            var line = mLines[lineIndex];
            if (line.Count == 0)
                continue;

            // Build plain string from glyphs
            var lineText = new StringBuilder(line.Count);
            for (var i = 0; i < line.Count; i++)
            {
                lineText.Append(line[i].mChar);
                var glyph = line[i];
                glyph.mColorIndex = PaletteIndex.Default;
                line[i] = glyph;
            }

            var buffer = lineText.ToString();

            if (mLanguageDefinition.TokenizeLine != null)
            {
                foreach (var token in mLanguageDefinition.TokenizeLine(buffer))
                    for (var i = token.Start; i < token.End && i < line.Count; i++)
                    {
                        var glyph = line[i];
                        glyph.mColorIndex = token.Color;
                        line[i] = glyph;
                    }
            }
            else
            {
                // Regex fallback
                foreach (var (regex, color) in mLanguageDefinition.RegexTokens)
                foreach (Match match in regex.Matches(buffer))
                {
                    var start = match.Index;
                    var end = start + match.Length;

                    for (var i = start; i < end && i < line.Count; i++)
                    {
                        var glyph = line[i];
                        glyph.mColorIndex = color;
                        line[i] = glyph;
                    }
                }

                // Post-process for keywords or known identifiers
                for (var i = 0; i < line.Count;)
                    if (char.IsLetter(line[i].mChar) || line[i].mChar == '_')
                    {
                        var start = i;
                        var end = i;

                        while (end < line.Count && (char.IsLetterOrDigit(line[end].mChar) || line[end].mChar == '_'))
                            end++;

                        var id = buffer.Substring(start, end - start);
                        if (!mLanguageDefinition.CaseSensitive)
                            id = id.ToLower();

                        var color = PaletteIndex.Identifier;
                        if (mLanguageDefinition.Keywords.Contains(id))
                            color = PaletteIndex.Keyword;
                        else if (mLanguageDefinition.Identifiers.ContainsKey(id))
                            color = PaletteIndex.KnownIdentifier;
                        else if (mLanguageDefinition.PreprocIdentifiers.ContainsKey(id))
                            color = PaletteIndex.PreprocessorIdentifier;

                        for (var j = start; j < end && j < line.Count; j++)
                        {
                            var glyph = line[j];
                            glyph.mColorIndex = color;
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


    private void ColorizeInternal()
    {
        if (mLines.Count == 0 || !mColorizerEnabled)
            return;

        if (mCheckComments)
        {
            var endLine = mLines.Count;
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
                var line = mLines[currentLine];

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
                    var c = g.mChar;

                    if (c != mLanguageDefinition.PreprocChar && !char.IsWhiteSpace(c))
                        firstChar = false;

                    if (currentIndex == line.Count - 1 && line[line.Count - 1].mChar == '\\')
                        concatenate = true;

                    var inComment = commentStartLine < currentLine ||
                                    (commentStartLine == currentLine && commentStartIndex <= currentIndex);

                    if (withinString)
                    {
                        var mod = line[currentIndex];
                        mod.mMultiLineComment = inComment;
                        line[currentIndex] = mod;

                        if (c == '\"')
                        {
                            if (currentIndex + 1 < line.Count && line[currentIndex + 1].mChar == '\"')
                            {
                                currentIndex += 1;
                                if (currentIndex < line.Count)
                                {
                                    var nlc = line[currentIndex];
                                    nlc.mMultiLineComment = inComment;
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
                                nlc.mMultiLineComment = inComment;
                                line[currentIndex] = nlc;
                            }
                        }
                    }
                    else
                    {
                        if (firstChar && c == mLanguageDefinition.PreprocChar)
                            withinPreproc = true;

                        if (c == '\"')
                        {
                            withinString = true;
                            var nlc = line[currentIndex];
                            nlc.mMultiLineComment = inComment;
                            line[currentIndex] = nlc;
                        }
                        else
                        {
                            var pred = (char a, Glyph b) => a == b.mChar;
                            //var pred = [](const char& a, const Glyph& b) { return a == b.mChar; };
                            var from = currentIndex;

                            var startStr = mLanguageDefinition.CommentStart;
                            var singleStartStr = mLanguageDefinition.SingleLineComment;

                            bool equals(string a, Line line, int count)
                            {
                                var eq = true;
                                for (var i = 0; i < a.Length && i < count; i++)
                                    eq &= pred(a[i], line[i]);

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
                                                    (commentStartLine == currentLine &&
                                                     commentStartIndex <= currentIndex);

                            var lt = line[currentIndex];
                            lt.mMultiLineComment = inComment;
                            lt.mComment = withinSingleLineComment;
                            line[currentIndex] = lt;

                            var endStr = mLanguageDefinition.CommentEnd;
                            if (currentIndex + 1 >= endStr.Length &&
                                equals(endStr, line, endStr.Length))
                            {
                                commentStartIndex = endIndex;
                                commentStartLine = endLine;
                            }
                        }
                    }

                    var t = line[currentIndex];
                    t.mPreprocessor = withinPreproc;
                    line[currentIndex] = t;

                    currentIndex += UTF8CharLength(c);
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

            mCheckComments = false;
        }

        if (mColorRangeMin < mColorRangeMax)
        {
            var increment = mLanguageDefinition.TokenizeLine == null ? 10 : 10000;
            var to = Math.Min(mColorRangeMin + increment, mColorRangeMax);
            ColorizeRange(mColorRangeMin, to);
            mColorRangeMin = to;

            if (mColorRangeMax == mColorRangeMin)
            {
                mColorRangeMin = int.MaxValue;
                mColorRangeMax = 0;
            }
        }
    }

    private float TextDistanceToLineStart(Coordinates aFrom)
    {
        var line = mLines[aFrom.mLine];
        var distance = 0.0f;
        //float spaceSize = ImGui::GetFont()->CalcTextSizeA(ImGui::GetFontSize(), FLT_MAX, -1.0f, " ", nullptr, nullptr).x;
        var spaceSize = ImGui.CalcTextSize(" ").X; // Not sure if that's correct
        var colIndex = GetCharacterIndex(aFrom);
        for (var it = 0; it < line.Count && it < colIndex;)
            if (line[it].mChar == '\t')
            {
                distance = (float)(1.0f + Math.Floor((1.0f + distance) / (mTabSize * spaceSize))) *
                           (mTabSize * spaceSize);
                ++it;
            }
            else
            {
                var d = UTF8CharLength(line[it].mChar);
                var l = d;
                var tempCString = new char[7];
                var i = 0;
                for (; i < 6 && d-- > 0 && it < line.Count; i++, it++)
                    tempCString[i] = line[it].mChar;

                tempCString[i] = '\0';
                if (l > 0)
                    distance += ImGui.CalcTextSize(new string(tempCString, 0, l)).X;
            }

        return distance;
    }

    internal void EnsureCursorVisible()
    {
        if (!mWithinRender)
        {
            mScrollToCursor = true;
            return;
        }

        var scrollX = ImGui.GetScrollX();
        var scrollY = ImGui.GetScrollY();

        var height = ImGui.GetWindowHeight();
        var width = ImGui.GetWindowWidth();

        var top = 1 + (int)Math.Ceiling(scrollY / mCharAdvance.Y);
        var bottom = (int)Math.Ceiling((scrollY + height) / mCharAdvance.Y);

        var left = (int)Math.Ceiling(scrollX / mCharAdvance.X);
        var right = (int)Math.Ceiling((scrollX + width) / mCharAdvance.X);

        var pos = GetActualCursorCoordinates();
        var len = TextDistanceToLineStart(pos);

        if (pos.mLine < top)
            ImGui.SetScrollY(Math.Max(0.0f, (pos.mLine - 1) * mCharAdvance.Y));
        if (pos.mLine > bottom - 4)
            ImGui.SetScrollY(Math.Max(0.0f, (pos.mLine + 4) * mCharAdvance.Y - height));
        if (len + mTextStart < left + 4)
            ImGui.SetScrollX(Math.Max(0.0f, len + mTextStart - 4));
        if (len + mTextStart > right - 4)
            ImGui.SetScrollX(Math.Max(0.0f, len + mTextStart + 4 - width));
    }

    private int GetPageSize()
    {
        var height = ImGui.GetWindowHeight() - 20.0f;
        return (int)Math.Floor(height / mCharAdvance.Y);
    }

    private string GetText(Coordinates aStart, Coordinates aEnd)
    {
        var lstart = aStart.mLine;
        var lend = aEnd.mLine;
        var istart = GetCharacterIndex(aStart);
        var iend = GetCharacterIndex(aEnd);
        var s = 0;

        for (var i = lstart; i < lend; i++)
            s += mLines[i].Count;

        StringBuilder result = new(s + s / 8);

        while (istart < iend || lstart < lend)
        {
            if (lstart >= mLines.Count)
                break;

            var line = mLines[lstart];
            if (istart < line.Count)
            {
                result.Append(line[istart].mChar);
                istart++;
            }
            else
            {
                istart = 0;
                ++lstart;
                result.Append('\n');
            }
        }

        return result.ToString();
    }

    private Coordinates GetActualCursorCoordinates()
    {
        return SanitizeCoordinates(mState.mCursorPosition);
    }

    private Coordinates SanitizeCoordinates(Coordinates aValue)
    {
        var line = aValue.mLine;
        var column = aValue.mColumn;
        if (line >= mLines.Count)
        {
            if (mLines.Count == 0)
            {
                line = 0;
                column = 0;
            }
            else
            {
                line = mLines.Count - 1;
                column = GetLineMaxColumn(line);
            }

            return new Coordinates(line, column);
        }

        column = mLines.Count == 0 ? 0 : Math.Min(column, GetLineMaxColumn(line));
        return new Coordinates(line, column);
    }

    private void Advance(Coordinates aCoordinates)
    {
        if (aCoordinates.mLine < mLines.Count)
        {
            var line = mLines[aCoordinates.mLine];
            var cindex = GetCharacterIndex(aCoordinates);

            if (cindex + 1 < line.Count)
            {
                var delta = UTF8CharLength(line[cindex].mChar);
                cindex = Math.Min(cindex + delta, line.Count - 1);
            }
            else
            {
                ++aCoordinates.mLine;
                cindex = 0;
            }

            aCoordinates.mColumn = GetCharacterColumn(aCoordinates.mLine, cindex);
        }
    }

    internal void DeleteRange(Coordinates aStart, Coordinates aEnd)
    {
        if (aStart.mLine >= mLines.Count || aEnd.mLine >= mLines.Count)
            return;

        if (aEnd == aStart)
            return;

        var start = GetCharacterIndex(aStart);
        var end = GetCharacterIndex(aEnd);

        if (aStart.mLine == aEnd.mLine)
        {
            var line = mLines[aStart.mLine];
            var n = GetLineMaxColumn(aStart.mLine);
            if (aEnd.mColumn >= n)
                line = line.Take(start).ToList();
            else
                line = line.Take(start).Take(end - start).ToList();
            mLines[aStart.mLine] = line;
        }
        else
        {
            var firstLine = mLines[aStart.mLine];
            var lastLine = mLines[aEnd.mLine];

            lastLine = lastLine.TakeLast(lastLine.Count - end).ToList();
            firstLine = firstLine.Take(start).ToList();

            firstLine.AddRange(lastLine);
            mLines[aStart.mLine] = firstLine;
            mLines[aEnd.mLine] = lastLine;

            if (aStart.mLine < aEnd.mLine)
                firstLine.AddRange(lastLine);

            if (aStart.mLine < aEnd.mLine)
                RemoveLine(aStart.mLine + 1, aEnd.mLine + 1);
        }

        mTextChanged = true;
    }

    internal int InsertTextAt(Coordinates aWhere, string aValue)
    {
        var totalLines = 0;

        while (aWhere.mLine >= mLines.Count)
            mLines.Add(new Line());

        var cindex = GetCharacterIndex(aWhere);

        for (var i = 0; i < aValue.Length; i++)
        {
            if (aValue[i] == '\r')
                continue;

            if (aValue[i] == '\n')
            {
                while (aWhere.mLine >= mLines.Count)
                    mLines.Add(new Line());

                var currentLine = mLines[aWhere.mLine];

                if (cindex < currentLine.Count)
                {
                    var newLine = InsertLine(aWhere.mLine + 1);
                    newLine.AddRange(currentLine.Skip(cindex));
                    currentLine.RemoveRange(cindex, currentLine.Count - cindex);
                }
                else
                {
                    InsertLine(aWhere.mLine + 1);
                }

                ++aWhere.mLine;
                aWhere.mColumn = 0;
                cindex = 0;
                ++totalLines;
            }
            else
            {
                while (aWhere.mLine >= mLines.Count)
                    mLines.Add(new Line());

                var line = mLines[aWhere.mLine];
                var d = UTF8CharLength(aValue[i]);
                while (d-- > 0 && i < aValue.Length && aValue[i] != '\0')
                    line.Insert(cindex++, new Glyph(aValue[i], PaletteIndex.Default));
                ++aWhere.mColumn;
            }

            mTextChanged = true;
        }

        return totalLines;
    }


    private void AddUndo(UndoRecord aValue)
    {
        if (mSuppressUndo)
            return;

        if (mUndoIndex < mUndoBuffer.Count)
            mUndoBuffer.RemoveRange(mUndoIndex, mUndoBuffer.Count - mUndoIndex);

        mUndoBuffer.Add(aValue);
        ++mUndoIndex;
    }

    private Coordinates ScreenPosToCoordinates(Vector2 aPosition)
    {
        var origin = ImGui.GetCursorScreenPos();
        Vector2 local = new(aPosition.X - origin.X, aPosition.Y - origin.Y);

        var lineNo = Math.Max(0, (int)Math.Floor(local.Y / mCharAdvance.Y));

        var columnCoord = 0;

        if (lineNo >= 0 && lineNo < mLines.Count)
        {
            var line = mLines[lineNo];

            var columnIndex = 0;
            var columnX = 0.0f;

            while (columnIndex < line.Count)
            {
                var columnWidth = 0.0f;

                if (line[columnIndex].mChar == '\t')
                {
                    // float spaceSize = ImGui.GetFont().CalcTextSizeA(ImGui.GetFontSize(), float.Max, -1.0f, " ").x;
                    var spaceSize = ImGui.CalcTextSize(" ").X; // Not sure if this is correct
                    var oldX = columnX;
                    var newColumnX = (float)(1.0f + Math.Floor((1.0f + columnX) / (mTabSize * spaceSize))) *
                                     (mTabSize * spaceSize);
                    columnWidth = newColumnX - oldX;
                    if (mTextStart + columnX + columnWidth * 0.5f > local.X)
                        break;
                    columnX = newColumnX;
                    columnCoord = columnCoord / mTabSize * mTabSize + mTabSize;
                    columnIndex++;
                }
                else
                {
                    var buf = new char[7];
                    var d = UTF8CharLength(line[columnIndex].mChar);
                    var i = 0;
                    while (i < 6 && d-- > 0)
                        buf[i++] = line[columnIndex++].mChar;
                    buf[i] = '\0';
                    // columnWidth = ImGui::GetFont()->CalcTextSizeA(ImGui,.GetFontSize(), FLT_MAX, -1.0f, buf).x;
                    columnWidth = ImGui.CalcTextSize(" ").X; // Not sure if this is correct
                    if (mTextStart + columnX + columnWidth * 0.5f > local.X)
                        break;
                    columnX += columnWidth;
                    columnCoord++;
                }
            }
        }

        return SanitizeCoordinates(new Coordinates(lineNo, columnCoord));
    }

    private Coordinates FindWordStart(Coordinates aFrom)
    {
        var at = aFrom;
        if (at.mLine >= mLines.Count)
            return at;

        var line = mLines[at.mLine];
        var cindex = GetCharacterIndex(at);

        if (cindex >= line.Count)
            return at;

        while (cindex > 0 && char.IsWhiteSpace(line[cindex].mChar))
            --cindex;

        var cstart = line[cindex].mColorIndex;
        while (cindex > 0)
        {
            var c = line[cindex].mChar;
            if ((c & 0xC0) != 0x80) // not UTF code sequence 10xxxxxx
            {
                if (c <= 32 && char.IsWhiteSpace(c))
                {
                    cindex++;
                    break;
                }

                if (cstart != line[cindex - 1].mColorIndex)
                    break;
            }

            --cindex;
        }

        return new Coordinates(at.mLine, GetCharacterColumn(at.mLine, cindex));
    }

    private Coordinates FindWordEnd(Coordinates aFrom)
    {
        var at = aFrom;
        if (at.mLine >= mLines.Count)
            return at;

        var line = mLines[at.mLine];
        var cindex = GetCharacterIndex(at);

        if (cindex >= line.Count)
            return at;

        var prevspace = char.IsWhiteSpace(line[cindex].mChar);
        var cstart = line[cindex].mColorIndex;
        while (cindex < line.Count)
        {
            var c = line[cindex].mChar;
            var d = UTF8CharLength(c);
            if (cstart != line[cindex].mColorIndex)
                break;

            if (prevspace != char.IsWhiteSpace(c))
            {
                if (char.IsWhiteSpace(c))
                    while (cindex < line.Count && char.IsWhiteSpace(line[cindex].mChar))
                        ++cindex;
                break;
            }

            cindex += d;
        }

        return new Coordinates(aFrom.mLine, GetCharacterColumn(aFrom.mLine, cindex));
    }

    private Coordinates FindNextWord(Coordinates aFrom)
    {
        var at = aFrom;
        if (at.mLine >= mLines.Count)
            return at;

        // skip to the next non-word character
        var cindex = GetCharacterIndex(aFrom);
        var isword = false;
        var skip = false;
        if (cindex < mLines[at.mLine].Count)
        {
            var line = mLines[at.mLine];
            isword = char.IsLetterOrDigit(line[cindex].mChar);
            skip = isword;
        }

        while (!isword || skip)
        {
            if (at.mLine >= mLines.Count)
            {
                var l = Math.Max(0, mLines.Count - 1);
                return new Coordinates(l, GetLineMaxColumn(l));
            }

            var line = mLines[at.mLine];
            if (cindex < line.Count)
            {
                isword = char.IsLetterOrDigit(line[cindex].mChar);

                if (isword && !skip)
                    return new Coordinates(at.mLine, GetCharacterColumn(at.mLine, cindex));

                if (!isword)
                    skip = false;

                cindex++;
            }
            else
            {
                cindex = 0;
                ++at.mLine;
                skip = false;
                isword = false;
            }
        }

        return at;
    }

    private int GetCharacterIndex(Coordinates aCoordinates)
    {
        if (aCoordinates.mLine >= mLines.Count)
            return -1;
        var line = mLines[aCoordinates.mLine];
        var c = 0;
        var i = 0;
        for (; i < line.Count && c < aCoordinates.mColumn;)
        {
            if (line[i].mChar == '\t')
                c = c / mTabSize * mTabSize + mTabSize;
            else
                ++c;
            i += UTF8CharLength(line[i].mChar);
        }

        return i;
    }

    private int GetCharacterColumn(int aLine, int aIndex)
    {
        if (aLine >= mLines.Count)
            return 0;
        var line = mLines[aLine];
        var col = 0;
        var i = 0;
        while (i < aIndex && i < line.Count)
        {
            var c = line[i].mChar;
            i += UTF8CharLength(c);
            if (c == '\t')
                col = col / mTabSize * mTabSize + mTabSize;
            else
                col++;
        }

        return col;
    }

    private int GetLineCharacterCount(int aLine)
    {
        if (aLine >= mLines.Count)
            return 0;
        var line = mLines[aLine];
        var c = 0;
        for (var i = 0; i < line.Count; c++)
            i += UTF8CharLength(line[i].mChar);
        return c;
    }

    private int GetLineMaxColumn(int aLine)
    {
        if (aLine >= mLines.Count)
            return 0;
        var line = mLines[aLine];
        var col = 0;
        for (var i = 0; i < line.Count;)
        {
            var c = line[i].mChar;
            if (c == '\t')
                col = col / mTabSize * mTabSize + mTabSize;
            else
                col++;
            i += UTF8CharLength(c);
        }

        return col;
    }

    private bool IsOnWordBoundary(Coordinates aAt)
    {
        if (aAt.mLine >= mLines.Count || aAt.mColumn == 0)
            return true;

        var line = mLines[aAt.mLine];
        var cindex = GetCharacterIndex(aAt);
        if (cindex >= line.Count)
            return true;

        if (mColorizerEnabled)
            return line[cindex].mColorIndex != line[cindex - 1].mColorIndex;

        return char.IsWhiteSpace(line[cindex].mChar) != char.IsWhiteSpace(line[cindex - 1].mChar);
    }

    private void RemoveLine(int aStart, int aEnd)
    {
        for (var i = aStart; i < aEnd; i++)
            RemoveLine(aStart);
    }

    private void RemoveLine(int aIndex)
    {
        mLines.RemoveAt(aIndex);

        mTextChanged = true;
    }

    private Line InsertLine(int aIndex)
    {
        // assert(!mReadOnly);

        mLines.Insert(aIndex, new Line());
        var result = mLines[aIndex];

        return result;
    }

    private void EnterCharacter(char aChar, bool aShift)
    {
        // assert(!mReadOnly);

        UndoRecord u = new();

        u.mBefore = mState;

        if (HasSelection)
        {
            if (aChar == '\t' && mState.mSelectionStart.mLine != mState.mSelectionEnd.mLine)
            {
                var start = mState.mSelectionStart;
                var end = mState.mSelectionEnd;
                var originalEnd = end;

                if (start > end)
                {
                    var tmp = start;
                    start = end;
                    end = tmp;
                }

                start.mColumn = 0;
                //			end.mColumn = end.mLine < mLines.size() ? mLines[end.mLine].size() : 0;
                if (end.mColumn == 0 && end.mLine > 0)
                    --end.mLine;
                if (end.mLine >= mLines.Count)
                    end.mLine = mLines.Count == 0 ? 0 : mLines.Count - 1;
                end.mColumn = GetLineMaxColumn(end.mLine);

                //if (end.mColumn >= GetLineMaxColumn(end.mLine))
                //	end.mColumn = GetLineMaxColumn(end.mLine) - 1;

                u.mRemovedStart = start;
                u.mRemovedEnd = end;
                u.mRemoved = GetText(start, end);

                var modified = false;

                for (var i = start.mLine; i <= end.mLine; i++)
                {
                    var line = mLines[i];
                    if (aShift)
                    {
                        if (line.Count != 0)
                        {
                            if (line[0].mChar == '\t')
                            {
                                line.RemoveAt(0);
                                modified = true;
                            }
                            else
                            {
                                for (var j = 0; j < mTabSize && line.Count != 0 && line[0].mChar == ' '; j++)
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
                    start = new Coordinates(start.mLine, GetCharacterColumn(start.mLine, 0));
                    Coordinates rangeEnd = new();
                    if (originalEnd.mColumn != 0)
                    {
                        end = new Coordinates(end.mLine, GetLineMaxColumn(end.mLine));
                        rangeEnd = end;
                        u.mAdded = GetText(start, end);
                    }
                    else
                    {
                        end = new Coordinates(originalEnd.mLine, 0);
                        rangeEnd = new Coordinates(end.mLine - 1, GetLineMaxColumn(end.mLine - 1));
                        u.mAdded = GetText(start, rangeEnd);
                    }

                    u.mAddedStart = start;
                    u.mAddedEnd = rangeEnd;
                    u.mAfter = mState;

                    mState.mSelectionStart = start;
                    mState.mSelectionEnd = end;
                    AddUndo(u);

                    mTextChanged = true;

                    EnsureCursorVisible();
                }

                return;
            } // c == '\t'

            u.mRemoved = GetSelectedText();
            u.mRemovedStart = mState.mSelectionStart;
            u.mRemovedEnd = mState.mSelectionEnd;
            DeleteSelection();
        } // HasSelection

        var coord = GetActualCursorCoordinates();
        u.mAddedStart = coord;

        //assert(!mLines.empty());

        if (aChar == '\n')
        {
            InsertLine(coord.mLine + 1);
            var line = mLines[coord.mLine];
            var newLine = mLines[coord.mLine + 1];

            if (mLanguageDefinition.AutoIndentation)
                for (var it = 0;
                     it < line.Count && char.IsAscii(line[it].mChar) && char.IsWhiteSpace(line[it].mChar);
                     ++it)
                    newLine.Add(line[it]);

            var whitespaceSize = newLine.Count;
            var cindex = GetCharacterIndex(coord);
            newLine.AddRange(line.TakeLast(line.Count - cindex));
            line.RemoveRange(cindex, line.Count - cindex);
            SetCursorPosition(new Coordinates(coord.mLine + 1, GetCharacterColumn(coord.mLine + 1, whitespaceSize)));
            u.mAdded = aChar.ToString();
        }
        else
        {
            var buf = new char[7];
            var e = ImTextCharToUtf8(ref buf, 7, aChar);
            if (e > 0)
            {
                buf[e] = '\0';
                var line = mLines[coord.mLine];
                var cindex = GetCharacterIndex(coord);

                if (IsOverwrite && cindex < line.Count)
                {
                    var d = UTF8CharLength(line[cindex].mChar);

                    u.mRemovedStart = mState.mCursorPosition;
                    u.mRemovedEnd = new Coordinates(coord.mLine, GetCharacterColumn(coord.mLine, cindex + d));

                    while (d-- > 0 && cindex < line.Count)
                    {
                        u.mRemoved += line[cindex].mChar;
                        line.RemoveAt(cindex);
                    }
                }

                var added = 0;
                foreach (var c in buf)
                {
                    if (c == '\0')
                        break;
                    line.Insert(cindex, new Glyph(c, PaletteIndex.Default));
                    added++;
                    cindex++;
                }

                u.mAdded = aChar.ToString();

                SetCursorPosition(new Coordinates(coord.mLine, GetCharacterColumn(coord.mLine, cindex)));
            }
            else
            {
                return;
            }
        }

        mTextChanged = true;

        u.mAddedEnd = GetActualCursorCoordinates();
        u.mAfter = mState;

        AddUndo(u);

        Colorize(coord.mLine - 1, 3);
        EnsureCursorVisible();
    }

    private void Backspace()
    {
        // assert(!mReadOnly);

        if (mLines.Count == 0)
            return;

        UndoRecord u = new();
        u.mBefore = mState;

        if (HasSelection)
        {
            u.mRemoved = GetSelectedText();
            u.mRemovedStart = mState.mSelectionStart;
            u.mRemovedEnd = mState.mSelectionEnd;

            DeleteSelection();
        }
        else
        {
            var pos = GetActualCursorCoordinates();
            SetCursorPosition(pos);

            if (mState.mCursorPosition.mColumn == 0)
            {
                if (mState.mCursorPosition.mLine == 0)
                    return;

                u.mRemoved = "\n";
                u.mRemovedStart = u.mRemovedEnd = new Coordinates(pos.mLine - 1, GetLineMaxColumn(pos.mLine - 1));
                Advance(u.mRemovedEnd);

                var prevSize = GetLineMaxColumn(mState.mCursorPosition.mLine - 1);

                RemoveLine(mState.mCursorPosition.mLine);
                --mState.mCursorPosition.mLine;
                mState.mCursorPosition.mColumn = prevSize;
            }
            else
            {
                var line = mLines[mState.mCursorPosition.mLine];
                var cindex = GetCharacterIndex(pos) - 1;
                var cend = cindex + 1;
                while (cindex > 0 && IsUTFSequence(line[cindex].mChar))
                    --cindex;

                //if (cindex > 0 && UTF8CharLength(line[cindex].mChar) > 1)
                //	--cindex;

                u.mRemovedStart = u.mRemovedEnd = GetActualCursorCoordinates();
                --u.mRemovedStart.mColumn;
                --mState.mCursorPosition.mColumn;

                while (cindex < line.Count && cend-- > cindex)
                {
                    u.mRemoved += line[cindex].mChar;
                    line.RemoveAt(cindex);
                }
            }

            mTextChanged = true;

            EnsureCursorVisible();
            Colorize(mState.mCursorPosition.mLine, 1);
        }

        u.mAfter = mState;
        AddUndo(u);
    }

    private void DeleteSelection()
    {
        // assert(mState.mSelectionEnd >= mState.mSelectionStart);

        if (mState.mSelectionEnd == mState.mSelectionStart)
            return;

        DeleteRange(mState.mSelectionStart, mState.mSelectionEnd);

        SetSelection(mState.mSelectionStart, mState.mSelectionStart);
        SetCursorPosition(mState.mSelectionStart);
        Colorize(mState.mSelectionStart.mLine, 1);
    }

    private string GetWordUnderCursor()
    {
        var c = GetCursorPosition();
        return GetWordAt(c);
    }

    private string GetWordAt(Coordinates aCoords)
    {
        var start = FindWordStart(aCoords);
        var end = FindWordEnd(aCoords);

        StringBuilder r = new();

        var istart = GetCharacterIndex(start);
        var iend = GetCharacterIndex(end);

        for (var it = istart; it < iend; ++it)
            r.Append(mLines[aCoords.mLine][it].mChar);

        return r.ToString();
    }

    private uint GetGlyphColor(Glyph aGlyph)
    {
        if (!mColorizerEnabled)
            return mPalette[(int)PaletteIndex.Default];
        if (aGlyph.mComment)
            return mPalette[(int)PaletteIndex.Comment];
        if (aGlyph.mMultiLineComment)
            return mPalette[(int)PaletteIndex.MultiLineComment];
        var color = mPalette[(int)aGlyph.mColorIndex];
        if (aGlyph.mPreprocessor)
        {
            var ppcolor = mPalette[(int)PaletteIndex.Preprocessor];
            var c0 = (int)((ppcolor & 0xff) + (color & 0xff)) / 2;
            var c1 = (int)(((ppcolor >> 8) & 0xff) + ((color >> 8) & 0xff)) / 2;
            var c2 = (int)(((ppcolor >> 16) & 0xff) + ((color >> 16) & 0xff)) / 2;
            var c3 = (int)(((ppcolor >> 24) & 0xff) + ((color >> 24) & 0xff)) / 2;
            return (uint)(c0 | (c1 << 8) | (c2 << 16) | (c3 << 24));
        }

        return color;
    }

    private void HandleKeyboardInputs()
    {
        var io = ImGui.GetIO();
        var shift = io.KeyShift;
        var ctrl = io.ConfigMacOSXBehaviors ? io.KeySuper : io.KeyCtrl;
        var alt = io.ConfigMacOSXBehaviors ? io.KeyCtrl : io.KeyAlt;

        if (ImGui.IsWindowFocused())
        {
            if (ImGui.IsWindowHovered())
                ImGui.SetMouseCursor(ImGuiMouseCursor.TextInput);
            //ImGui::CaptureKeyboardFromApp(true);

            io.WantCaptureKeyboard = true;
            io.WantTextInput = true;

            if (!IsReadOnly && ctrl && !shift && !alt && ImGui.IsKeyPressed(ImGuiKey.Z))
                Undo();
            else if (!IsReadOnly && !ctrl && !shift && alt && ImGui.IsKeyPressed(ImGuiKey.Backspace))
                Undo();
            else if (!IsReadOnly && ctrl && !shift && !alt && ImGui.IsKeyPressed(ImGuiKey.Y))
                Redo();
            else if (!ctrl && !alt && ImGui.IsKeyPressed(ImGuiKey.UpArrow))
                MoveUp(1, shift);
            else if (!ctrl && !alt && ImGui.IsKeyPressed(ImGuiKey.DownArrow))
                MoveDown(1, shift);
            else if (!alt && ImGui.IsKeyPressed(ImGuiKey.LeftArrow))
                MoveLeft(1, shift, ctrl);
            else if (!alt && ImGui.IsKeyPressed(ImGuiKey.RightArrow))
                MoveRight(1, shift, ctrl);
            else if (!alt && ImGui.IsKeyPressed(ImGuiKey.PageUp))
                MoveUp(GetPageSize() - 4, shift);
            else if (!alt && ImGui.IsKeyPressed(ImGuiKey.PageDown))
                MoveDown(GetPageSize() - 4, shift);
            else if (!alt && ctrl && ImGui.IsKeyPressed(ImGuiKey.Home))
                MoveTop(shift);
            else if (ctrl && !alt && ImGui.IsKeyPressed(ImGuiKey.End))
                MoveBottom(shift);
            else if (!ctrl && !alt && ImGui.IsKeyPressed(ImGuiKey.Home))
                MoveHome(shift);
            else if (!ctrl && !alt && ImGui.IsKeyPressed(ImGuiKey.End))
                MoveEnd(shift);
            else if (!IsReadOnly && !ctrl && !shift && !alt && ImGui.IsKeyPressed(ImGuiKey.Delete))
                Delete();
            else if (!IsReadOnly && !ctrl && !shift && !alt && ImGui.IsKeyPressed(ImGuiKey.Backspace))
                Backspace();
            else if (!ctrl && !shift && !alt && ImGui.IsKeyPressed(ImGuiKey.Insert))
                IsOverwrite ^= true;
            else if (ctrl && !shift && !alt && ImGui.IsKeyPressed(ImGuiKey.Insert))
                Copy();
            else if (ctrl && !shift && !alt && ImGui.IsKeyPressed(ImGuiKey.C))
                Copy();
            else if (!IsReadOnly && !ctrl && shift && !alt && ImGui.IsKeyPressed(ImGuiKey.Insert))
                Paste();
            else if (!IsReadOnly && ctrl && !shift && !alt && ImGui.IsKeyPressed(ImGuiKey.V))
                Paste();
            else if (ctrl && !shift && !alt && ImGui.IsKeyPressed(ImGuiKey.X))
                Cut();
            else if (!ctrl && shift && !alt && ImGui.IsKeyPressed(ImGuiKey.Delete))
                Cut();
            else if (ctrl && !shift && !alt && ImGui.IsKeyPressed(ImGuiKey.A))
                SelectAll();
            else if (!IsReadOnly && !ctrl && !shift && !alt && ImGui.IsKeyPressed(ImGuiKey.Enter))
                EnterCharacter('\n', false);
            else if (!IsReadOnly && !ctrl && !alt && ImGui.IsKeyPressed(ImGuiKey.Tab))
                EnterCharacter('\t', shift);

            if (!IsReadOnly && io.InputQueueCharacters.Size != 0)
                for (var i = 0; i < io.InputQueueCharacters.Size; i++)
                {
                    var c = io.InputQueueCharacters[i];
                    if (c != 0 && (c == '\n' || c >= 32))
                        EnterCharacter((char)c, shift);
                }
            // This line seems to be untranslatable to C#
            //io.InputQueueCharacters.resize(0);
        }
    }

    private void HandleMouseInputs()
    {
        var io = ImGui.GetIO();
        var shift = io.KeyShift;
        var ctrl = io.ConfigMacOSXBehaviors ? io.KeySuper : io.KeyCtrl;
        var alt = io.ConfigMacOSXBehaviors ? io.KeyCtrl : io.KeyAlt;

        mHasSelectionAnchor = HasSelection && shift;

        if (ImGui.IsWindowHovered())
            if (!shift && !alt)
            {
                var click = ImGui.IsMouseClicked(ImGuiMouseButton.Left);
                var doubleClick = ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left);
                var t = ImGui.GetTime();
                var tripleClick = click && !doubleClick && mLastClick != -1.0f &&
                                  t - mLastClick < io.MouseDoubleClickTime;

                /*
                Left mouse button triple click
                */
                if (tripleClick)
                {
                    if (!ctrl)
                    {
                        mState.mCursorPosition = mInteractiveStart =
                            mInteractiveEnd = ScreenPosToCoordinates(ImGui.GetMousePos());
                        mSelectionMode = SelectionMode.Line;
                        SetSelection(mInteractiveStart, mInteractiveEnd, mSelectionMode);
                    }

                    mLastClick = -1.0f;
                }
                /*
                Left mouse button double click
                */
                else if (doubleClick)
                {
                    if (!ctrl)
                    {
                        mState.mCursorPosition = mInteractiveStart =
                            mInteractiveEnd = ScreenPosToCoordinates(ImGui.GetMousePos());
                        if (mSelectionMode == SelectionMode.Line)
                            mSelectionMode = SelectionMode.Normal;
                        else
                            mSelectionMode = SelectionMode.Word;
                        SetSelection(mInteractiveStart, mInteractiveEnd, mSelectionMode);
                    }

                    mLastClick = (float)ImGui.GetTime();
                }
                /*
                Left mouse button click
                */
                else if (click)
                {
                    mState.mCursorPosition =
                        mInteractiveStart = mInteractiveEnd = ScreenPosToCoordinates(ImGui.GetMousePos());
                    if (ctrl)
                        mSelectionMode = SelectionMode.Word;
                    else
                        mSelectionMode = SelectionMode.Normal;
                    SetSelection(mInteractiveStart, mInteractiveEnd, mSelectionMode);

                    mLastClick = (float)ImGui.GetTime();
                }
                // Mouse left button dragging (=> update selection)
                else if (ImGui.IsMouseDragging(ImGuiMouseButton.Left) && ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    io.WantCaptureMouse = true;
                    mState.mCursorPosition = mInteractiveEnd = ScreenPosToCoordinates(ImGui.GetMousePos());
                    SetSelection(mInteractiveStart, mInteractiveEnd, mSelectionMode);
                }
            }
    }

    private void Render()
    {
        /* Compute mCharAdvance regarding to scaled font size (Ctrl + mouse wheel)*/
        //float fontSize = ImGui::GetFont()->CalcTextSizeA(ImGui::GetFontSize(), FLT_MAX, -1.0f, "#", nullptr, nullptr).x;
        var fontSize = ImGui.CalcTextSize("#").X; // Again, not sure if equal
        mCharAdvance = new Vector2(fontSize, ImGui.GetTextLineHeightWithSpacing() * mLineSpacing);

        /* Update palette with the current alpha from style */
        for (var i = 0; i < (int)PaletteIndex.Max; ++i)
        {
            var color = ImGui.ColorConvertU32ToFloat4(mPaletteBase[i]);
            color.W *= ImGui.GetStyle().Alpha;
            mPalette[i] = ImGui.ColorConvertFloat4ToU32(color);
        }

        //assert(mLineBuffer.empty());

        var contentSize = ImGui.GetWindowContentRegionMax();
        var drawList = ImGui.GetWindowDrawList();
        var longest = mTextStart;

        if (mScrollToTop)
        {
            mScrollToTop = false;
            ImGui.SetScrollY(0.0f);
        }

        var cursorScreenPos = ImGui.GetCursorScreenPos();
        var scrollX = ImGui.GetScrollX();
        var scrollY = ImGui.GetScrollY();

        var lineNo = (int)Math.Floor(scrollY / mCharAdvance.Y);
        var globalLineMax = mLines.Count;
        var lineMax = Math.Max(0,
            Math.Min(mLines.Count - 1, lineNo + (int)Math.Floor((scrollY + contentSize.Y) / mCharAdvance.Y)));

        var buf = mShowLineNumbers ? " " + globalLineMax + " " : "";
        mTextStart = ImGui.CalcTextSize(buf).X;

        if (mLines.Count != 0)
        {
            //float spaceSize = ImGui::GetFont()->CalcTextSizeA(ImGui::GetFontSize(), FLT_MAX, -1.0f, " ", nullptr, nullptr).x;
            var spaceSize = ImGui.CalcTextSize(" ").X;
            while (lineNo <= lineMax)
            {
                var lineStartScreenPos = new Vector2(cursorScreenPos.X, cursorScreenPos.Y + lineNo * mCharAdvance.Y);
                var textScreenPos = new Vector2(lineStartScreenPos.X + mTextStart, lineStartScreenPos.Y);

                var line = mLines[lineNo];
                longest = Math.Max(
                    mTextStart + TextDistanceToLineStart(new Coordinates(lineNo, GetLineMaxColumn(lineNo))), longest);
                var columnNo = 0;
                Coordinates lineStartCoord = new(lineNo, 0);
                Coordinates lineEndCoord = new(lineNo, GetLineMaxColumn(lineNo));

                // Draw selection for the current line
                var sstart = -1.0f;
                var ssend = -1.0f;

                // assert(mState.mSelectionStart <= mState.mSelectionEnd);
                if (mState.mSelectionStart <= lineEndCoord)
                    sstart = mState.mSelectionStart > lineStartCoord
                        ? TextDistanceToLineStart(mState.mSelectionStart)
                        : 0.0f;
                if (mState.mSelectionEnd > lineStartCoord)
                    ssend = TextDistanceToLineStart(mState.mSelectionEnd < lineEndCoord
                        ? mState.mSelectionEnd
                        : lineEndCoord);

                if (mState.mSelectionEnd.mLine > lineNo)
                    ssend += mCharAdvance.X;

                if (sstart != -1 && ssend != -1 && sstart < ssend)
                {
                    Vector2 vstart = new(lineStartScreenPos.X + mTextStart + sstart, lineStartScreenPos.Y);
                    Vector2 vend = new(lineStartScreenPos.X + mTextStart + ssend,
                        lineStartScreenPos.Y + mCharAdvance.Y);
                    drawList.AddRectFilled(vstart, vend, mPalette[(int)PaletteIndex.Selection]);
                }

                var start = new Vector2(lineStartScreenPos.X + scrollX, lineStartScreenPos.Y);

                // Draw line number (right aligned)
                if (mShowLineNumbers)
                {
                    buf = (lineNo + 1) + "  ";

                    var lineNoWidth = ImGui.CalcTextSize(buf).X;
                    drawList.AddText(new Vector2(lineStartScreenPos.X + mTextStart - lineNoWidth, lineStartScreenPos.Y),
                        mPalette[(int)PaletteIndex.LineNumber], buf);
                }

                if (mState.mCursorPosition.mLine == lineNo)
                {
                    var focused = ImGui.IsWindowFocused();

                    // Highlight the current line (where the cursor is)
                    if (!HasSelection)
                    {
                        var end = new Vector2(start.X + contentSize.X + scrollX, start.Y + mCharAdvance.Y);
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
                            var cindex = GetCharacterIndex(mState.mCursorPosition);
                            var cx = TextDistanceToLineStart(mState.mCursorPosition);

                            if (IsOverwrite && cindex < line.Count)
                            {
                                var c = line[cindex].mChar;
                                if (c == '\t')
                                {
                                    var x = (1.0f + Math.Floor((1.0f + cx) / (mTabSize * spaceSize))) *
                                            (mTabSize * spaceSize);
                                    width = (float)(x - cx);
                                }
                                else
                                {
                                    var buf2 = new char[2];
                                    buf2[0] = line[cindex].mChar;
                                    buf2[1] = '\0';
                                    //width = ImGui.GetFont()->CalcTextSizeA(ImGui.GetFontSize(), FLT_MAX, -1.0f, buf2).x;
                                    width = ImGui.CalcTextSize(new string(buf2)).X;
                                }
                            }

                            Vector2 cstart = new(textScreenPos.X + cx, lineStartScreenPos.Y);
                            Vector2 cend = new(textScreenPos.X + cx + width, lineStartScreenPos.Y + mCharAdvance.Y);
                            drawList.AddRectFilled(cstart, cend, mPalette[(int)PaletteIndex.Cursor]);
                            if (elapsed > 800)
                                mStartTime = timeEnd;
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

                    if ((color != prevColor || glyph.mChar == '\t' || glyph.mChar == ' ') && mLineBuffer.Length != 0)
                    {
                        Vector2 newOffset = new(textScreenPos.X + bufferOffset.X, textScreenPos.Y + bufferOffset.Y);
                        drawList.AddText(newOffset, prevColor, mLineBuffer);
                        //var textSize = ImGui::GetFont()->CalcTextSizeA(ImGui::GetFontSize(), FLT_MAX, -1.0f, mLineBuffer.c_str(), nullptr, nullptr);
                        var textSize = ImGui.CalcTextSize(mLineBuffer).X;
                        bufferOffset.X += textSize;
                        mLineBuffer = "";
                    }

                    prevColor = color;

                    if (glyph.mChar == '\t')
                    {
                        var oldX = bufferOffset.X;
                        bufferOffset.X = (float)(1.0f + Math.Floor((1.0f + bufferOffset.X) / (mTabSize * spaceSize))) *
                                         (mTabSize * spaceSize);
                        ++i;

                        if (mShowWhitespaces)
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
                    else if (glyph.mChar == ' ')
                    {
                        if (mShowWhitespaces)
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
                        var l = UTF8CharLength(glyph.mChar);
                        while (l-- > 0)
                            mLineBuffer += line[i++].mChar;
                    }

                    ++columnNo;
                }

                if (mLineBuffer.Count() != 0)
                {
                    Vector2 newOffset = new(textScreenPos.X + bufferOffset.X, textScreenPos.Y + bufferOffset.Y);
                    drawList.AddText(newOffset, prevColor, mLineBuffer);
                    mLineBuffer = "";
                }

                ++lineNo;
            }

            // Draw a tooltip on known identifiers/preprocessor symbols
            if (ImGui.IsMousePosValid())
            {
                var id = GetWordAt(ScreenPosToCoordinates(ImGui.GetMousePos()));
                if (id != "")
                {
                    if (mLanguageDefinition.Identifiers.ContainsKey(id))
                    {
                        var it = mLanguageDefinition.Identifiers[id];
                        ImGui.BeginTooltip();
                        ImGui.TextUnformatted(it.mDeclaration);
                        ImGui.EndTooltip();
                    }
                    else
                    {
                        if (mLanguageDefinition.PreprocIdentifiers.ContainsKey(id))
                        {
                            var pi = mLanguageDefinition.PreprocIdentifiers[id];
                            ImGui.BeginTooltip();
                            ImGui.TextUnformatted(pi.mDeclaration);
                            ImGui.EndTooltip();
                        }
                    }
                }
            }
        }


        ImGui.Dummy(new Vector2(longest + 2, mLines.Count * mCharAdvance.Y));

        if (mScrollToCursor)
        {
            EnsureCursorVisible();
            ImGui.SetWindowFocus();
            mScrollToCursor = false;
        }
    }

    #region Helpers

    private static bool IsUTFSequence(char c)
    {
        return (c & 0xC0) == 0x80;
    }

    private static int UTF8CharLength(char c)
    {
        if ((c & 0xFE) == 0xFC)
            return 6;
        if ((c & 0xFC) == 0xF8)
            return 5;
        if ((c & 0xF8) == 0xF0)
            return 4;
        if ((c & 0xF0) == 0xE0)
            return 3;
        if ((c & 0xE0) == 0xC0)
            return 2;
        return 1;
    }

    private static int ImTextCharToUtf8(ref char[] buf, int buf_size, uint c)
    {
        if (c < 0x80)
        {
            buf[0] = (char)c;
            return 1;
        }

        if (c < 0x800)
        {
            if (buf_size < 2) return 0;
            buf[0] = (char)(0xc0 + (c >> 6));
            buf[1] = (char)(0x80 + (c & 0x3f));
            return 2;
        }

        if (c >= 0xdc00 && c < 0xe000)
            return 0;
        if (c >= 0xd800 && c < 0xdc00)
        {
            if (buf_size < 4) return 0;
            buf[0] = (char)(0xf0 + (c >> 18));
            buf[1] = (char)(0x80 + ((c >> 12) & 0x3f));
            buf[2] = (char)(0x80 + ((c >> 6) & 0x3f));
            buf[3] = (char)(0x80 + (c & 0x3f));
            return 4;
        }

        //else if (c < 0x10000)
        {
            if (buf_size < 3) return 0;
            buf[0] = (char)(0xe0 + (c >> 12));
            buf[1] = (char)(0x80 + ((c >> 6) & 0x3f));
            buf[2] = (char)(0x80 + (c & 0x3f));
            return 3;
        }
    }

    #endregion
}