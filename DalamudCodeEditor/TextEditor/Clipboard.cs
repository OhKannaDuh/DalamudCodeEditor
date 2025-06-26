using System.Text;
using ImGuiNET;

namespace DalamudCodeEditor.TextEditor;

public class Clipboard(Editor editor) : EditorComponent(editor)
{
    public void Copy()
    {
        if (Selection.HasSelection)
        {
            ImGui.SetClipboardText(Selection.Text);
        }
        else
        {
            if (Buffer.GetLines().Count != 0)
            {
                var line = Buffer.GetLines()[Cursor.GetPosition().Line];
                var str = new StringBuilder();
                foreach (var g in line)
                {
                    str.Append(g.Character);
                }

                ImGui.SetClipboardText(str.ToString());
            }
        }
    }

    public void Cut()
    {
        if (editor.IsReadOnly)
        {
            Copy();
            return;
        }

        if (Selection.HasSelection)
        {
            var u = new UndoRecord
            {
                Before = State.Clone(),
                Removed = Selection.Text,
                RemovedStart = State.SelectionStart,
                RemovedEnd = State.SelectionEnd,
            };

            Copy();
            editor.DeleteSelection();

            u.After = State.Clone();
            UndoManager.AddUndo(u);
        }
    }

    public void Paste()
    {
        if (editor.IsReadOnly)
        {
            return;
        }

        var clipText = ImGui.GetClipboardText();
        if (!string.IsNullOrEmpty(clipText))
        {
            var u = new UndoRecord
            {
                Before = State.Clone(),
                Added = clipText,
                AddedStart = Cursor.GetPosition(),
            };

            if (Selection.HasSelection)
            {
                u.Removed = Selection.Text;
                u.RemovedStart = State.SelectionStart;
                u.RemovedEnd = State.SelectionEnd;
                editor.DeleteSelection();
            }

            editor.InsertText(clipText);
            u.AddedEnd = Cursor.GetPosition();
            u.After = State.Clone();
            UndoManager.AddUndo(u);
        }
    }
}
