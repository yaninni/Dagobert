using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;

namespace Dagobert.Utilities;

public static class ItemUtils
{
    private static readonly Dictionary<string, uint> ItemCache = new(StringComparer.OrdinalIgnoreCase);
    private static bool _cacheInitialized = false;

    public static void EnsureCache()
    {
        if (_cacheInitialized) return;
        
        var itemSheet = Svc.Data.GetExcelSheet<Item>();
        if (itemSheet == null) return;
        
        foreach (var item in itemSheet)
        {
            var name = item.Name.ToString();
            if (string.IsNullOrEmpty(name)) continue;
            ItemCache.TryAdd(name, item.RowId);
        }
        _cacheInitialized = true;
    }

    public static uint GetItemIdByName(string name)
    {
        EnsureCache();
        if (ItemCache.TryGetValue(name, out var id))
            return id;
        return 0;
    }

    public static string GetItemName(uint itemId, string fallback = "Unknown Item")
    {
        if (itemId == 0) return fallback;
        
        try
        {
            var row = Svc.Data.GetExcelSheet<Item>()?.GetRow(itemId);
            if (row?.RowId > 0)
                return row.Value.Name.ToString();
        }
        catch { }
        
        return fallback;
    }

    public static string GetGarlandToolsLink(uint itemId) => $"https://www.garlandtools.org/db/#item/{itemId}";

    public static void OpenGarlandTools(uint itemId)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = GetGarlandToolsLink(itemId),
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "Could not open Garland Tools link");
        }
    }

    public static void ToggleIgnore(uint itemId, string itemName, bool setIgnore)
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

    public static void ClearCache()
    {
        ItemCache.Clear();
        _cacheInitialized = false;
    }
}
