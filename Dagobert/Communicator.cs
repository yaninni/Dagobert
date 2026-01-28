using System;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.DalamudServices;
using Dagobert.Utilities;

namespace Dagobert;

public static class Communicator
{
    public static string GetGarlandToolsLink(uint itemId) => ItemUtils.GetGarlandToolsLink(itemId);

    public static uint GetItemIdByName(string name) => ItemUtils.GetItemIdByName(name);

    public static string GetCleanItemName(string rawName) => StringUtils.GetCleanItemName(rawName);

    public static void PrintPriceUpdate(string itemName, int? oldPrice, int? newPrice, float cutPercentage)
    {
        if (Plugin.Configuration.ShowPriceAdjustmentsMessages && oldPrice != null && newPrice != null && oldPrice.Value != newPrice.Value)
        {
            var dec = oldPrice.Value > newPrice.Value ? "cut" : "increase";
            var itemPayload = RawItemNameToItemPayload(itemName);

            if (itemPayload != null)
            {
                var seString = new SeStringBuilder()
                    .AddItemLink(itemPayload.ItemId, itemPayload.IsHQ)
                    .AddText($": Pinching from {oldPrice.Value:N0} to {newPrice.Value:N0} gil, a {dec} of {MathF.Abs(MathF.Round(cutPercentage, 2))}%")
                    .Build();
                Svc.Chat.Print(seString);
            }
            else
                Svc.Chat.Print($"{StringUtils.GetCleanItemName(itemName)}: Pinching from {oldPrice.Value:N0} to {newPrice.Value:N0} gil, a {dec} of {MathF.Abs(MathF.Round(cutPercentage, 2))}%");
        }
    }

    private static ItemPayload? RawItemNameToItemPayload(string itemName)
    {
        try
        {
            ItemUtils.EnsureCache();
            var cleanedName = StringUtils.GetCleanItemName(itemName);
            var isHq = StringUtils.ContainsHqIcon(itemName);
            var id = ItemUtils.GetItemIdByName(cleanedName);

            if (id > 0)
            {
                return new ItemPayload(id, isHq);
            }
        }
        catch { }
        return null;
    }

    public static void PrintAboveMaxCutError(string itemName)
    {
        if (Plugin.Configuration.ShowErrorsInChat)
        {
            var itemPayload = RawItemNameToItemPayload(itemName);
            if (itemPayload != null)
            {
                var seString = new SeStringBuilder()
                    .AddItemLink(itemPayload.ItemId, itemPayload.IsHQ)
                    .AddText($": Item ignored because it would cut the price by more than {Plugin.Configuration.MaxUndercutPercentage}%")
                    .Build();
                Svc.Chat.PrintError(seString);
            }
            else Svc.Chat.PrintError($"{StringUtils.GetCleanItemName(itemName)}: Item ignored because it would cut the price by more than {Plugin.Configuration.MaxUndercutPercentage}%");
        }
    }

    public static void PrintRetainerName(string name)
    {
        if (Plugin.Configuration.ShowRetainerNames)
        {
            var seString = new SeStringBuilder()
                .AddText("Now Pinching items of retainer: ")
                .AddUiForeground(name, 561)
                .Build();
            Svc.Chat.Print(seString);
        }
    }

    public static void PrintNoPriceToSetError(string itemName)
    {
        if (Plugin.Configuration.ShowErrorsInChat)
        {
            var itemPayload = RawItemNameToItemPayload(itemName);
            if (itemPayload != null)
            {
                var seString = new SeStringBuilder()
                    .AddItemLink(itemPayload.ItemId, itemPayload.IsHQ)
                    .AddText($": No price to set, please set price manually")
                    .Build();
                Svc.Chat.PrintError(seString);
            }
            else Svc.Chat.PrintError($"{StringUtils.GetCleanItemName(itemName)}: No price to set, please set price manually");
        }
    }

    public static void PrintAllRetainersDisabled()
    {
        var seString = new SeStringBuilder()
            .AddText("All retainers are disabled. Open configuration with ")
            .Add(Plugin.ConfigLinkPayload)
            .AddUiForeground("/dagobert", 31)
            .Build();
        Svc.Chat.PrintError(seString);
    }
}