using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Network.Structures;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;
using System;
using System.Linq;
using System.Collections.Generic;
using Dagobert.Windows;
using Dagobert.Utilities;

namespace Dagobert
{
    internal unsafe sealed class MarketBoardHandler : IDisposable
    {
        private readonly Lumina.Excel.ExcelSheet<Item> _items;
        private bool _newRequest;
        private bool _useHq;
        private bool _itemHq;
        private int _lastRequestId = -1;

        public int CurrentStackSize { get; set; } = 1;
        public int? DetectedBaitIndex { get; private set; } = null;
        private readonly HashSet<ulong> _ownRetainerIds = new();

        public event EventHandler<NewPriceEventArgs>? NewPriceReceived;

        // Status Codes
        public const int PRICE_NO_DATA = -1;
        public const int PRICE_IGNORED = -2;

        public MarketBoardHandler()
        {
            _items = Svc.Data.GetExcelSheet<Item>();
            Plugin.MarketBoard.OfferingsReceived += OnOfferings;
            Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, UIConsts.AddonRetainerSell, OnRetainerSellSetup);
            Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, UIConsts.AddonItemSearchResult, OnSearchResultSetup);
        }

        public void Dispose()
        {
            Plugin.MarketBoard.OfferingsReceived -= OnOfferings;
            Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, UIConsts.AddonRetainerSell, OnRetainerSellSetup);
            Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, UIConsts.AddonItemSearchResult, OnSearchResultSetup);
        }

        public void Reset()
        {
            _lastRequestId = -1;
            _newRequest = false;
            DetectedBaitIndex = null;
            _ownRetainerIds.Clear();
        }

        private void OnOfferings(IMarketBoardCurrentOfferings offer)
        {
            if (!_newRequest) return;
            DetectedBaitIndex = null;

            uint itemId = 0;
            if (offer.ItemListings.Count > 0) itemId = (uint)offer.ItemListings[0].ItemId;

            if (offer.ItemListings.Count == 0)
            {
                _lastRequestId = offer.RequestId;
                _newRequest = false;
                UpdatePrice(PRICE_NO_DATA);
                return;
            }

            if (Plugin.Configuration.ItemConfigs.TryGetValue(itemId, out var settings))
            {
                if (settings.Ignore)
                {
                    _lastRequestId = offer.RequestId;
                    _newRequest = false;
                    UpdatePrice(PRICE_IGNORED);
                    return;
                }
            }

            Item itemSheetRow;
            bool canBeHq = false;
            try
            {
                itemSheetRow = _items.GetRow(itemId);
                canBeHq = itemSheetRow.RowId != 0 && itemSheetRow.CanBeHq;
            }
            catch
            {
                Svc.Log.Warning($"MarketBoard: Failed to get item sheet for ID {itemId}");
            }

            IMarketBoardItemListing targetListing = null;
            bool useOutlier = Plugin.Configuration.SmartOutlierProtection;
            int maxOutliersToCheck = useOutlier ? 20 : 1;
            
            var listingsArray = System.Buffers.ArrayPool<IMarketBoardItemListing>.Shared.Rent(Math.Min(offer.ItemListings.Count, 20));
            int validCount = 0;

            foreach (var l in offer.ItemListings)
            {
                if (validCount >= maxOutliersToCheck) break;

                if (Plugin.Configuration.MatchStackSize && CurrentStackSize > 1)
                {
                    if (!(l.ItemQuantity >= (CurrentStackSize * 0.5) || l.ItemQuantity == 99)) continue;
                }

                if (_useHq && canBeHq && !l.IsHq) continue;

                listingsArray[validCount++] = l;
            }

            if (validCount == 0 && Plugin.Configuration.MatchStackSize && CurrentStackSize > 1)
            {
                foreach (var l in offer.ItemListings)
                {
                    if (validCount >= maxOutliersToCheck) break;
                    if (_useHq && canBeHq && !l.IsHq) continue;
                    listingsArray[validCount++] = l;
                }
            }

            if (validCount == 0)
            {
                System.Buffers.ArrayPool<IMarketBoardItemListing>.Shared.Return(listingsArray);
                _lastRequestId = offer.RequestId;
                _newRequest = false;
                VisualMonitor.LogActivity(ActivityType.Warning, "MarketBoard: No valid data");
                UpdatePrice(PRICE_NO_DATA);
                return;
            }

            targetListing = listingsArray[0];
            if (useOutlier && validCount >= 2)
            {
                for (int i = 0; i < validCount - 1; i++)
                {
                    var currentPrice = listingsArray[i].PricePerUnit;
                    var nextPrice = listingsArray[i+1].PricePerUnit;
                    
                    if (currentPrice < (nextPrice * 0.5))
                    {
                        // Found a potential bait/outlier
                        targetListing = listingsArray[i+1];
                        DetectedBaitIndex = 0; 
                    }
                    else
                    {
                        break;
                    }
                }
            }

            int lowest = (int)targetListing.PricePerUnit;
            int final = CalculatePrice(lowest, targetListing.RetainerId, itemId);
            
            System.Buffers.ArrayPool<IMarketBoardItemListing>.Shared.Return(listingsArray);

            if (final > lowest) final = lowest;
            if (final < 1) final = 1;

            _lastRequestId = offer.RequestId;
            _newRequest = false;
            VisualMonitor.LogActivity(ActivityType.Success, $"Recieved Price: {final} gil");
            UpdatePrice(final);
        }

        private int CalculatePrice(int lowest, ulong retainerId, uint itemId)
        {
            var cfg = Plugin.Configuration;
            ItemSettings? itemSet = null;
            if (cfg.ItemConfigs.TryGetValue(itemId, out var settings)) itemSet = settings;

            int p;

            if (!cfg.UndercutSelf && IsOwnRetainer(retainerId)) p = lowest;
            else if (cfg.PricingPersonality == PricingPersonality.PoliteMatch) p = lowest;
            else if (itemSet != null && itemSet.MatchOnly) p = lowest;
            else
            {
                if (cfg.UndercutMode == UndercutMode.FixedAmount)
                    p = Math.Max(lowest - cfg.UndercutAmount, 1);
                else
                    p = Math.Max((100 - cfg.UndercutAmount) * lowest / 100, 1);
            }

            if (cfg.PricingPersonality == PricingPersonality.CleanNumbers)
            {
                if (p > 100000) p = (p / 1000) * 1000;
                else if (p > 10000) p = (p / 100) * 100;
                else if (p > 1000) p = (p / 50) * 50;
                if (p < 1) p = 1;
            }

            if (itemSet != null && itemSet.MinPrice > 0)
            {
                if (p < itemSet.MinPrice) p = itemSet.MinPrice;
            }

            return p;
        }

        private bool IsOwnRetainer(ulong id)
        {
            if (_ownRetainerIds.Count == 0)
            {
                var mgr = RetainerManager.Instance();
                if (mgr != null)
                {
                    for (uint i = 0; i < mgr->GetRetainerCount(); ++i)
                    {
                        var retainer = mgr->GetRetainerBySortedIndex(i);
                        if (retainer != null) _ownRetainerIds.Add(retainer->RetainerId);
                    }
                }
            }
            return _ownRetainerIds.Contains(id);
        }

        private void OnSearchResultSetup(AddonEvent t, AddonArgs a) { _newRequest = true; _useHq = Plugin.Configuration.HQ && _itemHq; }
        private void OnRetainerSellSetup(AddonEvent t, AddonArgs a) => _itemHq = ((AddonRetainerSell*)a.Addon.Address)->ItemName->NodeText.ToString().Contains('\uE03C');
        private void UpdatePrice(int p) => NewPriceReceived?.Invoke(this, new NewPriceEventArgs(p));
    }
}