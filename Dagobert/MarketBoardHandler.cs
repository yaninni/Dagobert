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

            // --- Check Ignore Config Immediately ---
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

            // --- Filtering Logic ---
            var itemSheetRow = _items.GetRow(itemId);
            bool canBeHq = itemSheetRow.RowId != 0 && itemSheetRow.CanBeHq;

            var listings = offer.ItemListings.AsEnumerable();

            if (Plugin.Configuration.MatchStackSize && CurrentStackSize > 1)
            {
                listings = listings.Where(l => l.ItemQuantity >= (CurrentStackSize * 0.5) || l.ItemQuantity == 99);
                if (!listings.Any()) listings = offer.ItemListings.AsEnumerable();
            }

            if (_useHq && canBeHq)
                listings = listings.Where(l => l.IsHq);

            var validListings = listings.ToList();
            if (validListings.Count == 0)
            {
                _lastRequestId = offer.RequestId;
                _newRequest = false;
                UpdatePrice(PRICE_NO_DATA);
                return;
            }

            // --- Smart Outlier Protection ---
            var targetListing = validListings[0];
            if (Plugin.Configuration.SmartOutlierProtection && validListings.Count >= 2)
            {
                var first = validListings[0].PricePerUnit;
                var second = validListings[1].PricePerUnit;
                if (first < (second * 0.5))
                {
                    targetListing = validListings[1];
                    DetectedBaitIndex = 0;
                }
            }

            int lowest = (int)targetListing.PricePerUnit;
            int final = CalculatePrice(lowest, targetListing.RetainerId, itemId);

            if (final > lowest) final = lowest;
            if (final < 1) final = 1;

            _lastRequestId = offer.RequestId;
            _newRequest = false;
            UpdatePrice(final);
        }

        private int CalculatePrice(int lowest, ulong retainerId, uint itemId)
        {
            var cfg = Plugin.Configuration;
            ItemSettings? itemSet = null;
            if (cfg.ItemConfigs.TryGetValue(itemId, out var settings)) itemSet = settings;

            // Pricing Personality
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

            // Clean Numbers
            if (cfg.PricingPersonality == PricingPersonality.CleanNumbers)
            {
                if (p > 100000) p = (p / 1000) * 1000;
                else if (p > 10000) p = (p / 100) * 100;
                else if (p > 1000) p = (p / 50) * 50;
                if (p < 1) p = 1;
            }

            // Min Price Enforce
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