using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using ECommons.UIHelpers.AddonMasterImplementations;
using static ECommons.GenericHelpers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using Dagobert.Utilities;

namespace Dagobert.Windows;

public sealed class ConfigWindow : Window
{
    private float[] _cachedGraph = Array.Empty<float>();
    private int _lastCount = -1;

    private int _newItemId = 0;
    private int _newItemMinPrice = 0;
    private bool _newItemIgnore = false;
    private bool _newItemMatchOnly = false;
    private string _targetItemName = "";
    private string _searchFilter = string.Empty;
    private bool _focusLogicTab = false;
    
    private List<uint> _cachedFilteredKeys = new();
    private string _cachedFilterText = string.Empty;

    public ConfigWindow() : base("Dagobert Configuration") { }
    public void OpenForItem(int itemId)
    {
        IsOpen = true;
        _focusLogicTab = true;
        LoadItemIntoEditor(itemId);
    }

    private void LoadItemIntoEditor(int itemId)
    {
        if (itemId < 0) itemId = 0;

        _newItemId = itemId;

        if (Plugin.Configuration.ItemConfigs.TryGetValue((uint)itemId, out var settings))
        {
            _newItemMinPrice = settings.MinPrice;
            _newItemIgnore = settings.Ignore;
            _newItemMatchOnly = settings.MatchOnly;
        }
        else
        {
            _newItemMinPrice = 0;
            _newItemIgnore = false;
            _newItemMatchOnly = false;
        }

        _targetItemName = GetItemName((uint)itemId);
    }

    private string GetItemName(uint itemId) => ItemUtils.GetItemName(itemId, "Unknown / None");

    private void ClearEditor()
    {
        _newItemId = 0;
        _newItemMinPrice = 0;
        _newItemIgnore = false;
        _newItemMatchOnly = false;
        _targetItemName = "";
    }

    public override void Draw()
    {
        if (_focusLogicTab)
        {
            _focusLogicTab = false;
        }

        if (ImGui.BeginTabBar("Tabs"))
        {
            if (ImGui.BeginTabItem("Configuration")) { DrawConfig(); ImGui.EndTabItem(); }

            if (ImGui.BeginTabItem("Advanced Logic")) { DrawLogic(); ImGui.EndTabItem(); }

            if (ImGui.BeginTabItem("Statistics")) { DrawStats(); ImGui.EndTabItem(); }

            if (ImGui.BeginTabItem("Activity Monitor")) { VisualMonitor.Draw(); ImGui.EndTabItem(); }
            ImGui.EndTabBar();
        }
    }

    private void DrawLogic()
    {
        var cfg = Plugin.Configuration;
        var sheet = Svc.Data.GetExcelSheet<Item>();

        ImGui.TextColored(new Vector4(0, 1, 1, 1), "Advanced Automation Logic");

        DrawCheck("Enable Advanced Humanization", () => cfg.EnableAdvancedHumanization, v => cfg.EnableAdvancedHumanization = v, "Enables cognitive states, reaction timing, and focus variation.");
        if (cfg.EnableAdvancedHumanization)
        {
            ImGui.Indent();
            DrawCheck("Simulate Fitts's Law (Cursor Travel)", () => cfg.EnableFittsLaw, v => cfg.EnableFittsLaw = v);
            DrawCheck("Simulate Misclicks", () => cfg.EnableMisclicks, v => cfg.EnableMisclicks = v, "2% chance to click wrong item and correct it.");
            DrawCheck("Comparative Doubt (Inspect Bait)", () => cfg.EnableComparativeDoubt, v => cfg.EnableComparativeDoubt = v, "Inspects suspicious low prices before undercutting.");
            ImGui.Unindent();
        }

        DrawCheck("Enable Fatigue System", () => cfg.EnableFatigue, v => cfg.EnableFatigue = v, "Increases delay slowly as more items are processed.");
        DrawCheck("Mouse Entropy Check", () => cfg.MouseEntropyCheck, v => cfg.MouseEntropyCheck = v, "Aborts automation if you move the mouse.");

        ImGui.Separator();

        DrawCheck("Smart Outlier Protection", () => cfg.SmartOutlierProtection, v => cfg.SmartOutlierProtection = v, "Ignores listings that are >50% cheaper than the next.");
        DrawCheck("Match Stack Size", () => cfg.MatchStackSize, v => cfg.MatchStackSize = v, "Undercuts only listings with similar stack sizes.");

        ImGui.Separator();

        ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), "Item Rules Editor");
        ImGui.BeginGroup();
        {
            if (_newItemId > 0 && string.IsNullOrEmpty(_targetItemName))
            {
                _targetItemName = GetItemName((uint)_newItemId);
            }

            float inputWidth = Math.Max(150, ImGui.GetContentRegionAvail().X * 0.3f);
            
            ImGui.SetNextItemWidth(inputWidth);
            if (ImGui.InputInt("Item ID", ref _newItemId))
            {
                if (_newItemId < 0) _newItemId = 0;
                _targetItemName = GetItemName((uint)_newItemId);
            }

            ImGui.SameLine();
            if (!string.IsNullOrEmpty(_targetItemName))
            {
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(new Vector4(0, 1, 0, 1), $"Found: {_targetItemName}");
            }
            else
            {
                ImGui.AlignTextToFramePadding();
                ImGui.TextDisabled("(Select item from list or enter ID)");
            }

            ImGui.SetNextItemWidth(inputWidth);
            if (ImGui.InputInt("Min Price (Cost Basis)", ref _newItemMinPrice))
            {
                if (_newItemMinPrice < 0) _newItemMinPrice = 0;
            }
            ImGui.SameLine();
            Dagobert.Utilities.ImGuiUtils.Tooltip("Dagobert will never list lower than this price.");

            ImGui.Checkbox("Ignore Item", ref _newItemIgnore);
            ImGui.SameLine();
            ImGui.Checkbox("Match Only (No Undercut)", ref _newItemMatchOnly);

            if (ImGui.Button("Add / Update Item Rule"))
            {
                if (_newItemId > 0)
                {
                    if (!cfg.ItemConfigs.ContainsKey((uint)_newItemId))
                        cfg.ItemConfigs[(uint)_newItemId] = new ItemSettings();

                    var s = cfg.ItemConfigs[(uint)_newItemId];
                    s.MinPrice = _newItemMinPrice;
                    s.Ignore = _newItemIgnore;
                    s.MatchOnly = _newItemMatchOnly;

                    cfg.Save();
                    ClearEditor();
                }
            }

            ImGui.SameLine();
            if (ImGui.Button("Clear Form"))
            {
                ClearEditor();
            }
        }
        ImGui.EndGroup();

        ImGui.Separator();
        
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Search List:");
        ImGui.SameLine();
        ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X - 60);
        ImGui.InputText("##Search", ref _searchFilter, 100);
        ImGui.PopItemWidth();
        ImGui.SameLine();
        if (ImGui.Button("Clear")) _searchFilter = string.Empty;

        try
        {
            float tableHeight = ImGui.GetContentRegionAvail().Y - 10;
            if (ImGui.BeginTable("ItemRules", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable, new Vector2(0, tableHeight)))
            {
                ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 50);
                ImGui.TableSetupColumn("Item Name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Min Price", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupColumn("Ignore", ImGuiTableColumnFlags.WidthFixed, 50);
                ImGui.TableSetupColumn("Match", ImGuiTableColumnFlags.WidthFixed, 50);
                ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableHeadersRow();

                var keys = cfg.ItemConfigs.Keys.ToList();

                if (!string.IsNullOrWhiteSpace(_searchFilter))
                {
                    if (_cachedFilterText != _searchFilter || _cachedFilteredKeys.Count == 0)
                    {
                        _cachedFilterText = _searchFilter;
                        _cachedFilteredKeys = keys.Where(k =>
                            k.ToString().Contains(_searchFilter) ||
                            GetItemName(k).Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)
                        ).ToList();
                    }
                    keys = _cachedFilteredKeys;
                }
                else
                {
                    _cachedFilteredKeys.Clear();
                    _cachedFilterText = string.Empty;
                }

                foreach (var id in keys)
                {
                    if (!cfg.ItemConfigs.TryGetValue(id, out var settings)) continue;

                    string itemName = GetItemName(id);

                    ImGui.TableNextRow();

                    if (id == _newItemId)
                        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(ImGuiCol.Header));

                    ImGui.TableNextColumn();
                    ImGui.Text(id.ToString());

                    ImGui.TableNextColumn();
                    ImGui.Text(itemName);
                    if (ImGui.BeginPopupContextItem($"gt_{id}"))
                    {
                        if (ImGui.MenuItem("Open in Garland Tools"))
                        {
                            try
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = ItemUtils.GetGarlandToolsLink(id),
                                    UseShellExecute = true
                                });
                            }
                            catch (Exception ex)
                            {
                                Svc.Log.Error(ex, "Could not open Garland Tools link");
                            }
                        }

                        if (ImGui.MenuItem("Inspect"))
                        {
                            Plugin.Instance.ItemInspector.Inspect(id);
                        }
                        ImGui.EndPopup();
                    }

                    ImGui.TableNextColumn();
                    ImGui.PushItemWidth(-1);
                    int p = settings.MinPrice;

                    if (ImGui.InputInt($"##mp{id}", ref p, 0))
                    {
                        if (p < 0) p = 0;
                        settings.MinPrice = p;
                        cfg.Save();
                    }
                    ImGui.PopItemWidth();

                    ImGui.TableNextColumn();
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (ImGui.GetColumnWidth() - 20) / 2);
                    bool ign = settings.Ignore;
                    if (ImGui.Checkbox($"##ign{id}", ref ign)) { settings.Ignore = ign; cfg.Save(); }

                    ImGui.TableNextColumn();
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (ImGui.GetColumnWidth() - 20) / 2);
                    bool mo = settings.MatchOnly;
                    if (ImGui.Checkbox($"##mo{id}", ref mo)) { settings.MatchOnly = mo; cfg.Save(); }

                    ImGui.TableNextColumn();

                    if (ImGuiComponents.IconButton((int)id, FontAwesomeIcon.Pen))
                    {
                        LoadItemIntoEditor((int)id);
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Edit in top form");

                    ImGui.SameLine();

                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0, 0, 1));
                    if (ImGuiComponents.IconButton((int)id + 100000, FontAwesomeIcon.Trash))
                    {
                        cfg.ItemConfigs.Remove(id);
                        cfg.Save();
                        if (_newItemId == id) ClearEditor();
                    }
                    ImGui.PopStyleColor();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Delete Rule");
                }
                ImGui.EndTable();
            }
        }
        catch (Exception ex)
        {
            ImGui.TextColored(new Vector4(1, 0, 0, 1), $"Error drawing list: {ex.Message}");
        }
    }

    private void DrawStats()
    {
        Plugin.Configuration.Initialize();
        var s = Plugin.Configuration.Stats;
        
        ImGuiUtils.DrawStatCard("Total Gil Earned", s.TotalGilEarned, new Vector4(0, 1, 0, 1));
        ImGui.SameLine(0, 60);
        ImGuiUtils.DrawStatCard("Items Sold", s.TotalItemsSold, new Vector4(0, 0.8f, 1, 1));
        ImGui.SameLine(0, 60);
        ImGuiUtils.DrawStatCard("Undercuts Made", s.TotalUndercutsMade, new Vector4(1, 0.5f, 0, 1));

        ImGui.Separator();

        if (ImGui.CollapsingHeader("Advanced Statistics", ImGuiTreeNodeFlags.DefaultOpen))
        {
            DrawAdvancedStatistics();
        }

        ImGui.Separator();

        if (s.SalesHistory.Count > 1)
        {
            if (_lastCount != s.SalesHistory.Count) { _cachedGraph = s.SalesHistory.Select(x => (float)x.Price).ToArray(); _lastCount = s.SalesHistory.Count; }
            if (_cachedGraph.Length > 0)
                ImGui.PlotLines("##SalesGraph", _cachedGraph.AsSpan(), 0, $"Last {_cachedGraph.Length} Sales", 0f, _cachedGraph.Max() * 1.1f, new Vector2(ImGui.GetContentRegionAvail().X, 100));
        }
        else ImGui.TextDisabled("Not enough data for graph.");
    }

    private void DrawAdvancedStatistics()
    {
        var avgSaleTime = StatisticsAnalyzer.GetOverallAverageSaleTime();
        if (avgSaleTime > 0)
        {
            ImGui.TextColored(new Vector4(0.4f, 0.8f, 1, 1), "Average Sale Time:");
            ImGui.SameLine();
            ImGui.Text($"{avgSaleTime:F1} hours");
        }

        var trend = StatisticsAnalyzer.GetSalesTrend(7);
        if (trend.PeriodSales > 0)
        {
            ImGui.TextColored(new Vector4(0.4f, 0.8f, 1, 1), "7-Day Trend:");
            ImGui.SameLine();
            var trendColor = trend.IsPositiveTrend ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0.3f, 0.3f, 1);
            var trendIcon = trend.IsPositiveTrend ? "▲" : "▼";
            ImGui.TextColored(trendColor, $"{trendIcon} {trend.RevenueChangePercent:F1}% ({trend.PeriodSales} sales)");
        }

        if (ImGui.CollapsingHeader("Top Selling Items"))
        {
            var bestSellers = StatisticsAnalyzer.GetBestSellingItems(5);
            if (bestSellers.Any())
            {
                if (ImGui.BeginTable("BestSellers", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                {
                    ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Sold", ImGuiTableColumnFlags.WidthFixed, 50);
                    ImGui.TableSetupColumn("Avg Price", ImGuiTableColumnFlags.WidthFixed, 80);
                    ImGui.TableSetupColumn("Revenue", ImGuiTableColumnFlags.WidthFixed, 100);
                    ImGui.TableHeadersRow();

                    foreach (var item in bestSellers)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.Text(item.ItemName);
                        ImGui.TableNextColumn();
                        ImGui.TextColored(new Vector4(0, 1, 0, 1), item.TotalSold.ToString());
                        ImGui.TableNextColumn();
                        ImGui.Text($"{item.AveragePrice:N0}");
                        ImGui.TableNextColumn();
                        ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), $"{item.TotalRevenue:N0}");
                    }
                    ImGui.EndTable();
                }
            }
            else
            {
                ImGui.TextDisabled("No sales data available yet.");
            }
        }

        if (ImGui.CollapsingHeader("Worst Performing Items"))
        {
            var worstPerformers = StatisticsAnalyzer.GetWorstPerformingItems(5);
            if (worstPerformers.Any())
            {
                if (ImGui.BeginTable("WorstPerformers", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                {
                    ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Sold", ImGuiTableColumnFlags.WidthFixed, 50);
                    ImGui.TableSetupColumn("Avg Sale Time", ImGuiTableColumnFlags.WidthFixed, 100);
                    ImGui.TableSetupColumn("Last Sold", ImGuiTableColumnFlags.WidthFixed, 100);
                    ImGui.TableHeadersRow();

                    foreach (var item in worstPerformers)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.Text(item.ItemName);
                        ImGui.TableNextColumn();
                        ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 1), item.TotalSold.ToString());
                        ImGui.TableNextColumn();
                        if (item.AverageSaleTime > 0)
                            ImGui.Text($"{item.AverageSaleTime:F1}h");
                        else
                            ImGui.TextDisabled("N/A");
                        ImGui.TableNextColumn();
                        var daysAgo = (DateTime.Now - item.LastSoldDateTime).TotalDays;
                        ImGui.Text($"{daysAgo:F0}d ago");
                    }
                    ImGui.EndTable();
                }
            }
            else
            {
                ImGui.TextDisabled("No sales data available yet.");
            }
        }
    }


    private void DrawConfig()
    {
        if (ImGui.BeginTabBar("ConfigSections"))
        {
            if (ImGui.BeginTabItem("Pricing"))
            {
                DrawPricingSettings();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Timings"))
            {
                DrawTimingSettings();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Other"))
            {
                DrawNotificationSettings();
                DrawRetainerSelection();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawPricingSettings()
    {
        var cfg = Plugin.Configuration;
        ImGui.TextColored(new Vector4(0, 1, 1, 1), "Pricing Strategy");
        
        DrawCheck("Use HQ price", () => cfg.HQ, v => cfg.HQ = v, "Fails if no HQ price on MB");
        DrawEnumCombo("Pricing Personality", () => cfg.PricingPersonality, v => cfg.PricingPersonality = v);
        
        ImGui.Separator();
        
        DrawEnumCombo("Undercut Mode", () => cfg.UndercutMode, v =>
        {
            if (v == UndercutMode.Percentage && cfg.UndercutAmount >= 100) cfg.UndercutAmount = 1;
            cfg.UndercutMode = v;
        });

        ImGui.Indent();
        int amt = cfg.UndercutAmount;
        if (cfg.UndercutMode == UndercutMode.FixedAmount)
        {
            if (ImGui.InputInt("Undercut Amount (Gil)", ref amt))
            {
                cfg.UndercutAmount = Math.Max(1, amt);
                cfg.Save();
            }
        }
        else
        {
            if (ImGui.SliderInt("Undercut Percentage", ref amt, 1, 99, "%d%%"))
            {
                cfg.UndercutAmount = amt;
                cfg.Save();
            }
        }
        ImGui.Unindent();

        float maxCut = cfg.MaxUndercutPercentage;
        if (ImGui.SliderFloat("Max Price Drop %", ref maxCut, 0.1f, 99.9f, "%.1f%%"))
        {
            cfg.MaxUndercutPercentage = maxCut;
            cfg.Save();
        }
        Dagobert.Utilities.ImGuiUtils.Tooltip("If the price drops more than this % from the last check, ignore it.");

        DrawCheck("Undercut Self", () => cfg.UndercutSelf, v => cfg.UndercutSelf = v, "If you are already the lowest, undercut yourself again? (Usually No)");
    }

    private void DrawTimingSettings()
    {
        var cfg = Plugin.Configuration;
        ImGui.TextColored(new Vector4(1, 1, 0, 1), "Delays & Humanization");

        DrawEnumCombo("Delay Strategy", () => cfg.DelayStrategy, v => cfg.DelayStrategy = v);

        int pMin = cfg.GetMBPricesDelayMin;
        int pMax = cfg.GetMBPricesDelayMax;
        if (DrawRange("Price Check Delay (ms)", ref pMin, ref pMax))
        {
            cfg.GetMBPricesDelayMin = pMin;
            cfg.GetMBPricesDelayMax = pMax;
            cfg.Save();
        }

        int kMin = cfg.MarketBoardKeepOpenMin;
        int kMax = cfg.MarketBoardKeepOpenMax;
        if (DrawRange("Keep Window Open (ms)", ref kMin, ref kMax))
        {
            cfg.MarketBoardKeepOpenMin = kMin;
            cfg.MarketBoardKeepOpenMax = kMax;
            cfg.Save();
        }
    }

    private void DrawNotificationSettings()
    {
        var cfg = Plugin.Configuration;
        
        ImGui.TextColored(new Vector4(1, 0, 1, 1), "Chat & Discord");

        DrawCheck("Show Errors in Chat", () => cfg.ShowErrorsInChat, v => cfg.ShowErrorsInChat = v);
        DrawCheck("Show Price Adjustments", () => cfg.ShowPriceAdjustmentsMessages, v => cfg.ShowPriceAdjustmentsMessages = v);
        DrawCheck("Show Retainer Names", () => cfg.ShowRetainerNames, v => cfg.ShowRetainerNames = v);
        
        ImGui.Separator();

        DrawCheck("Enable Discord Logging", () => cfg.EnableDiscordLogging, v => cfg.EnableDiscordLogging = v);
        if (cfg.EnableDiscordLogging)
        {
            ImGui.Indent();
            string url = cfg.DiscordWebhookUrl;
            if (ImGui.InputText("Webhook URL", ref url, 512, ImGuiInputTextFlags.Password))
            {
                cfg.DiscordWebhookUrl = url;
                cfg.Save();
            }
            DrawCheck("Stop on Tell (Whisper)", () => cfg.StopOnTell, v => cfg.StopOnTell = v, "Pauses automation if you receive a /tell");
            ImGui.Unindent();
        }
    }

    private void DrawCheck(string label, Func<bool> get, Action<bool> set, string? tooltip = null) { bool v = get(); if (ImGui.Checkbox(label, ref v)) { set(v); Plugin.Configuration.Save(); } if (tooltip != null) Dagobert.Utilities.ImGuiUtils.Tooltip(tooltip); }
    private void DrawEnumCombo<T>(string label, Func<T> get, Action<T> set) where T : struct, Enum { if (ImGui.BeginCombo(label, get().ToString())) { foreach (var e in Enum.GetValues<T>()) if (ImGui.Selectable(e.ToString(), e.Equals(get()))) { set(e); Plugin.Configuration.Save(); } ImGui.EndCombo(); } }
    private bool DrawRange(string label, ref int min, ref int max) { ImGui.BeginGroup(); ImGui.Text(label); ImGui.PushItemWidth(100); bool changed = ImGui.DragInt($"Min##{label}", ref min, 10, 100, 10000); ImGui.SameLine(); changed |= ImGui.DragInt($"Max##{label}", ref max, 10, 100, 10000); ImGui.PopItemWidth(); if (changed) { min = Math.Max(100, min); max = Math.Max(min, max); } ImGui.EndGroup(); return changed; }
    private unsafe void DrawRetainerSelection() { ImGui.Text("Retainer Selection"); string[] names = Plugin.Configuration.LastKnownRetainerNames.ToArray(); if (TryGetAddonByName<AtkUnitBase>("RetainerList", out var addon) && IsAddonReady(addon)) { try { var liveNames = new AddonMaster.RetainerList(addon).Retainers.Select(r => r.Name).ToArray(); if (!new HashSet<string>(liveNames).SetEquals(names)) { Plugin.Configuration.LastKnownRetainerNames = [.. liveNames]; Plugin.Configuration.EnabledRetainerNames.RemoveWhere(n => !liveNames.Contains(n) && n != Configuration.ALL_DISABLED_SENTINEL); Plugin.Configuration.Save(); names = liveNames; } } catch { } } if (names.Length == 0) { ImGui.TextColored(new Vector4(1, 1, 0, 1), "Open Retainer List to populate."); return; } var enabled = Plugin.Configuration.EnabledRetainerNames; for (int i = 0; i < names.Length; i++) { string n = names[i]; bool isAllDisabled = enabled.Contains(Configuration.ALL_DISABLED_SENTINEL); bool isEnabled = !isAllDisabled && (enabled.Count == 0 || enabled.Contains(n)); if (ImGui.Checkbox($"{n}##{i}", ref isEnabled)) { enabled.Remove(Configuration.ALL_DISABLED_SENTINEL); if (isEnabled) { enabled.Add(n); if (enabled.Count == names.Length) enabled.Clear(); } else { if (enabled.Count == 0) foreach (var x in names) if (x != n) enabled.Add(x); else enabled.Remove(n); if (enabled.Count == 0) enabled.Add(Configuration.ALL_DISABLED_SENTINEL); } Plugin.Configuration.Save(); } if (i % 2 == 0 && i < names.Length - 1) ImGui.SameLine(0, 150); } }
}