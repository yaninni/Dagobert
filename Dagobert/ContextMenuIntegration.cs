using Dalamud.Game.Gui.ContextMenu;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System;
using Dagobert.Utilities;

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

            if (args.Target is MenuTargetInventory invTarget)
            {
                itemId = invTarget.TargetItem?.ItemId ?? 0;
            }

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

            if (itemData.Name.ToString() == "") return;

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
                OnClicked = _ => ItemUtils.ToggleIgnore(itemId, itemData.Name.ToString(), !isIgnored)
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

        private void OpenConfigurationForItem(uint itemId)
        {
            Plugin.Instance.ConfigWindow.OpenForItem((int)itemId);
        }
    }
}