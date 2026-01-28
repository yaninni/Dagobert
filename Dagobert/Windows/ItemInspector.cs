using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Lumina.Excel.Sheets;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Enums;
using Dagobert.Utilities;

namespace Dagobert.Windows;

public class ItemInspector : Window
{
    private uint _currentItemId;
    private Item? _localItem;
    private List<Recipe> _recipes = new();
    private List<GatheringPoint> _gatheringPoints = new();
    private List<uint> _gilShops = new();
    
    private bool _isLoading;
    private bool _isLoadingMarketData;
    private readonly Stack<uint> _backHistory = new();
    private readonly Stack<uint> _forwardHistory = new();
    
    // Search functionality
    private string _searchQuery = "";
    private List<(uint ItemId, string ItemName)> _searchResults = new();
    private bool _showSearchResults;
    
    // Universalis data
    private UniversalisMarketData? _marketData;
    private PriceSuggestion? _priceSuggestion;
    private string _currentWorld = "";
    private string _currentDatacenter = "";
    private List<CrossWorldListing>? _crossWorldListings;
    private bool _showCrossWorldListings;
    
    // Cross-DC data
    private CrossDcData? _crossDcData;
    private bool _isLoadingCrossDcData;
    private bool _showCrossDcListings;
    private bool _hasAttemptedCrossDcLoad;

    public ItemInspector() : base("Item Inspector (Local)", ImGuiWindowFlags.NoScrollbar)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(450, 550),
            MaximumSize = new Vector2(1200, 1000)
        };
    }

    public void Inspect(uint itemId, bool addToHistory = true)
    {
        if (_currentItemId == itemId && _localItem != null)
        {
            IsOpen = true;
            return;
        }

        if (addToHistory && _currentItemId != 0) _backHistory.Push(_currentItemId);
        if (addToHistory) _forwardHistory.Clear();

        _currentItemId = itemId;
        _isLoading = true;
        _marketData = null;
        _priceSuggestion = null;
        _crossWorldListings = null;
        _showCrossWorldListings = false;
        _crossDcData = null;
        _showCrossDcListings = false;
        _hasAttemptedCrossDcLoad = false;
        IsOpen = true;

        string worldName = "";
        try
        {
            var localPlayer = Svc.Objects.FirstOrDefault(o => o.ObjectKind == ObjectKind.Player) as IPlayerCharacter;
            if (localPlayer != null)
            {
                worldName = localPlayer.CurrentWorld.Value.Name.ToString();
                if (string.IsNullOrEmpty(worldName))
                {
                    worldName = localPlayer.HomeWorld.Value.Name.ToString();
                }
            }
        }
        catch { }
        _currentWorld = worldName;
        _currentDatacenter = UniversalisClient.GetDatacenterForWorld(worldName) ?? worldName;

        Task.Run(async () =>
        {
            try
            {
                _localItem = LuminaDataProvider.GetItem(itemId);
                _recipes = LuminaDataProvider.GetRecipesForItem(itemId);
                _gatheringPoints = LuminaDataProvider.GetGatheringPointsForItem(itemId);
                _gilShops = LuminaDataProvider.GetGilShopsForItem(itemId);
                _isLoading = false;
                
                await FetchMarketData(itemId);
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex, "Failed to load local data for item inspector.");
                _isLoading = false;
            }
        });
    }
    
    private async Task FetchMarketData(uint itemId)
    {
        _isLoadingMarketData = true;
        try
        {
            if (!string.IsNullOrEmpty(_currentWorld))
            {
                bool isHq = _localItem?.CanBeHq ?? false;
                _marketData = await UniversalisClient.GetMarketData(itemId, _currentWorld, false);
                
                if (_marketData != null)
                {
                    _priceSuggestion = UniversalisClient.GetPriceSuggestion(_marketData, false);
                    
                    var dcName = UniversalisClient.GetDatacenterForWorld(_currentWorld) ?? _currentWorld;
                    _crossWorldListings = await UniversalisClient.GetCrossWorldListings(itemId, dcName, 50);
                    
                }
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"Failed to fetch market data: {ex.Message}");
        }
        finally
        {
            _isLoadingMarketData = false;
        }
    }

    public override void Draw()
    {
        DrawCompactNavigation();
        
        HandleMouseNavigation();
        
        ImGui.Separator();

        if (_isLoading) { ImGui.Text("Indexing Local Data..."); return; }
        if (_localItem == null || _localItem.Value.RowId == 0) { ImGui.TextColored(new Vector4(1, 0, 0, 1), "Item not found in game data."); return; }
        
        var item = _localItem.Value;

        DrawPremiumHeader(item);
        ImGui.Separator();

        if (ImGui.BeginTabBar("InspectorTabs"))
        {
            if (ImGui.BeginTabItem("Overview")) { DrawOverviewTab(item); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Market Data")) { DrawMarketDataTab(); ImGui.EndTabItem(); }
            if (_gatheringPoints.Any() || _gilShops.Any())
            {
                if (ImGui.BeginTabItem("Acquisition")) { DrawAcquisitionTab(item); ImGui.EndTabItem(); }
            }
            if (_recipes.Any())
            {
                if (ImGui.BeginTabItem("Crafting")) { DrawCraftingTab(item); ImGui.EndTabItem(); }
            }
            ImGui.EndTabBar();
        }

        DrawFooter();
    }

    private void DrawCompactNavigation()
    {
        ImGui.BeginGroup();
        
        bool canGoBack = _backHistory.Count > 0;
        if (!canGoBack) ImGui.BeginDisabled();
        if (ImGuiComponents.IconButton("##back", FontAwesomeIcon.ArrowLeft))
        {
            NavigateBack();
        }
        if (!canGoBack) ImGui.EndDisabled();
        if (ImGui.IsItemHovered() && canGoBack) ImGui.SetTooltip("Go back (Mouse Button 4)");
        
        ImGui.SameLine();
        
        bool canGoForward = _forwardHistory.Count > 0;
        if (!canGoForward) ImGui.BeginDisabled();
        if (ImGuiComponents.IconButton("##forward", FontAwesomeIcon.ArrowRight))
        {
            NavigateForward();
        }
        if (!canGoForward) ImGui.EndDisabled();
        if (ImGui.IsItemHovered() && canGoForward) ImGui.SetTooltip("Go forward (Mouse Button 5)");
        
        ImGui.SameLine();
        
        ImGui.SetNextItemWidth(200);
        string previousQuery = _searchQuery;
        if (ImGui.InputTextWithHint("##itemSearch", "Search items...", ref _searchQuery, 100))
        {
            // Search on every keystroke
            if (_searchQuery != previousQuery)
            {
                if (string.IsNullOrWhiteSpace(_searchQuery))
                {
                    _showSearchResults = false;
                }
                else
                {
                    PerformItemSearch(_searchQuery);
                }
            }
        }
        
        if (_showSearchResults && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsItemHovered() && !ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows))
        {
            _showSearchResults = false;
        }
        
        if (_showSearchResults && _searchResults.Any())
        {
            ImGui.SetNextWindowPos(ImGui.GetItemRectMin() + new Vector2(0, ImGui.GetItemRectSize().Y));

            if (ImGui.Begin("##searchResults", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoNavInputs | ImGuiWindowFlags.NoNavFocus))
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), $"Results for '{_searchQuery}':");
                ImGui.Separator();
                
                foreach (var (itemId, itemName) in _searchResults.Take(10))
                {
                    if (ImGui.Selectable($"{itemName}##search_{itemId}"))
                    {
                        Inspect(itemId);
                        _showSearchResults = false;
                        _searchQuery = "";
                    }
                }
                
                ImGui.End();
            }
        }
        else if (_showSearchResults && string.IsNullOrWhiteSpace(_searchQuery))
        {
            _showSearchResults = false;
        }
        
        ImGui.SameLine();
        string currentItemDisplayName = _localItem?.Name.ToString() ?? "Loading...";
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), $"| {currentItemDisplayName}");
        
        ImGui.EndGroup();
    }
    
    private void PerformItemSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _showSearchResults = false;
            return;
        }
        
        _searchResults.Clear();
        var itemSheet = Svc.Data.GetExcelSheet<Item>();
        if (itemSheet == null) return;
        
        var searchLower = query.ToLowerInvariant();
        
        foreach (var item in itemSheet)
        {
            if (item.RowId == 0) continue;
            
            var name = item.Name.ToString();
            if (string.IsNullOrEmpty(name)) continue;
            
            if (name.ToLowerInvariant().Contains(searchLower))
            {
                _searchResults.Add((item.RowId, name));
            }
        }
        
        _searchResults = _searchResults
            .OrderByDescending(r => r.ItemName.Equals(query, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(r => r.ItemName.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .ThenBy(r => r.ItemName)
            .Take(20)
            .ToList();
        
        _showSearchResults = _searchResults.Any();
    }

    private void NavigateBack()
    {
        if (_backHistory.TryPop(out var prevId))
        {
            _forwardHistory.Push(_currentItemId);
            Inspect(prevId, false);
        }
    }

    private void NavigateForward()
    {
        if (_forwardHistory.TryPop(out var nextId))
        {
            _backHistory.Push(_currentItemId);
            Inspect(nextId, false);
        }
    }

    private void HandleMouseNavigation()
    {

        if (ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows))
        {
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Middle + 1))
            {
                NavigateBack();
            }
            else if (ImGui.IsMouseClicked(ImGuiMouseButton.Middle + 2))
            {
                NavigateForward();
            }
        }
    }

    private void DrawPremiumHeader(Item item)
    {
        var icon = Plugin.TextureProvider.GetFromGameIcon((uint)item.Icon);
        if (icon.TryGetWrap(out var wrap, out _))
        {
            ImGui.Image(wrap.Handle, new Vector2(48, 48));
            ImGui.SameLine(0, 15);
        }

        ImGui.BeginGroup();
        ImGui.SetWindowFontScale(1.3f);
        ImGui.TextColored(new Vector4(0.4f, 0.8f, 1, 1), item.Name.ToString());
        ImGui.SetWindowFontScale(1.0f);
        ImGui.TextDisabled($"Item Level {item.LevelItem.RowId} | ID: {item.RowId}");
        ImGui.EndGroup();
    }

    private void DrawOverviewTab(Item item)
    {
        if (ImGui.BeginChild("OverviewScroll"))
        {
            var desc = item.Description.ToString();
            if (!string.IsNullOrEmpty(desc))
            {
                ImGui.TextWrapped(desc);
                ImGui.Spacing();
            }

            ImGuiUtils.DrawStatCard("General Info", () =>
            {
                DrawStat("Stack Size", item.StackSize.ToString());
                DrawStat("Tradeable", item.IsUntradable ? "No" : "Yes");
                if (item.PriceMid > 0) DrawStat("Purchase Price", $"{item.PriceMid:N0} gil");
                if (item.PriceLow > 0) DrawStat("Vendor Value", $"{item.PriceLow:N0} gil");
            });

            var stats = new List<(string, int)>();
            for (int i = 0; i < item.BaseParam.Count; i++)
            {
                var param = item.BaseParam[i];
                if (param.RowId == 0) continue;
                stats.Add((param.Value.Name.ToString(), (int)item.BaseParamValue[i]));
            }

            if (stats.Any())
            {
                ImGuiUtils.DrawStatCard("Attributes & Stats", () =>
                {
                    foreach (var (label, val) in stats)
                        DrawStat(label, val.ToString());
                });
            }
            ImGui.EndChild();
        }
    }
    
    private void DrawMarketDataTab()
    {
        if (ImGui.BeginChild("MarketDataScroll"))
        {
            if (_isLoadingMarketData)
            {
                ImGui.Text("Loading market data from Universalis...");
                ImGui.EndChild();
                return;
            }
            
            if (_marketData == null)
            {
                ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), "No market data available.");
                if (string.IsNullOrEmpty(_currentWorld))
                {
                    ImGui.TextDisabled("Could not detect current world.");
                }
                ImGui.EndChild();
                return;
            }
            
            if (!_isLoadingMarketData)
            {
                if (ImGui.Button($"{FontAwesomeIcon.Sync.ToIconString()} Refresh Market Data"))
                {
                    _ = RefreshMarketDataAsync();
                }
                ImGui.Spacing();
            }
            
            if (_priceSuggestion != null && _priceSuggestion.HasData)
            {
                ImGui.BeginGroup();
                ImGui.TextColored(new Vector4(0, 1, 0.5f, 1), $"{FontAwesomeIcon.Lightbulb.ToIconString()} Price Suggestion");
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1, 1, 0, 1), _priceSuggestion.GetRecommendation());
                ImGui.EndGroup();
                ImGui.Spacing();
            }
            
            if (ImGui.CollapsingHeader($"Current World: {_currentWorld}", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (_priceSuggestion != null && _priceSuggestion.HasData)
                {
                    if (ImGui.BeginTable("CurrentWorldPrices", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                    {
                        ImGui.TableSetupColumn("Metric", ImGuiTableColumnFlags.WidthFixed, 120);
                        ImGui.TableSetupColumn("Price", ImGuiTableColumnFlags.WidthStretch);
                        
                        ImGuiUtils.DrawPriceRow("Minimum", _priceSuggestion.CurrentWorldMin, new Vector4(0.5f, 1, 0.5f, 1));
                        ImGuiUtils.DrawPriceRow("Average", _priceSuggestion.CurrentWorldAvg, new Vector4(1, 1, 1, 1));
                        ImGuiUtils.DrawPriceRow("Median", _priceSuggestion.CurrentWorldMedian, new Vector4(1, 1, 0.5f, 1));
                        ImGuiUtils.DrawPriceRow("Maximum", _priceSuggestion.CurrentWorldMax, new Vector4(1, 0.5f, 0.5f, 1));
                        
                        ImGui.EndTable();
                    }
                    
                    ImGui.TextDisabled($"Active Listings: {_priceSuggestion.CurrentWorldListingCount}");
                    if (_priceSuggestion.SalesPerDay > 0)
                    {
                        ImGui.TextDisabled($"Sales/Day: {_priceSuggestion.SalesPerDay:F1}");
                    }
                }
                else
                {
                    ImGui.TextDisabled("No listings found on current world.");
                }
            }
            
            if (ImGui.CollapsingHeader("Cross-World (Datacenter)"))
            {
                if (_crossWorldListings != null && _crossWorldListings.Any())
                {
                    var sortedByPrice = _crossWorldListings.OrderBy(l => l.PricePerUnit).ToList();
                    var minPrice = sortedByPrice.First().PricePerUnit;
                    var minWorld = sortedByPrice.First().WorldName;
                    var avgPrice = (int)sortedByPrice.Average(l => l.PricePerUnit);
                    var medianPrice = MathUtils.GetMedian(sortedByPrice.Select(l => l.PricePerUnit).ToList());
                    var maxPrice = sortedByPrice.Last().PricePerUnit;
                    var maxWorld = sortedByPrice.Last().WorldName;
                    
                    if (ImGui.BeginTable("CrossWorldPrices", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                    {
                        ImGui.TableSetupColumn("Metric", ImGuiTableColumnFlags.WidthFixed, 100);
                        ImGui.TableSetupColumn("Price", ImGuiTableColumnFlags.WidthFixed, 100);
                        ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthStretch);
                        
                        ImGuiUtils.DrawPriceRowWithWorld("Minimum", minPrice, minWorld, new Vector4(0.5f, 1, 0.5f, 1));
                        ImGuiUtils.DrawPriceRow("Average", avgPrice, new Vector4(1, 1, 1, 1));
                        ImGuiUtils.DrawPriceRow("Median", medianPrice, new Vector4(1, 1, 0.5f, 1));
                        ImGuiUtils.DrawPriceRowWithWorld("Maximum", maxPrice, maxWorld, new Vector4(1, 0.5f, 0.5f, 1));
                        
                        ImGui.EndTable();
                    }
                    
                    ImGui.TextDisabled($"Based on {_crossWorldListings.Count} listings across all worlds");
                }
                else if (_priceSuggestion != null && _priceSuggestion.HasData)
                {
                    if (ImGui.BeginTable("CrossWorldPrices", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                    {
                        ImGui.TableSetupColumn("Metric", ImGuiTableColumnFlags.WidthFixed, 120);
                        ImGui.TableSetupColumn("Price", ImGuiTableColumnFlags.WidthStretch);
                        
                        ImGuiUtils.DrawPriceRow("Minimum", _priceSuggestion.CrossWorldMin, new Vector4(0.5f, 1, 0.5f, 1));
                        ImGuiUtils.DrawPriceRow("Average", _priceSuggestion.CrossWorldAvg, new Vector4(1, 1, 1, 1));
                        ImGuiUtils.DrawPriceRow("Median", _priceSuggestion.CrossWorldMedian, new Vector4(1, 1, 0.5f, 1));
                        
                        ImGui.EndTable();
                    }
                }
                else
                {
                    ImGui.TextDisabled("No cross-world data available.");
                }
            }
            
            if (_marketData.Listings != null && _marketData.Listings.Any())
            {
                if (ImGui.CollapsingHeader("Current Listings"))
                {
                    if (ImGui.BeginTable("Listings", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(0, 250)))
                    {
                        ImGui.TableSetupColumn("Unit Price", ImGuiTableColumnFlags.WidthFixed, 80);
                        ImGui.TableSetupColumn("Qty", ImGuiTableColumnFlags.WidthFixed, 40);
                        ImGui.TableSetupColumn("Total", ImGuiTableColumnFlags.WidthFixed, 90);
                        ImGui.TableSetupColumn("w/ Tax", ImGuiTableColumnFlags.WidthFixed, 90);
                        ImGui.TableSetupColumn("HQ", ImGuiTableColumnFlags.WidthFixed, 35);
                        ImGui.TableSetupColumn("Retainer", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableHeadersRow();
                        
                        foreach (var listing in _marketData.Listings.Take(20))
                        {
                            ImGui.TableNextRow();
                            
                            var totalPrice = listing.PricePerUnit * listing.Quantity;
                            var priceWithTax = (int)(totalPrice / 0.95); // What you'd need to list to receive this amount
                            var taxAmount = priceWithTax - totalPrice;
                            
                            ImGui.TableNextColumn();
                            ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), $"{listing.PricePerUnit:N0}");
                            
                            ImGui.TableNextColumn();
                            ImGui.Text(listing.Quantity.ToString());
                            
                            ImGui.TableNextColumn();
                            ImGui.Text($"{totalPrice:N0}");
                            
                            ImGui.TableNextColumn();
                            ImGui.TextColored(new Vector4(1, 0.5f, 0.5f, 1), $"{priceWithTax:N0}");
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip($"You'd need to list at this price to RECEIVE {totalPrice:N0} gil after 5% tax ({taxAmount:N0} gil tax)");
                            }
                            
                            ImGui.TableNextColumn();
                            if (listing.Hq)
                            {
                                ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), "HQ");
                            }
                            
                            ImGui.TableNextColumn();
                            ImGui.Text(listing.RetainerName);
                        }
                        
                        ImGui.EndTable();
                    }
                }
            }
            
            if (_crossWorldListings != null && _crossWorldListings.Any())
            {
                var sortedByPrice = _crossWorldListings.OrderBy(l => l.PricePerUnit).ToList();
                var cheapestListing = sortedByPrice.First();
                var cheapestPrice = cheapestListing.PricePerUnit;
                
                var cheapestByWorld = sortedByPrice
                    .GroupBy(l => l.WorldName)
                    .Select(g => new { World = g.Key, MinPrice = g.Min(l => l.PricePerUnit), Count = g.Count() })
                    .OrderBy(x => x.MinPrice)
                    .ToList();
                
                var cheapestWorldName = cheapestByWorld.First().World;
                var isMyWorldCheapest = cheapestWorldName.Equals(_currentWorld, StringComparison.OrdinalIgnoreCase);
                
                string headerText = isMyWorldCheapest 
                    ? $"Cross-World Listings ({_crossWorldListings.Count}) - Your world is cheapest!"
                    : $"Cross-World Listings ({_crossWorldListings.Count}) - Cheapest: {cheapestWorldName} @ {cheapestPrice:N0} gil";
                
                if (ImGui.CollapsingHeader(headerText, ImGuiTreeNodeFlags.DefaultOpen))
                {
                    if (isMyWorldCheapest)
                    {
                        ImGui.TextColored(new Vector4(0, 1, 0.5f, 1), $"{FontAwesomeIcon.CheckCircle.ToIconString()} Your world ({_currentWorld}) has the cheapest listings!");
                    }
                    else
                    {
                        ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), $"{FontAwesomeIcon.ExclamationTriangle.ToIconString()} Cheapest on {cheapestWorldName}: {cheapestPrice:N0} gil");
                        ImGui.TextDisabled($"Your world ({_currentWorld}): {cheapestByWorld.FirstOrDefault(w => w.World.Equals(_currentWorld, StringComparison.OrdinalIgnoreCase))?.MinPrice ?? 0:N0} gil");
                    }
                    ImGui.Spacing();
                    
                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "Top Worlds by Price:");
                    foreach (var world in cheapestByWorld.Take(3))
                    {
                        bool isMyWorld = world.World.Equals(_currentWorld, StringComparison.OrdinalIgnoreCase);
                        bool isCheapest = world.MinPrice == cheapestPrice;
                        
                        var color = isCheapest ? new Vector4(0, 1, 0.5f, 1) : (isMyWorld ? new Vector4(0.4f, 0.8f, 1, 1) : new Vector4(1, 1, 1, 1));
                        var icon = isCheapest ? FontAwesomeIcon.Trophy : (isMyWorld ? FontAwesomeIcon.Home : FontAwesomeIcon.Globe);
                        
                        ImGui.TextColored(color, $"{icon.ToIconString()} {world.World}: {world.MinPrice:N0} gil");
                        if (isMyWorld) ImGui.SameLine();
                        if (isMyWorld) ImGui.TextDisabled("(you)");
                    }
                    ImGui.Spacing();
                    
                    if (ImGui.Button(_showCrossWorldListings ? "Hide All Listings" : "Show All Listings"))
                    {
                        _showCrossWorldListings = !_showCrossWorldListings;
                    }
                    
                    if (_showCrossWorldListings)
                    {
                        ImGui.Spacing();
                        if (ImGui.BeginTable("CrossWorldListings", 7, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(0, 300)))
                        {
                            ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthFixed, 120);
                            ImGui.TableSetupColumn("Unit Price", ImGuiTableColumnFlags.WidthFixed, 80);
                            ImGui.TableSetupColumn("Qty", ImGuiTableColumnFlags.WidthFixed, 40);
                            ImGui.TableSetupColumn("Total", ImGuiTableColumnFlags.WidthFixed, 90);
                            ImGui.TableSetupColumn("w/ Tax", ImGuiTableColumnFlags.WidthFixed, 90);
                            ImGui.TableSetupColumn("HQ", ImGuiTableColumnFlags.WidthFixed, 35);
                            ImGui.TableSetupColumn("Retainer", ImGuiTableColumnFlags.WidthStretch);
                            ImGui.TableHeadersRow();
                            
                            int rank = 0;
                            foreach (var listing in sortedByPrice)
                            {
                                rank++;
                                ImGui.TableNextRow();
                                
                                var priceWithTax = (int)(listing.TotalPrice / 0.95);
                                var taxAmount = priceWithTax - listing.TotalPrice;
                                bool isMyWorld = listing.WorldName.Equals(_currentWorld, StringComparison.OrdinalIgnoreCase);
                                bool isCheapest = rank == 1;
                                
                                ImGui.TableNextColumn();
                                var worldColor = isCheapest ? new Vector4(1, 0.8f, 0, 1) : (isMyWorld ? new Vector4(0.4f, 0.8f, 1, 1) : new Vector4(0.6f, 0.8f, 1, 1));
                                ImGui.TextColored(worldColor, listing.WorldName);
                                if (isCheapest && ImGui.IsItemHovered()) ImGui.SetTooltip("Cheapest listing!");
                                if (isMyWorld && ImGui.IsItemHovered()) ImGui.SetTooltip("Your world");
                                
                                ImGui.TableNextColumn();
                                var priceColor = isCheapest ? new Vector4(0, 1, 0.5f, 1) : new Vector4(1, 0.8f, 0, 1);
                                ImGui.TextColored(priceColor, $"{listing.PricePerUnit:N0}");
                                
                                ImGui.TableNextColumn();
                                ImGui.Text(listing.Quantity.ToString());
                                
                                ImGui.TableNextColumn();
                                ImGui.Text($"{listing.TotalPrice:N0}");
                                
                                ImGui.TableNextColumn();
                                ImGui.TextColored(new Vector4(1, 0.5f, 0.5f, 1), $"{priceWithTax:N0}");
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip($"List at this price to RECEIVE {listing.TotalPrice:N0} gil after 5% tax ({taxAmount:N0} gil tax)");
                                }
                                
                                ImGui.TableNextColumn();
                                if (listing.Hq)
                                {
                                    ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), "HQ");
                                }
                                
                                ImGui.TableNextColumn();
                                ImGui.Text(listing.RetainerName);
                            }
                            
                            ImGui.EndTable();
                        }
                    }
                }
            }
            
            if (_priceSuggestion?.LastUploadTime != null)
            {
                try
                {
                    long timestamp = _priceSuggestion.LastUploadTime.Value;

                    if (timestamp > 253402300799L)
                    {
                        timestamp = timestamp / 1000;
                    }
                    
                    var uploadTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
                    var timeAgo = DateTime.Now - uploadTime;
                    ImGui.Spacing();
                    ImGui.TextDisabled($"Data updated {timeAgo.TotalMinutes:F0} minutes ago");
                }
                catch
                {
                    // ignore invalid timestamps
                }
            }
            
            DrawCrossDcSection();
            
            ImGui.EndChild();
        }
    }

    private void DrawCrossDcSection()
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        if (!_hasAttemptedCrossDcLoad && !_isLoadingCrossDcData && _crossDcData == null)
        {
            var cached = UniversalisClient.GetCachedCrossDcData(_currentItemId, _currentDatacenter);
            if (cached != null)
            {
                _crossDcData = cached;
                _hasAttemptedCrossDcLoad = true;
            }
        }
        
        if (!_hasAttemptedCrossDcLoad)
        {
            if (ImGui.Button($"{FontAwesomeIcon.Globe.ToIconString()} Load Cross-DC Data"))
            {
                _ = LoadCrossDcDataAsync();
            }
            ImGui.SameLine();
            ImGui.TextDisabled("(Fetches from all 12 other datacenters)");
            return;
        }
        
        if (_isLoadingCrossDcData)
        {
            ImGui.TextColored(new Vector4(0.4f, 0.8f, 1, 1), $"{FontAwesomeIcon.Spinner.ToIconString()} Loading cross-datacenter data...");
            return;
        }

        if (_crossDcData == null || !_crossDcData.OtherDatacenterListings.Any())
        {
            ImGui.TextDisabled("No cross-DC data available.");
            if (ImGui.Button("Retry"))
            {
                _hasAttemptedCrossDcLoad = false;
            }
            return;
        }

        if (ImGui.Button($"{FontAwesomeIcon.Sync.ToIconString()} Refresh Cross-DC Data"))
        {
            _ = LoadCrossDcDataAsync(forceRefresh: true);
        }
        ImGui.SameLine();
        ImGui.TextDisabled("(Clears cache and re-fetches all DCs)");
        ImGui.Spacing();

        var dcWithListings = _crossDcData.OtherDatacenterListings.Where(d => d.ListingCount > 0).ToList();
        
        if (!dcWithListings.Any())
        {
            return;
        }

        if (ImGui.CollapsingHeader($"Cross-DC (All Regions) - {dcWithListings.Count} DCs with listings", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (_crossDcData.OverallMinPrice > 0)
            {
                ImGui.TextColored(new Vector4(1, 1, 0, 1), "Global Summary");
                
                if (ImGui.BeginTable("CrossDcGlobalSummary", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                {
                    ImGui.TableSetupColumn("Metric", ImGuiTableColumnFlags.WidthFixed, 100);
                    ImGui.TableSetupColumn("Price", ImGuiTableColumnFlags.WidthFixed, 100);
                    ImGui.TableSetupColumn("Datacenter", ImGuiTableColumnFlags.WidthStretch);
                    
                    var cheapestDc = dcWithListings.OrderBy(d => d.MinPrice).First();
                    var expensiveDc = dcWithListings.OrderByDescending(d => d.MaxPrice).First();
                    
                    ImGuiUtils.DrawPriceRowWithWorld("Minimum", _crossDcData.OverallMinPrice, cheapestDc.DatacenterName, new Vector4(0.5f, 1, 0.5f, 1));
                    ImGuiUtils.DrawPriceRow("Average", _crossDcData.OverallAvgPrice, new Vector4(1, 1, 1, 1));
                    ImGuiUtils.DrawPriceRowWithWorld("Maximum", _crossDcData.OverallMaxPrice, expensiveDc.DatacenterName, new Vector4(1, 0.5f, 0.5f, 1));
                    
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("Total Listings");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{_crossDcData.TotalListingCount:N0}");
                    ImGui.TableNextColumn();
                    ImGui.TextDisabled($"Across {dcWithListings.Count} datacenters");
                    
                    ImGui.EndTable();
                }
                ImGui.Spacing();
            }

            var byRegion = dcWithListings.GroupBy(d => d.Region).OrderBy(g => g.Key);
            
            foreach (var regionGroup in byRegion)
            {
                var regionDisplayName = regionGroup.Key switch
                {
                    "NorthAmerica" => "North America",
                    "Europe" => "Europe",
                    "Japan" => "Japan",
                    "Oceania" => "Oceania",
                    _ => regionGroup.Key
                };
                
                var regionMin = regionGroup.Min(d => d.MinPrice);
                var regionListings = regionGroup.Sum(d => d.ListingCount);
                
                if (ImGui.CollapsingHeader($"{regionDisplayName} - Min: {regionMin:N0} gil ({regionListings} listings)"))
                {
                    foreach (var dcListing in regionGroup.OrderBy(d => d.MinPrice))
                    {
                        var isCheapestGlobal = dcListing.MinPrice == _crossDcData.OverallMinPrice && _crossDcData.OverallMinPrice > 0;
                        var icon = isCheapestGlobal ? FontAwesomeIcon.Trophy : FontAwesomeIcon.Server;
                        var headerColor = isCheapestGlobal ? new Vector4(0, 1, 0.5f, 1) : new Vector4(1, 1, 1, 1);
                        
                        var headerText = $"{icon.ToIconString()} {dcListing.DatacenterName} - Min: {dcListing.MinPrice:N0} gil ({dcListing.ListingCount} listings)";
                        
                        if (ImGui.CollapsingHeader(headerText))
                        {
                            if (ImGui.BeginTable($"DcStats_{dcListing.DatacenterName}", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                            {
                                ImGui.TableSetupColumn("Metric", ImGuiTableColumnFlags.WidthFixed, 100);
                                ImGui.TableSetupColumn("Price", ImGuiTableColumnFlags.WidthStretch);
                                
                                ImGuiUtils.DrawPriceRow("Minimum", dcListing.MinPrice, new Vector4(0.5f, 1, 0.5f, 1));
                                ImGuiUtils.DrawPriceRow("Average", dcListing.AvgPrice, new Vector4(1, 1, 1, 1));
                                ImGuiUtils.DrawPriceRow("Maximum", dcListing.MaxPrice, new Vector4(1, 0.5f, 0.5f, 1));
                                
                                ImGui.EndTable();
                            }

                            // Show top listings from this DC
                            if (dcListing.Listings.Any())
                            {
                                ImGui.Spacing();
                                if (ImGui.BeginTable($"DcListings_{dcListing.DatacenterName}", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(0, 150)))
                                {
                                    ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthFixed, 100);
                                    ImGui.TableSetupColumn("Price", ImGuiTableColumnFlags.WidthFixed, 80);
                                    ImGui.TableSetupColumn("Qty", ImGuiTableColumnFlags.WidthFixed, 40);
                                    ImGui.TableSetupColumn("Total", ImGuiTableColumnFlags.WidthFixed, 80);
                                    ImGui.TableSetupColumn("HQ", ImGuiTableColumnFlags.WidthFixed, 35);
                                    ImGui.TableSetupColumn("Retainer", ImGuiTableColumnFlags.WidthStretch);
                                    ImGui.TableHeadersRow();
                                    
                                    foreach (var listing in dcListing.Listings.OrderBy(l => l.PricePerUnit).Take(10))
                                    {
                                        ImGui.TableNextRow();
                                        
                                        ImGui.TableNextColumn();
                                        ImGui.Text(listing.WorldName);
                                        
                                        ImGui.TableNextColumn();
                                        ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), $"{listing.PricePerUnit:N0}");
                                        
                                        ImGui.TableNextColumn();
                                        ImGui.Text(listing.Quantity.ToString());
                                        
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{listing.TotalPrice:N0}");
                                        
                                        ImGui.TableNextColumn();
                                        if (listing.Hq)
                                        {
                                            ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), "HQ");
                                        }
                                        
                                        ImGui.TableNextColumn();
                                        ImGui.Text(listing.RetainerName);
                                    }
                                    
                                    ImGui.EndTable();
                                }
                            }
                        }
                    }
                }
            }
        }
    }
    

    private async Task RefreshMarketDataAsync()
    {
        if (_isLoadingMarketData || string.IsNullOrEmpty(_currentWorld)) return;
        
        _isLoadingMarketData = true;
        
        try
        {
            UniversalisClient.ClearCache();
            
            bool isHq = _localItem?.CanBeHq ?? false;
            _marketData = await UniversalisClient.GetMarketData(_currentItemId, _currentWorld, false);
            
            if (_marketData != null)
            {
                _priceSuggestion = UniversalisClient.GetPriceSuggestion(_marketData, false);
                
                var dcName = UniversalisClient.GetDatacenterForWorld(_currentWorld) ?? _currentWorld;
                _crossWorldListings = await UniversalisClient.GetCrossWorldListings(_currentItemId, dcName, 50);
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"Failed to refresh market data: {ex.Message}");
        }
        finally
        {
            _isLoadingMarketData = false;
        }
    }

    private async Task LoadCrossDcDataAsync(bool forceRefresh = false)
    {
        if (_isLoadingCrossDcData || string.IsNullOrEmpty(_currentDatacenter)) return;
        
        _isLoadingCrossDcData = true;
        _hasAttemptedCrossDcLoad = true;
        
        try
        {
            if (forceRefresh)
            {
                UniversalisClient.ClearCrossDcCache();
            }
            
            _crossDcData = await UniversalisClient.GetCrossDcListings(_currentItemId, _currentDatacenter, 50, useCache: !forceRefresh);
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"Failed to load cross-DC data: {ex.Message}");
        }
        finally
        {
            _isLoadingCrossDcData = false;
        }
    }

    private void DrawAcquisitionTab(Item item)
    {
        if (ImGui.BeginChild("AcqScroll"))
        {
            if (_gatheringPoints.Any())
            {
                if (ImGui.CollapsingHeader("Gathering Locations", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    foreach (var point in _gatheringPoints)
                    {
                        var territory = "Unknown Zone";
                        if (point.TerritoryType.RowId > 0)
                            territory = point.TerritoryType.Value.PlaceName.Value.Name.ToString();
                        
                        var level = 0;
                        if (point.GatheringPointBase.RowId > 0)
                            level = (int)point.GatheringPointBase.Value.GatheringLevel;
                            
                        ImGui.BulletText($"{territory} (Lv{level})");
                    }
                }
            }

            if (_gilShops.Any())
            {
                if (ImGui.CollapsingHeader("Vendor Shops"))
                {
                    foreach (var shopId in _gilShops)
                    {
                        var shop = Svc.Data.GetExcelSheet<GilShop>()?.GetRow(shopId);
                        var shopName = shop?.Name.ToString() ?? $"Vendor Shop #{shopId}";
                        if (string.IsNullOrEmpty(shopName)) shopName = $"Vendor Shop #{shopId}";
                        ImGui.BulletText(shopName);
                    }
                }
            }
            ImGui.EndChild();
        }
    }

    private void DrawCraftingTab(Item item)
    {
        if (ImGui.BeginChild("CraftScroll"))
        {
            try
            {
                // Get the result item for display
                var resultIcon = Plugin.TextureProvider.GetFromGameIcon((uint)item.Icon);

                foreach (var recipe in _recipes)
                {
                    string job = "Unknown Job";
                    if (recipe.CraftType.RowId > 0)
                        job = recipe.CraftType.Value.Name.ToString();

                    uint recipeLevel = 0;
                    if (recipe.RecipeLevelTable.RowId > 0)
                        recipeLevel = recipe.RecipeLevelTable.RowId;

                    ImGui.BeginGroup();
                    if (resultIcon.TryGetWrap(out var wrap, out _) && wrap.Handle != nint.Zero)
                    {
                        ImGui.Image(wrap.Handle, new Vector2(32, 32));
                        ImGui.SameLine(0, 8);
                    }
                    ImGui.TextColored(new Vector4(0.4f, 0.8f, 1, 1), $"Recipe: {job} Lv{recipeLevel}");
                    ImGui.EndGroup();

                    if (ImGui.BeginTable($"RecipeTable_{recipe.RowId}", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
                    {
                        ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, 36);
                        ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("Amount", ImGuiTableColumnFlags.WidthFixed, 60);
                        ImGui.TableHeadersRow();

                        for (int i = 0; i < recipe.Ingredient.Count; i++)
                        {
                            var ing = recipe.Ingredient[i];
                            if (ing.RowId == 0) continue;

                            var itemName = "Unknown Ingredient";
                            try
                            {
                                if (ing.RowId > 0 && ing.IsValid)
                                    itemName = ing.Value.Name.ToString();
                            }
                            catch
                            { /* ignore */ }

                            var amount = recipe.AmountIngredient[i];

                            ImGui.TableNextRow();

                            ImGui.TableNextColumn();
                            DrawItemIcon(ing.RowId);

                            ImGui.TableNextColumn();
                            bool wasClicked = DrawClickableItemWithContextMenu(ing.RowId, itemName);
                            if (wasClicked) Inspect(ing.RowId);

                            ImGui.TableNextColumn();
                            ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1), $"x{amount}");
                        }

                        ImGui.EndTable();
                    }

                    ImGui.Spacing();
                }
            }
            catch (Exception ex)
            {  
              // eh
            }

            ImGui.EndChild();
        }
    }
    private void DrawItemIcon(uint itemId)
    {
        try
        {
            var item = Svc.Data.GetExcelSheet<Item>()?.GetRow(itemId);
            if (item?.RowId > 0)
            {
                var icon = Plugin.TextureProvider.GetFromGameIcon((uint)item.Value.Icon);
                if (icon.TryGetWrap(out var wrap, out _) && wrap.Handle != nint.Zero)
                {
                    ImGui.Image(wrap.Handle, new Vector2(28, 28));
                    return;
                }
            }
        }
        catch { }
        
        ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "?");
    }

    private bool DrawClickableItemWithContextMenu(uint itemId, string itemName)
    {
        ImGui.BeginGroup();
        ImGui.TextColored(new Vector4(1, 1, 1, 1), itemName);
        var rectMin = ImGui.GetItemRectMin();
        var rectMax = ImGui.GetItemRectMax();
        ImGui.EndGroup();

        bool isHovered = ImGui.IsItemHovered();
        
        if (isHovered)
        {
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddLine(
                new Vector2(rectMin.X, rectMax.Y + 1),
                new Vector2(rectMax.X, rectMax.Y + 1),
                ImGui.GetColorU32(new Vector4(0.4f, 0.8f, 1, 1)),
                1f
            );
            
            ImGui.BeginTooltip();
            ImGui.TextColored(new Vector4(0.4f, 0.8f, 1, 1), itemName);
            ImGui.TextDisabled($"ID: {itemId}");
            ImGui.Text("Left-click to inspect");
            ImGui.Text("Right-click for options");
            ImGui.EndTooltip();
        }

        bool wasClicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            ImGui.OpenPopup($"ContextMenu_{itemId}");
        }
        
        if (ImGui.BeginPopup($"ContextMenu_{itemId}"))
        {
            ImGui.TextColored(new Vector4(0.4f, 0.8f, 1, 1), itemName);
            ImGui.Separator();
            
            if (ImGui.MenuItem($"{FontAwesomeIcon.Search.ToIconString()} Inspect"))
            {
                Inspect(itemId);
            }
            
            bool isIgnored = Plugin.Configuration.ItemConfigs.TryGetValue(itemId, out var settings) && settings.Ignore;
            string ignoreLabel = isIgnored ? $"{FontAwesomeIcon.Eye.ToIconString()} Unignore Item" : $"{FontAwesomeIcon.EyeSlash.ToIconString()} Ignore Item";
            
            if (ImGui.MenuItem(ignoreLabel))
            {
                ToggleIgnoreItem(itemId, itemName, !isIgnored);
            }
            
            if (ImGui.MenuItem($"{FontAwesomeIcon.Cog.ToIconString()} Configure..."))
            {
                Plugin.Instance.ConfigWindow.OpenForItem((int)itemId);
            }
            
            ImGui.Separator();
            
            if (ImGui.MenuItem($"{FontAwesomeIcon.ExternalLinkAlt.ToIconString()} Open in Garland Tools"))
            {
                try
                {
                    ItemUtils.OpenGarlandTools(itemId);
                }
                catch (Exception ex)
                {
                    Svc.Log.Error(ex, "Could not open Garland Tools link");
                }
            }
            
            ImGui.EndPopup();
        }

        return wasClicked;
    }

    private void ToggleIgnoreItem(uint itemId, string itemName, bool setIgnore)
    {
        ItemUtils.ToggleIgnore(itemId, itemName, setIgnore);
    }

    private void DrawStatCard(string title, System.Action content)
    {
        ImGuiUtils.DrawStatCard(title, content);
    }

    private void DrawFooter()
    {
        // TODO/TBD
    }

    private void DrawItemLink(uint id, string name, int amount = -1)
    {
        string label = amount > 0 ? $"{amount}x {name}" : name;
        if (ImGuiComponents.IconButton($"ins_{id}", FontAwesomeIcon.Search)) Inspect(id);
        ImGui.SameLine();
        ImGui.Text(label);
    }

    private void DrawStat(string label, string value)
    {
        ImGuiUtils.DrawStat(label, value);
    }
}
