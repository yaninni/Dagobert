using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.DalamudServices;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Dagobert;

public static class Communicator
{
    private static readonly ExcelSheet<Item> ItemSheet = Svc.Data.GetExcelSheet<Item>();
    private static readonly Regex CleanNameRegex = new Regex(@"[^\p{L}\p{N}\p{P}\p{S}\s]+", RegexOptions.Compiled);

    public static string GetCleanItemName(string rawName)
    {
        if (string.IsNullOrEmpty(rawName)) return "Unknown Item";
        try
        {
            string working = rawName.Replace("\uE03C", " (HQ)");
            string clean = CleanNameRegex.Replace(working, "").Trim();
            if (string.IsNullOrWhiteSpace(clean))
                clean = new string(working.Where(c => !char.IsControl(c)).ToArray()).Trim();
            return clean;
        }
        catch { return "Unknown Item"; }
    }

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
                Svc.Chat.Print($"{itemName}: Pinching from {oldPrice.Value:N0} to {newPrice.Value:N0}, a {dec} of {MathF.Abs(MathF.Round(cutPercentage, 2))}%");
        }
    }

    private static ItemPayload? RawItemNameToItemPayload(string itemName)
    {
        try
        {
            var seString = SeString.Parse(Encoding.UTF8.GetBytes(itemName));
            var textPayloads = seString.Payloads.OfType<TextPayload>().ToList();
            if (textPayloads.Count == 0) return null;

            var cleanedName = "";
            var isHq = false;

            if (textPayloads.Count == 1) cleanedName = textPayloads[0].Text?.Trim();
            else if (textPayloads.Count >= 2)
            {
                var nameParts = new StringBuilder();
                for (int i = 1; i < textPayloads.Count; i++)
                {
                    var text = textPayloads[i].Text;
                    if (i == 1 && text?.Length >= 2 && text[1] == '\u0003') text = text[2..];
                    nameParts.Append(text);
                }
                cleanedName = nameParts.ToString();
                if (cleanedName.Length >= 1 && cleanedName[^1] == '\uE03C')
                {
                    isHq = true;
                    cleanedName = cleanedName[..^1].TrimEnd();
                }
                else cleanedName = cleanedName.TrimEnd();
            }

            var item = ItemSheet.FirstOrDefault(i => i.Name.ToString().Equals(cleanedName, StringComparison.OrdinalIgnoreCase));
            if (item.RowId > 0) return new ItemPayload(item.RowId, isHq);
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
            else Svc.Chat.PrintError($"{itemName}: Item ignored because it would cut the price by more than {Plugin.Configuration.MaxUndercutPercentage}%");
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
            else Svc.Chat.PrintError($"{itemName}: No price to set, please set price manually");
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