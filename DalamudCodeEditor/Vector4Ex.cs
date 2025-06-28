﻿using System.Numerics;
using ImGuiNET;

namespace DalamudCodeEditor;

public static class Vector4Ex
{
    public static uint GetU32(this Vector4 vector)
    {
        return ImGui.GetColorU32(vector);
    }
}
