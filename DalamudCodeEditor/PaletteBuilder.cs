using System.Numerics;
using ImGuiNET;

namespace DalamudCodeEditor;

public static class PaletteBuilder
{
    public static uint[] GetDarkPalette()
    {
        var entries = new List<PaletteEntry>
        {
            // === Syntax Elements ===
            Entry(PaletteIndex.Default, 0.75f, 0.75f, 0.75f), // Light gray
            Entry(PaletteIndex.Keyword, 0.86f, 0.58f, 0.98f), // Purple
            Entry(PaletteIndex.Number, 0.98f, 0.73f, 0.38f), // Orange
            Entry(PaletteIndex.String, 0.65f, 0.85f, 0.60f), // Light green
            Entry(PaletteIndex.Function, 0.53f, 0.78f, 0.98f), // Light blue
            Entry(PaletteIndex.Punctuation, 0.75f, 0.75f, 0.75f), // Light gray
            Entry(PaletteIndex.Comment, 0.50f, 0.56f, 0.60f), // Gray-blue

            // === Editor UI ===
            Entry(PaletteIndex.Background, 0.122f, 0.122f, 0.122f), // You liked this
            Entry(PaletteIndex.Cursor, 1.0f, 1.0f, 1.0f), // White
            Entry(PaletteIndex.Selection, 0.2f, 0.4f, 0.8f, 0.35f), // Blue-gray selection
            Entry(PaletteIndex.LineNumber, 0.55f, 0.55f, 0.60f), // Subtle gray
            Entry(PaletteIndex.CurrentLineFill, 0.2f, 0.2f, 0.25f, 0.35f),
            Entry(PaletteIndex.CurrentLineFillInactive, 0.2f, 0.2f, 0.25f, 0.15f),
            Entry(PaletteIndex.CurrentLineEdge, 0.4f, 0.4f, 0.5f, 0.25f),

            // === Reserved ===
            Entry(PaletteIndex.Max, 1f, 0f, 0f, 1f),
        };

        var palette = new uint[Enum.GetValues(typeof(PaletteIndex)).Length];
        foreach (var entry in entries)
        {
            palette[(int)entry.Index] = entry.Color;
        }

        return palette;
    }

    public static uint[] GetUnhighlightedDarkPalette()
    {
        var entries = new List<PaletteEntry>
        {
            // === Syntax Elements (uniform text color) ===
            Entry(PaletteIndex.Default, 0xffb0b3b8), // Softer light gray
            Entry(PaletteIndex.Keyword, 0xffb0b3b8),
            Entry(PaletteIndex.Number, 0xffb0b3b8),
            Entry(PaletteIndex.String, 0xffb0b3b8),
            Entry(PaletteIndex.Function, 0xffb0b3b8),
            Entry(PaletteIndex.Punctuation, 0xffb0b3b8),
            Entry(PaletteIndex.Comment, 0xff5c6370),

            // === Editor UI (matched to GetDarkPalette) ===
            Entry(PaletteIndex.Background, 0xff1f1f1f), // #1F1F1F
            Entry(PaletteIndex.Cursor, 0xffffffff), // White
            Entry(PaletteIndex.Selection, 0x592f4a9e), // Slightly muted bluish selection
            Entry(PaletteIndex.LineNumber, 0xff8c8c8c), // Soft gray
            Entry(PaletteIndex.CurrentLineFill, 0x4022222a),
            Entry(PaletteIndex.CurrentLineFillInactive, 0x201c1c22),
            Entry(PaletteIndex.CurrentLineEdge, 0x403c4048),

            // === Reserved ===
            Entry(PaletteIndex.Max, 0xffb0b3b8),
        };

        var palette = new uint[Enum.GetValues(typeof(PaletteIndex)).Length];
        foreach (var entry in entries)
        {
            palette[(int)entry.Index] = entry.Color;
        }

        return palette;
    }

    // === Entry helpers ===

    public static PaletteEntry Entry(PaletteIndex index, uint rgba)
    {
        return new PaletteEntry(index, rgba);
    }

    public static PaletteEntry Entry(PaletteIndex index, Vector4 color)
    {
        return new PaletteEntry(index, ToColor(color));
    }

    public static PaletteEntry Entry(PaletteIndex index, float r, float g, float b)
    {
        return new PaletteEntry(index, ToColor(new Vector4(r, g, b, 1.0f)));
    }

    public static PaletteEntry Entry(PaletteIndex index, float r, float g, float b, float a)
    {
        return new PaletteEntry(index, ToColor(new Vector4(r, g, b, a)));
    }

    public static uint ToColor(Vector4 color)
    {
        return ImGui.GetColorU32(color);
    }

    public class PaletteEntry(PaletteIndex index, uint color)
    {
        public PaletteIndex Index { get; } = index;

        public uint Color { get; } = color;
    }
}
