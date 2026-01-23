using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System;

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
            if (args.Target is not MenuTargetInventory inventoryTarget) return;
            var itemId = inventoryTarget.TargetItem?.ItemId ?? 0;
            if (itemId == 0) return;

            if (itemId > 1_000_000)
                itemId -= 1_000_000;

            Item itemData;
            try
            {
                itemData = Svc.Data.GetExcelSheet<Item>().GetRow(itemId);
                if (itemData.RowId == 0) return;
            }
            catch (Exception)
            {
                return;
            }

            if (itemData.ItemSearchCategory.RowId == 0) return;

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
                OnClicked = _ => ToggleIgnore(itemId, itemData.Name.ToString(), !isIgnored)
            });

            args.AddMenuItem(new MenuItem
            {
                Name = "Configure...",
                PrefixChar = 'D',
                PrefixColor = 561,
                OnClicked = _ => OpenConfigurationForItem(itemId)
            });
        }

        private void ToggleIgnore(uint itemId, string itemName, bool setIgnore)
        {
            if (!Plugin.Configuration.ItemConfigs.ContainsKey(itemId))
            {
                Plugin.Configuration.ItemConfigs[itemId] = new ItemSettings();
            }

            Plugin.Configuration.ItemConfigs[itemId].Ignore = setIgnore;
            Plugin.Configuration.Save();

            var builder = new SeStringBuilder()
                .AddUiForeground("[Dagobert] ", 45)
                .AddItemLink(itemId, false)
                .AddText(setIgnore ? " is now ignored." : " is no longer ignored.");

            Svc.Chat.Print(builder.Build());
        }

        private void OpenConfigurationForItem(uint itemId)
        {
            Plugin.Instance.ConfigWindow.OpenForItem((int)itemId);
        }
    }
}