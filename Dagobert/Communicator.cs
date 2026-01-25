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
            var se = SeString.Parse(Encoding.UTF8.GetBytes(rawName));
            var sb = new StringBuilder();
            foreach (var p in se.Payloads) if (p is TextPayload tp) sb.Append(tp.Text);
            
            string clean = sb.Length > 0 ? sb.ToString() : rawName;
            
            clean = clean.Replace("\uE03C", "").Replace("\uE03B", "");
            
            clean = new string(clean.Where(c => !char.IsControl(c)).ToArray());
            
            int startIdx = 0;
            while (startIdx < clean.Length)
            {
                char c = clean[startIdx];
                if (char.IsLetterOrDigit(c) || c == '(' || c == '[' || c == '{' || c == '<') break;
                if (c == '%' || c == '&' || char.IsWhiteSpace(c))
                {
                    startIdx++;
                    continue;
                }
                break;
            }
            
            if (startIdx >= clean.Length) return clean.Trim();
            return clean.Substring(startIdx).Trim();
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
                Svc.Chat.Print($"{GetCleanItemName(itemName)}: Pinching from {oldPrice.Value:N0} to {newPrice.Value:N0} gil, a {dec} of {MathF.Abs(MathF.Round(cutPercentage, 2))}%");
        }
    }

    private static ItemPayload? RawItemNameToItemPayload(string itemName)
    {
        try
        {
            var cleanedName = GetCleanItemName(itemName);
            var isHq = itemName.Contains('\uE03C');

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
            else Svc.Chat.PrintError($"{GetCleanItemName(itemName)}: Item ignored because it would cut the price by more than {Plugin.Configuration.MaxUndercutPercentage}%");
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
            else Svc.Chat.PrintError($"{GetCleanItemName(itemName)}: No price to set, please set price manually");
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