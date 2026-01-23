using Dalamud.Bindings.ImGui;

namespace Dagobert
{
    /// <summary>
    /// Constants for Game UI Addons, Node IDs, and Callback Indices.
    /// </summary>
    public static class UIConsts
    {
        public const string AddonRetainerList = "RetainerList";
        public const string AddonRetainerSellList = "RetainerSellList";
        public const string AddonSelectString = "SelectString";
        public const string AddonRetainerSell = "RetainerSell";
        public const string AddonItemSearchResult = "ItemSearchResult";
        public const string AddonContextMenu = "ContextMenu";
        public const string AddonTalk = "Talk";
        public const int NodeId_RetainerList_PinchButtonAnchor = 27;
        public const int NodeId_SellList_PinchButtonAnchor = 17;
        public const int NodeId_SellList_ItemList = 10;
        public const int NodeId_SellList_RetainerNameHeader = 2;
        public const int SelectString_SellInventoryOptionIndex = 2;
        public const int ContextMenu_AdjustPrice_Callback = 0;
    }

    public static class Utils
    {

    }
    public static class ImGuiHelper
    {
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
}