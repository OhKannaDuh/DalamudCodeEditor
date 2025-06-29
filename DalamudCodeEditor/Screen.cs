using System.Numerics;
using DalamudCodeEditor.TextEditor;
using ImGuiNET;

namespace DalamudCodeEditor;

public static class Screen
{
    public static Coordinate ScreenPositionToCoordinates(Vector2 position, Editor editor)
    {
        var origin = ImGui.GetCursorScreenPos();
        var local = position - origin;

        var line = Math.Max(0, (int)Math.Floor(local.Y / editor.Renderer.LineHeight));
        var column = 0;

        var gutter = editor.Renderer.GutterWidth;
        var spaceWidth = ImGui.CalcTextSize(" ").X;

        if (line >= editor.Buffer.LineCount)
        {
            return new Coordinate(line, column).Sanitized(editor);
        }

        var glyphs = editor.Buffer.GetLine(line);
        var xOffset = 0.0f;
        var glyphIndex = 0;

        while (glyphIndex < glyphs.Count)
        {
            float charWidth;
            var c = glyphs[glyphIndex].Character;

            if (c == '\t')
            {
                var tabWidth = editor.Style.TabSize * spaceWidth;
                var nextTabStop = (float)Math.Floor((xOffset + 0.5f) / tabWidth + 1) * tabWidth;
                charWidth = nextTabStop - xOffset;

                if (gutter + xOffset + charWidth * 0.5f > local.X)
                {
                    break;
                }

                xOffset = nextTabStop;
                column = column / editor.Style.TabSize * editor.Style.TabSize + editor.Style.TabSize;
                glyphIndex++;
            }
            else
            {
                var len = Utf8Helper.UTF8CharLength(c);

                // Defensive bounds check
                if (glyphIndex + len > glyphs.Count)
                {
                    len = glyphs.Count - glyphIndex;
                }

                var slice = glyphs.Skip(glyphIndex).Take(len).Select(g => g.Character).ToArray();
                string utf8Char = new(slice);

                charWidth = ImGui.CalcTextSize(utf8Char).X;

                if (gutter + xOffset + charWidth * 0.5f > local.X)
                {
                    break;
                }

                xOffset += charWidth;

                // Instead of column++, use display width if available
                column += GlyphHelper.GetGlyphDisplayWidth(utf8Char, editor.Style.TabSize);

                glyphIndex += len;
            }
        }

        return new Coordinate(line, column).Sanitized(editor);
    }
}
