using ImGuiNET;

namespace DalamudCodeEditor.TextEditor;

public partial class Editor
{
    public readonly uint[] mPalette = [];

    public uint[] mPaletteBase = [];

    public uint[] Palette
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
}
