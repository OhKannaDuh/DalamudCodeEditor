using ImGuiNET;

namespace DalamudCodeEditor.TextEditor;

public class Style(Editor editor) : EditorComponent(editor)
{
    public int TabSize { get; private set; } = 4;

    public bool DrawBorder { get; private set; } = false;

    public ImGuiWindowFlags EditorFlags { get; private set; } =
        ImGuiWindowFlags.HorizontalScrollbar | ImGuiWindowFlags.AlwaysHorizontalScrollbar | ImGuiWindowFlags.NoMove;

    public bool ShowLineNumbers { get; private set; } = true;

    public bool ShowWhitespace { get; private set; } = true;
    
    public float LineSpacing { get; private set; } = 1;

    public void ToggleLineNumbers()
    {
        ShowLineNumbers ^= true;
    }

    public void ToggleWhitespace()
    {
        ShowWhitespace ^= true;
    }
}
