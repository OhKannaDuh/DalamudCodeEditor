using System.Numerics;
using ImGuiNET;

namespace DalamudCodeEditor.TextEditor;

public class Renderer(Editor editor) : EditorComponent(editor)
{
    public bool IsRendering { get; private set; } = false;

    public float GutterWidth { get; private set; } = 20f;

    public string TextRun = string.Empty;


    public float CharacterWidth
    {
        get => ImGui.CalcTextSize("#").X;
    }

    public float LineHeight
    {
        get => ImGui.GetTextLineHeightWithSpacing() * Style.LineSpacing;
    }

    public Vector2 ContentSize
    {
        get => ImGui.GetWindowContentRegionMax();
    }

    public void SetGutterWidth(float width)
    {
        GutterWidth = width;
    }

    public void Render()
    {
        var contentSize = ImGui.GetWindowContentRegionMax();
        var drawList = ImGui.GetWindowDrawList();
        var longest = GutterWidth;

        Scroll.ScrollToTop();

        var cursorScreenPos = ImGui.GetCursorScreenPos();
        var scrollX = ImGui.GetScrollX();
        var scrollY = ImGui.GetScrollY();

        var lineNo = (int)Math.Floor(scrollY / Renderer.LineHeight);
        var globalLineMax = Buffer.GetLines().Count;
        var lineMax = Math.Max(0,
            Math.Min(Buffer.GetLines().Count - 1, lineNo + (int)Math.Floor((scrollY + contentSize.Y) / LineHeight)));

        var buf = Style.ShowLineNumbers ? " " + globalLineMax + " " : "";
        SetGutterWidth(ImGui.CalcTextSize(buf).X);

        if (Buffer.GetLines().Count != 0)
        {
            var spaceSize = ImGui.CalcTextSize(" ").X;
            while (lineNo <= lineMax)
            {
                var lineStartScreenPos = new Vector2(cursorScreenPos.X, cursorScreenPos.Y + lineNo * LineHeight);
                var textScreenPos = new Vector2(lineStartScreenPos.X + GutterWidth, lineStartScreenPos.Y);

                var line = Buffer.GetLines()[lineNo];
                longest = Math.Max(
                    GutterWidth + Buffer.TextDistanceToLineStart(new Coordinate(lineNo, Buffer.GetLineMaxColumn(lineNo))), longest);
                var columnNo = 0;
                Coordinate lineStartCoord = new(lineNo, 0);
                Coordinate lineEndCoord = new(lineNo, Buffer.GetLineMaxColumn(lineNo));

                // Draw selection for the current line
                var selectinoStart = -1.0f;
                var selectEnd = -1.0f;

                if (State.SelectionStart <= lineEndCoord)
                {
                    selectinoStart = State.SelectionStart > lineStartCoord
                        ? Buffer.TextDistanceToLineStart(State.SelectionStart)
                        : 0.0f;
                }

                if (State.SelectionEnd > lineStartCoord)
                {
                    selectEnd = Buffer.TextDistanceToLineStart(State.SelectionEnd < lineEndCoord
                        ? State.SelectionEnd
                        : lineEndCoord);
                }

                if (State.SelectionEnd.Line > lineNo)
                {
                    selectEnd += CharacterWidth;
                }

                if (selectinoStart != -1 && selectEnd != -1 && selectinoStart < selectEnd)
                {
                    Vector2 vstart = new(lineStartScreenPos.X + GutterWidth + selectinoStart, lineStartScreenPos.Y);
                    Vector2 vend = new(lineStartScreenPos.X + GutterWidth + selectEnd,
                        lineStartScreenPos.Y + LineHeight);
                    drawList.AddRectFilled(vstart, vend, Palette.Selection.GetU32());
                }

                var start = new Vector2(lineStartScreenPos.X + scrollX, lineStartScreenPos.Y);

                // Draw line number (right aligned)
                if (Style.ShowLineNumbers)
                {
                    buf = lineNo + 1 + "  ";

                    var lineNoWidth = ImGui.CalcTextSize(buf).X;
                    drawList.AddText(new Vector2(lineStartScreenPos.X + GutterWidth - lineNoWidth, lineStartScreenPos.Y),
                        Palette.LineNumber.GetU32(), buf);
                }

                if (State.CursorPosition.Line == lineNo)
                {
                    var focused = ImGui.IsWindowFocused();

                    // Highlight the current line (where the cursor is)
                    if (!Selection.HasSelection)
                    {
                        var end = new Vector2(start.X + contentSize.X + scrollX, start.Y + LineHeight);
                        drawList.AddRectFilled(start, end,
                            Palette[focused ? PaletteIndex.CurrentLineFill : PaletteIndex.CurrentLineFillInactive].GetU32());
                        drawList.AddRect(start, end, Palette.CurrentLineEdge.GetU32(), 1.0f);
                    }

                    // Render the cursor
                    if (focused)
                    {
                        var timeEnd = DateTime.Now.Ticks;
                        var elapsed = timeEnd - editor.StartTime;
                        if (elapsed > 400)
                        {
                            var width = 1.0f;
                            var cindex = Buffer.GetCharacterIndex(State.CursorPosition);
                            var cx = Buffer.TextDistanceToLineStart(State.CursorPosition);

                            if (editor.IsOverwrite && cindex < line.Count)
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
                            Vector2 cend = new(textScreenPos.X + cx + width, lineStartScreenPos.Y + LineHeight);
                            drawList.AddRectFilled(cstart, cend, Palette[PaletteIndex.Cursor].GetU32());
                            if (elapsed > 800)
                            {
                                //@todo
                                // mStartTime = timeEnd;
                            }
                        }
                    }
                }

                // Render colorized text
                var prevColor = line.Count == 0 ? Palette[PaletteIndex.Default].GetU32() : Colorizer.GetGlyphColor(line[0]);
                Vector2 bufferOffset = new();

                for (var i = 0; i < line.Count;)
                {
                    var glyph = line[i];
                    var color = Colorizer.GetGlyphColor(glyph);

                    if ((color != prevColor || glyph.Character == '\t' || glyph.Character == ' ') && TextRun.Length != 0)
                    {
                        Vector2 newOffset = new(textScreenPos.X + bufferOffset.X, textScreenPos.Y + bufferOffset.Y);
                        drawList.AddText(newOffset, prevColor, TextRun);

                        var textSize = ImGui.CalcTextSize(TextRun).X;
                        bufferOffset.X += textSize;
                        TextRun = "";
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
                            TextRun += line[i++].Character;
                        }
                    }

                    ++columnNo;
                }

                if (TextRun.Count() != 0)
                {
                    Vector2 newOffset = new(textScreenPos.X + bufferOffset.X, textScreenPos.Y + bufferOffset.Y);
                    drawList.AddText(newOffset, prevColor, TextRun);
                    TextRun = "";
                }

                ++lineNo;
            }
        }


        ImGui.Dummy(new Vector2(longest + 2, Buffer.GetLines().Count * LineHeight));
    }

    public void Start()
    {
        IsRendering = true;
    }

    public void End()
    {
        IsRendering = false;
    }

    public int GetPageSize()
    {
        return (int)Math.Floor((ImGui.GetWindowHeight() - 20.0f) / LineHeight);
    }
}
