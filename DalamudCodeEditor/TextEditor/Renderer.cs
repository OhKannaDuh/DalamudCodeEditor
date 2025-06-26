using System.Numerics;
using ImGuiNET;

namespace DalamudCodeEditor.TextEditor;

public class Renderer(Editor editor) : EditorComponent(editor)
{
    public bool IsRendering { get; private set; } = false;

    public float GutterWidth { get; private set; } = 20f;

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

    public readonly LineRenderer LineRenderer = new(editor);

    public void SetGutterWidth(float width)
    {
        GutterWidth = width;
    }

    public void Render()
    {
        // IsRendering = true;
        // Pallete.Update();

        // var drawList = ImGui.GetWindowDrawList();
        // var cursorScreenPos = ImGui.GetCursorScreenPos();
        // var scrollX = ImGui.GetScrollX();
        // var scrollY = ImGui.GetScrollY();
        //
        // var visibleLineStart = (int)Math.Floor(scrollY / LineHeight);
        // var visibleLineCount = (int)Math.Floor(ImGui.GetWindowContentRegionMax().Y / LineHeight);
        // var visibleLineEnd = visibleLineStart + visibleLineCount;
        //
        // var totalLines = Buffer.LineCount;
        // var clampedLineEnd = Math.Min(totalLines - 1, visibleLineEnd);
        // var lineMax = Math.Max(0, clampedLineEnd);
        //
        // var buffer = Style.ShowLineNumbers ? " " + totalLines + " " : "";
        // var textStart = ImGui.CalcTextSize(buffer).X;
        //
        // if (totalLines <= 0)
        // {
        //     Dummy();
        // }


        // var longest = mTextStart;
    }

    private void Dummy()
    {
        var longest = Buffer.LongestLine * CharacterWidth;
        ImGui.Dummy(new Vector2(longest + 2, Buffer.LineCount * LineHeight));
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
