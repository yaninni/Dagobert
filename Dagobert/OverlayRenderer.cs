using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Numerics;
using static ECommons.GenericHelpers;
using Dagobert.Utilities;

namespace Dagobert
{
    internal static class OverlayRenderer
    {
        public static unsafe void DrawRetainerListOverlay(bool isBusy, Action onStart, Action onCancel)
        {
            if (!TryGetAddonByName<AtkUnitBase>(UIConsts.AddonRetainerList, out var addon) || !IsAddonReady(addon)) return;
            if (addon->UldManager.NodeListCount <= UIConsts.NodeId_RetainerList_PinchButtonAnchor) return;

            var node = addon->UldManager.NodeList[UIConsts.NodeId_RetainerList_PinchButtonAnchor];
            if (node == null) return;

            using (var style = new ImGuiNodeStyle(node))
            {
                if (isBusy)
                {
                    if (ImGui.Button("Cancel Operation")) onCancel();
                }
                else
                {
                    if (ImGui.Button("Auto Pinch All")) onStart();
                }
            }
        }
        public static unsafe void DrawSellListOverlay(
            bool isBusy,
            int currentItem,
            int totalItems,
            Action onPinch,
            Action onUnlistInventory,
            Action onUnlistRetainer,
            Action onCancel)
        {
            if (!TryGetAddonByName<AtkUnitBase>(UIConsts.AddonRetainerSellList, out var addon) || !IsAddonReady(addon)) return;
            if (addon->UldManager.NodeListCount <= UIConsts.NodeId_SellList_PinchButtonAnchor) return;

            var node = addon->UldManager.NodeList[UIConsts.NodeId_SellList_PinchButtonAnchor];
            if (node == null) return;

            using (var style = new ImGuiNodeStyle(node))
            {
                if (isBusy)
                {
                    if (totalItems > 0)
                    {
                        float p = (float)currentItem / totalItems;
                        ImGui.ProgressBar(p, new Vector2(100, 0), $"{currentItem}/{totalItems}");
                        ImGui.SameLine();
                    }
                    if (ImGui.Button("Cancel")) onCancel();
                }
                else
                {
                    if (ImGui.Button("Auto Pinch")) onPinch();

                    if (ImGui.Button("Unlist (Inv)")) onUnlistInventory();
                    ImGuiUtils.Tooltip("Return all listed items to Inventory");

                    ImGui.SameLine();

                    if (ImGui.Button("Unlist (Ret)")) onUnlistRetainer();
                    ImGuiUtils.Tooltip("Return all listed items to Retainer Inventory");
                }
            }
        }
        private unsafe struct ImGuiNodeStyle : IDisposable
        {
            private readonly float _oldScale;

            public ImGuiNodeStyle(AtkResNode* node)
            {
                var pos = GetNodePosition(node);
                var scale = GetNodeScale(node);

                ImGuiHelpers.ForceNextWindowMainViewport();
                ImGuiHelpers.SetNextWindowPosRelativeMainViewport(pos);

                ImGui.PushStyleColor(ImGuiCol.WindowBg, 0);
                _oldScale = ImGui.GetFont().Scale;
                ImGui.GetFont().Scale *= scale.X;
                ImGui.PushFont(ImGui.GetFont());

                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(3f, 3f) * scale.X);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(node->Width, node->Height) * scale);

                ImGui.Begin($"###AutoPinchOverlay_{node->NodeId}",
                    ImGuiWindowFlags.NoDecoration |
                    ImGuiWindowFlags.NoScrollbar |
                    ImGuiWindowFlags.AlwaysAutoResize |
                    ImGuiWindowFlags.NoSavedSettings |
                    ImGuiWindowFlags.NoNavFocus |
                    ImGuiWindowFlags.NoFocusOnAppearing);
            }

            public void Dispose()
            {
                ImGui.End();
                ImGui.PopStyleVar(5);
                ImGui.GetFont().Scale = _oldScale;
                ImGui.PopFont();
                ImGui.PopStyleColor();
            }

            private static Vector2 GetNodePosition(AtkResNode* n)
            {
                var p = new Vector2(n->X, n->Y);
                for (var pN = n->ParentNode; pN != null; pN = pN->ParentNode)
                {
                    p *= new Vector2(pN->ScaleX, pN->ScaleY);
                    p += new Vector2(pN->X, pN->Y);
                }
                return p;
            }

            private static Vector2 GetNodeScale(AtkResNode* n)
            {
                var s = new Vector2(n->ScaleX, n->ScaleY);
                for (var pN = n->ParentNode; pN != null; pN = pN->ParentNode)
                    s *= new Vector2(pN->ScaleX, pN->ScaleY);
                return s;
            }
        }
    }
}