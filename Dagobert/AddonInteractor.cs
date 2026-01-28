using System;
using ECommons.Automation;
using ECommons.DalamudServices;
using System.Runtime.CompilerServices;
using ECommons.UIHelpers.AddonMasterImplementations;
using ECommons.UIHelpers.AtkReaderImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Generic;
using static ECommons.GenericHelpers;
using static ECommons.UIHelpers.AtkReaderImplementations.ReaderContextMenu;
using Dagobert.Windows;
using Dagobert.Utilities;

namespace Dagobert
{
    internal static unsafe class AddonInteractor
    {
        private static bool? True() => true;
        private static bool? False() => false;

        public static bool? FireCallback(string addonName, bool postSetup, params object[] args)
        {
            if (TryGetAddonByName<AtkUnitBase>(addonName, out var addon) && IsAddonReady(addon))
            {
                if (!addon->IsVisible) 
                {
                    Svc.Log.Debug($"FireCallback: {addonName} not visible");
                    return false;
                }
                
                // Validate addon state
                if (addon->RootNode == null)
                {
                    Svc.Log.Warning($"FireCallback: {addonName} has null RootNode");
                    return false;
                }
                
                VisualMonitor.LogActivity(ActivityType.Pulse, $"UI Interaction: {addonName}");
                Callback.Fire(addon, postSetup, args);
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWindowVisible(string addonName)
        {
            if (TryGetAddonByName<AtkUnitBase>(addonName, out var addon) && IsAddonReady(addon))
            {
                return addon->IsVisible;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWindowVisible(void* addonPtr)
        {
            if (addonPtr == null) return false;
            var addon = (AtkUnitBase*)addonPtr;
            return IsAddonReady(addon) && addon->IsVisible;
        }

        public static void CloseWindow(string addonName)
        {
            if (TryGetAddonByName<AtkUnitBase>(addonName, out var addon) && IsAddonReady(addon))
            {
                VisualMonitor.LogActivity(ActivityType.Info, $"Closing {addonName}");
                addon->Close(true);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool? SelectRetainer(int index)
        {
            return FireCallback(UIConsts.AddonRetainerList, true, 2, index);
        }

        public static bool? ClickSellFromInventory()
        {
            if (TryGetAddonByName<AtkUnitBase>(UIConsts.AddonSelectString, out var addon) && IsAddonReady(addon))
            {
                if (!addon->IsVisible) return false;
                new AddonMaster.SelectString(addon).Entries[UIConsts.SelectString_SellInventoryOptionIndex].Select();
                return true;
            }
            return false;
        }

        public static string GetRetainerNameFromSellList()
        {
            if (TryGetAddonByName<AtkUnitBase>(UIConsts.AddonRetainerSellList, out var addon) && IsAddonReady(addon))
            {
                if (addon->UldManager.NodeListCount > UIConsts.NodeId_SellList_RetainerNameHeader)
                {
                    var node = addon->UldManager.NodeList[UIConsts.NodeId_SellList_RetainerNameHeader];
                    if (node != null && node->Type == NodeType.Text)
                    {
                        var text = ((AtkTextNode*)node)->NodeText.ToString();
                        if (string.IsNullOrEmpty(text)) return "Unknown";
                        
                        ReadOnlySpan<char> span = text.AsSpan();
                        int quoteIdx = span.IndexOf('\'');
                        if (quoteIdx != -1)
                        {
                            span = span.Slice(0, quoteIdx);
                        }
                        return span.Trim().ToString();
                    }
                }
            }
            return "Unknown";
        }

        public static int GetSellListCount()
        {
            if (TryGetAddonByName<AtkUnitBase>(UIConsts.AddonRetainerSellList, out var addon) && IsAddonReady(addon))
            {
                if (addon->UldManager.NodeListCount > UIConsts.NodeId_SellList_ItemList)
                {
                    var listNode = (AtkComponentNode*)addon->UldManager.NodeList[UIConsts.NodeId_SellList_ItemList];
                    if (listNode != null && listNode->Component != null)
                    {
                        try
                        {
                            return ((AtkComponentList*)listNode->Component)->ListLength;
                        }
                        catch
                        {
                            Svc.Log.Warning("GetSellListCount: Failed to read list length");
                            return 0;
                        }
                    }
                }
            }
            return 0;
        }

        public static bool? OpenContextMenuForSellItem(int index)
        {
            return FireCallback(UIConsts.AddonRetainerSellList, true, 0, index, 1);
        }

        public static List<ContextMenuEntry> GetContextMenuEntries()
        {
            if (TryGetAddonByName<AtkUnitBase>(UIConsts.AddonContextMenu, out var addon) && IsAddonReady(addon))
            {
                return new ReaderContextMenu(addon).Entries;
            }
            return new List<ContextMenuEntry>();
        }

        public static bool? ExecuteContextMenuEntry(int index)
        {
            return FireCallback(UIConsts.AddonContextMenu, true, 0, index, 0, 0, 0);
        }
        public static bool? OpenMarketBoardComparison()
        {
            // Callback: 4 (Compare Prices)
            return FireCallback(UIConsts.AddonRetainerSell, true, 4);
        }
        public static bool? InspectBaitItem(int index)
        {
            return FireCallback(UIConsts.AddonItemSearchResult, true, 0, index, 2);
        }

        public static (string ItemName, int Quantity, int? AskingPrice) GetRetainerSellWindowData()
        {
            if (TryGetAddonByName<AddonRetainerSell>(UIConsts.AddonRetainerSell, out var addon) && IsAddonReady(&addon->AtkUnitBase))
            {
                if (!addon->AtkUnitBase.IsVisible) return ("", 0, null);

                var item = addon->ItemName->NodeText.ToString();
                var qty = (int)addon->Quantity->Value;
                var price = (int)addon->AskingPrice->Value;
                return (item, qty, price);
            }
            return ("", 0, null);
        }
        public static bool SetRetainerSellPrice(int price)
        {
            if (TryGetAddonByName<AddonRetainerSell>(UIConsts.AddonRetainerSell, out var addon) && IsAddonReady(&addon->AtkUnitBase))
            {
                addon->AskingPrice->SetValue(price);
                return true;
            }
            return false;
        }
        public static bool? ConfirmRetainerSellPrice(bool success)
        {
            return FireCallback(UIConsts.AddonRetainerSell, true, success ? 0 : 1);
        }

        public static void SkipTalk()
        {
            if (TryGetAddonByName<AtkUnitBase>(UIConsts.AddonTalk, out var addon) && IsAddonReady(addon))
            {
                if (addon->IsVisible) new AddonMaster.Talk(addon).Click();
            }
        }
    }
}