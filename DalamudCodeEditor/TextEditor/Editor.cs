using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;

namespace DalamudCodeEditor.TextEditor;

public partial class Editor
{
    // Components
    public readonly TextBuffer Buffer;

    public readonly Style Style;

    public readonly Renderer Renderer;

    public readonly Colorizer Colorizer;

    public readonly UndoManager UndoManager;

    public readonly InputManager InputManager;

    public readonly Cursor Cursor;

    public readonly Scroll Scroll;

    public readonly Selection Selection;

    public readonly Clipboard Clipboard;

    public State State;

    // Properties
    public bool IsReadOnly { get; private set; }

    public Editor()
    {
        Buffer = new TextBuffer(this);
        Style = new Style(this);
        Renderer = new Renderer(this);
        Colorizer = new Colorizer(this);
        UndoManager = new UndoManager(this);
        InputManager = new InputManager(this);
        Cursor = new Cursor(this);
        Scroll = new Scroll(this);
        Selection = new Selection(this);
        Clipboard = new Clipboard(this);
        State = new State(this);

        InputManager.Keyboard.InitializeKeyboardBindings();
        Language = new LuaLanguageDefinition();

        mPalette = new uint[(int)PaletteIndex.Max];
        Palette = PaletteBuilder.GetDarkPalette();
    }

    public void SetText(string text)
    {
        Buffer.SetText(text);

        ColorizeInternal();
    }

    public string GetText()
    {
        return Buffer.GetText();
    }

    public void Draw(string title, Vector2 size = new())
    {
        Renderer.Start();
        Buffer.MarkClean();
        Cursor.MarkClean();


        // var paletteColor = mPalette[(int)PaletteIndex.Background];
        // // @todo: Check if this can just handle the raw uint
        // using var color = ImRaii.PushColor(ImGuiCol.ChildBg, ImGui.ColorConvertU32ToFloat4(paletteColor));

        // @todo: change mPallette cringe
        using var color = ImRaii.PushColor(ImGuiCol.ChildBg, mPalette[(int)PaletteIndex.Background]);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        using var child = ImRaii.Child(title, size, Style.DrawBorder, Style.EditorFlags);

        // Handle inputs within the child window
        HandleKeyboardInputs(); // InputManager.Keyboard.HandleInput();
        InputManager.Mouse.HandleInput();
        ColorizeInternal();

        Render(); // Renderer.Render();

        Scroll.ScrollToCursor();
        Renderer.End();
    }
}
