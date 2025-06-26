using ImGuiNET;
using System.Numerics;

namespace DalamudCodeEditor.TextEditor;

public class LineRenderer(Editor editor)
{
    // @todo: Bring this inline and prevent its direct use
    public string TextRun = string.Empty;

    private uint currentColor;

    public void Render(ImDrawListPtr drawList, List<Glyph> glyphs, Vector2 position)
    {
        TextRun = string.Empty;
        currentColor = GetColor(glyphs.FirstOrDefault());

        for (var i = 0; i < glyphs.Count;)
        {
            var glyph = glyphs[i];
            var color = GetColor(glyph);

            if (ShouldFlush(glyph, color))
            {
                DrawRun(drawList, position, TextRun, currentColor);
                TextRun = string.Empty;
            }

            if (glyph.Character != '\t' && glyph.Character != ' ')
            {
                var len = Utf8Helper.UTF8CharLength(glyph.Character);
                for (var j = 0; j < len && i < glyphs.Count; j++, i++)
                {
                    TextRun += glyphs[i].Character;
                }
            }
            else
            {
                DrawRun(drawList, position, TextRun, currentColor);
                TextRun = string.Empty;
                // Handle special rendering for whitespace here
                i++;
            }

            currentColor = color;
        }

        if (TextRun.Length > 0)
        {
            DrawRun(drawList, position, TextRun, currentColor);
        }
    }

    private uint GetColor(Glyph glyph)
    {
        // Simplified version — mirrors Editor.GetGlyphColor
        if (glyph.Comment)
        {
            return editor.Palette[(int)PaletteIndex.Comment];
        }

        if (glyph.MultiLineComment)
        {
            return editor.Palette[(int)PaletteIndex.MultiLineComment];
        }

        var color = editor.Palette[(int)glyph.ColorIndex];
        if (glyph.Preprocessor)
        {
            var pp = editor.Palette[(int)PaletteIndex.Preprocessor];
            return Blend(color, pp);
        }

        return color;
    }

    private bool ShouldFlush(Glyph glyph, uint nextColor)
    {
        return glyph.Character == '\t' || glyph.Character == ' ' || nextColor != currentColor;
    }

    private void DrawRun(ImDrawListPtr drawList, Vector2 position, string text, uint color)
    {
        if (!string.IsNullOrEmpty(text))
        {
            drawList.AddText(position, color, text);
            position.X += ImGui.CalcTextSize(text).X;
        }
    }

    private uint Blend(uint a, uint b)
    {
        int blend(int x, int y)
        {
            return (x + y) / 2;
        }

        return (uint)(
            blend((int)(a & 0xff), (int)(b & 0xff)) |
            blend((int)(a >> 8 & 0xff), (int)(b >> 8 & 0xff)) << 8 |
            blend((int)(a >> 16 & 0xff), (int)(b >> 16 & 0xff)) << 16 |
            blend((int)(a >> 24 & 0xff), (int)(b >> 24 & 0xff)) << 24
        );
    }
}
