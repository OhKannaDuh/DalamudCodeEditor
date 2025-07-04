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
        var xOffset = 0f;
        var glyphIndex = 0;

        while (glyphIndex < glyphs.Count)
        {
            var glyph = glyphs[glyphIndex];
            float charWidth;

            if (glyph.Rune.Value == '\t')
            {
                var tabWidth = editor.Style.TabSize * spaceWidth;
                var nextTabStop = (float)Math.Floor((xOffset + 0.5f) / tabWidth + 1) * tabWidth;
                charWidth = nextTabStop - xOffset;

                if (gutter + xOffset + charWidth * 0.5f > local.X)
                {
                    break;
                }

                xOffset = nextTabStop;

                // Snap column to next tab stop
                column = (column / editor.Style.TabSize + 1) * editor.Style.TabSize;

                glyphIndex++;
            }
            else
            {
                charWidth = ImGui.CalcTextSize(glyph.Rune.ToString()).X;

                if (gutter + xOffset + charWidth * 0.5f > local.X)
                {
                    break;
                }

                xOffset += charWidth;

                column += GlyphHelper.GetGlyphDisplayWidth(glyph, editor.Style.TabSize);

                glyphIndex++;
            }
        }

        return new Coordinate(line, column).Sanitized(editor);
    }
}
