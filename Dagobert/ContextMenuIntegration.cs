using Dalamud.Game.Gui.ContextMenu;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System;
using System.Diagnostics.CodeAnalysis;
using Dagobert.Utilities;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace Dagobert
{
    public sealed class ContextMenuIntegration : IDisposable
    {
        public ContextMenuIntegration()
        {
            if (Plugin.ContextMenu != null)
            {
                Plugin.ContextMenu.OnMenuOpened += OnMenuOpened;
            }
        }

        public void Dispose()
        {
            if (Plugin.ContextMenu != null)
            {
                Plugin.ContextMenu.OnMenuOpened -= OnMenuOpened;
            }
        }

        private void OnMenuOpened(IMenuOpenedArgs args)
        {
            uint itemId = 0;

            if (args.MenuType == ContextMenuType.Inventory)
            {
                itemId = (args.Target as MenuTargetInventory)?.TargetItem?.BaseItemId ?? 0u;
            }
            else
            {
                itemId = GetItemIdFromAgent(args.AddonName);

                if (itemId == 0u)
                {
                    Svc.Log.Debug("Failed to get item ID from agent {0}", args.AddonName ?? "null");
                    itemId = (uint)Svc.GameGui.HoveredItem % 500000;
                }
            }

            if (itemId == 0u)
            {
                Svc.Log.Debug("Failed to get item ID for context menu in {0}", args.AddonName ?? "null");
                return;
            }

            var item = Svc.Data.GetExcelSheet<Item>().GetRowOrDefault(itemId);

            if (!item.HasValue)
            {
                Svc.Log.Debug("Failed to get item data for item ID {0}", itemId);
                return;
            }

            bool isIgnored = false;
            if (Plugin.Configuration.ItemConfigs.TryGetValue(itemId, out var settings))
            {
                isIgnored = settings.Ignore;
            }

            string ignoreLabel = isIgnored ? "Unignore Item" : "Ignore Item";

            args.AddMenuItem(new MenuItem
            {
                Name = ignoreLabel,
                PrefixChar = 'D',
                PrefixColor = 561,
                OnClicked = _ => ItemUtils.ToggleIgnore(itemId, item.Value.Name.ToString(), !isIgnored)
            });

            args.AddMenuItem(new MenuItem
            {
                Name = "Configure...",
                PrefixChar = 'D',
                PrefixColor = 561,
                OnClicked = _ => OpenConfigurationForItem(itemId)
            });

            args.AddMenuItem(new MenuItem
            {
                Name = "Inspect",
                PrefixChar = 'D',
                PrefixColor = 561,
                OnClicked = _ => Plugin.Instance.ItemInspector.Inspect(itemId)
            });

            args.AddMenuItem(new MenuItem
            {
                Name = "Open in Garland Tools",
                PrefixChar = 'D',
                PrefixColor = 561,
                OnClicked = _ => ItemUtils.OpenGarlandTools(itemId)
            });
        }

        private unsafe uint GetItemIdFromAgent(string? addonName)
        {
            if (string.IsNullOrEmpty(addonName))
                return 0u;

            var itemId = addonName switch
            {
                "ChatLog" => AgentChatLog.Instance()->ContextItemId,
                "GatheringNote" => *(uint*)((nint)AgentGatheringNote.Instance() + 0xA0),
                "GrandCompanySupplyList" => *(uint*)((nint)AgentGrandCompanySupply.Instance() + 0x54),
                "ItemSearch" => (uint)AgentContext.Instance()->UpdateCheckerParam,
                "RecipeNote" => AgentRecipeNote.Instance()->ContextMenuResultItemId,

                _ => 0u,
            };

            return itemId % 500000;
        }

        private void OpenConfigurationForItem(uint itemId)
        {
            Plugin.Instance.ConfigWindow.OpenForItem((int)itemId);
        }
    }
}
