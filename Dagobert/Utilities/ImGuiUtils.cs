using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Dagobert.Utilities;

public static class ImGuiUtils
{
    public static void DrawPriceRow(string label, int price, Vector4 color)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text(label);
        ImGui.TableNextColumn();
        ImGui.TextColored(color, $"{price:N0} gil");
    }

    public static void DrawPriceRowWithWorld(string label, int price, string worldName, Vector4 color)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text(label);
        ImGui.TableNextColumn();
        ImGui.TextColored(color, $"{price:N0} gil");
        ImGui.TableNextColumn();
        ImGui.TextDisabled(worldName);
    }

    public static void DrawStatCard(string title, Action content)
    {
        ImGui.TextColored(new Vector4(1, 1, 0, 1), $"[ {title} ]");
        ImGui.Indent();
        content();
        ImGui.Unindent();
        ImGui.Spacing();
    }

    public static void DrawStat(string label, string value)
    {
        ImGui.TextDisabled($"{label}:");
        ImGui.SameLine();
        ImGui.Text(value);
    }

    public static void DrawStatCard(string label, long val, Vector4 color)
    {
        ImGui.BeginGroup();
        ImGui.TextColored(color, label);
        ImGui.SetWindowFontScale(1.5f);
        ImGui.Text($"{val:N0}");
        ImGui.SetWindowFontScale(1.0f);
        ImGui.EndGroup();
    }

    public static void Tooltip(string s)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.SetTooltip(s);
            ImGui.EndTooltip();
        }
    }
}
