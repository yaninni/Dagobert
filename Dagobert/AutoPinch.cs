using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static ECommons.GenericHelpers;
using static ECommons.UIHelpers.AtkReaderImplementations.ReaderContextMenu;
//TODO fix pinch all retainers it's borked
namespace Dagobert
{
    internal sealed class AutoPinch : Window, IDisposable
    {
        private readonly MarketBoardHandler _mbHandler;
        private readonly TaskManager _tm;
        private readonly Dictionary<string, int?> _cachedPrices = [];
        private RetainerStats? _stats;
        private int? _newPrice;
        private int? _oldPrice;
        private bool _skipItem;
        private long _priceWaitStart = 0;
        private Vector2 _lastMousePos;
        private int _itemsProcessedSession = 0;
        private int _totalItemsRetainer = 0;
        private int _currentItemIndex = 0;

        private DelayStrategy Strategy => Plugin.Configuration.DelayStrategy;

        public AutoPinch() : base("Dagobert", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.AlwaysAutoResize, true)
        {
            _mbHandler = new MarketBoardHandler();
            _mbHandler.NewPriceReceived += (_, e) => _newPrice = e.NewPrice;

            Position = Vector2.Zero;
            IsOpen = true;
            ShowCloseButton = false;
            DisableWindowSounds = true;
            SizeConstraints = new WindowSizeConstraints { MaximumSize = Vector2.Zero };

            _tm = new TaskManager { TimeLimitMS = 10000, AbortOnTimeout = true };

            Svc.Chat.ChatMessage += OnChatMessage;
        }

        public void Dispose()
        {
            Svc.Chat.ChatMessage -= OnChatMessage;
            _mbHandler.Dispose();
        }

        private void OnChatMessage(XivChatType type, int ts, ref SeString sender, ref SeString msg, ref bool handled)
        {
            if (Plugin.Configuration.StopOnTell && type == XivChatType.TellIncoming && _tm.IsBusy)
            {
                AbortOperation();
                DiscordSender.SendAlert(sender.ToString(), msg.ToString());
            }
        }
        public override unsafe void Draw()
        {
            try
            {
                if (Plugin.Configuration.MouseEntropyCheck && _tm.IsBusy)
                {
                    var cur = ImGui.GetMousePos();
                    if (_lastMousePos != Vector2.Zero && Vector2.Distance(cur, _lastMousePos) > 50)
                    {
                        AbortOperation();
                        DiscordSender.SendLog("[Safety] Aborted due to mouse movement.");
                    }
                    _lastMousePos = cur;
                }
                OverlayRenderer.DrawRetainerListOverlay(
                    _tm.IsBusy,
                    PinchAllRetainers,
                    AbortOperation
                );

                OverlayRenderer.DrawSellListOverlay(
                    _tm.IsBusy,
                    _currentItemIndex,
                    _totalItemsRetainer,
                    PinchAllRetainerItems,
                    () => UnlistAllItems(false),
                    () => UnlistAllItems(true),
                    AbortOperation
                );
            }
            catch (Exception ex)
            {
                AbortOperation();
                if (Plugin.Configuration.ShowErrorsInChat) Svc.Chat.PrintError($"AutoPinch Error: {ex.Message}");
            }
        }

        private void AbortOperation()
        {
            _tm.Abort();
            RemoveTalkListeners();
        }
        private unsafe void PinchAllRetainers() // this is absolutely fucked and I am not sure where I went wrong needs fixing soon TM
        {
            if (_tm.IsBusy) return;
            ClearState();
            if (!TryGetAddonByName<AtkUnitBase>(UIConsts.AddonRetainerList, out var addon) || !IsAddonReady(addon)) return;

            Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, UIConsts.AddonTalk, SkipRetainerDialog);
            Svc.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, UIConsts.AddonTalk, SkipRetainerDialog);
            var retainers = new AddonMaster.RetainerList(addon).Retainers;
            var enabledNames = Plugin.Configuration.EnabledRetainerNames;

            if (enabledNames.Contains(Configuration.ALL_DISABLED_SENTINEL))
            {
                Communicator.PrintAllRetainersDisabled();
                return;
            }

            DiscordSender.SendLog("[Started] Auto Pinch all.");

            for (int i = 0; i < retainers.Length; i++)
            {
                var name = retainers[i].Name;
                if (enabledNames.Count > 0 && !enabledNames.Contains(name)) continue;

                int capturedIndex = i;
                _tm.Enqueue(() => AddonInteractor.SelectRetainer(capturedIndex), $"ClickRetainer_{i}");

                _tm.Enqueue(() => _stats = new RetainerStats(name));
                _tm.DelayNext(Humanizer.GetReactionDelay());

                _tm.Enqueue(AddonInteractor.ClickSellFromInventory, "SellItems");
                _tm.Enqueue(WaitForSellListVisible, "WaitForSellList");
                _tm.Enqueue(() => QueueItems(InsertSingleItem, true));

                _tm.DelayNext(Humanizer.GetReactionDelay());
                _tm.Enqueue(() => AddonInteractor.CloseWindow(UIConsts.AddonRetainerSellList), "CloseSellList");
                _tm.DelayNext(Humanizer.GetReactionDelay());
                _tm.Enqueue(SendReport);
                _tm.Enqueue(() => AddonInteractor.CloseWindow(UIConsts.AddonSelectString), "CloseRetainer");
                _tm.DelayNext(Humanizer.GetReactionDelay() + 200);
            }

            _tm.Enqueue(RemoveTalkListeners);
            _tm.Enqueue(() => DiscordSender.SendLog("[Finished] Auto Pinch."));
        }

        private void PinchAllRetainerItems()
        {
            if (_tm.IsBusy) return;
            ClearState();

            string retainer = AddonInteractor.GetRetainerNameFromSellList();
            _stats = new RetainerStats(retainer);

            QueueItems(EnqueueSingleItem, false);
            _tm.Enqueue(SendReport);
        }

        private void UnlistAllItems(bool toRetainer)
        {
            if (_tm.IsBusy) return;
            ClearState();
            _itemsProcessedSession = 0;
            QueueItems(i => EnqueueUnlist(i, toRetainer), true);
            _tm.Enqueue(() => DiscordSender.SendLog("[Finished] Unlisting items."));
        }
        private bool? QueueItems(Action<int> queueFunc, bool reverse)
        {
            int count = AddonInteractor.GetSellListCount();
            _totalItemsRetainer = count;
            _currentItemIndex = 0;

            if (count == 0) return true;

            if (reverse) for (int i = count - 1; i >= 0; i--) queueFunc(i);
            else for (int i = 0; i < count; i++) queueFunc(i);
            return true;
        }

        private void EnqueueUnlist(int i, bool toRetainer)
        {
            _tm.Enqueue(() => AddonInteractor.OpenContextMenuForSellItem(i), $"Ctx_{i}");
            _tm.DelayNext(Humanizer.GetFittsDelay());
            _tm.Enqueue(() => ClickContextWithdraw(toRetainer), $"Wd_{i}");
            _tm.DelayNext(Humanizer.GetReactionDelay());
            _tm.Enqueue(() => { _currentItemIndex++; _itemsProcessedSession++; return true; }, "Inc");
        }

        private void EnqueueSingleItem(int i) => BuildItemTaskChain(i, (f, n) => _tm.Enqueue(f, n), ms => _tm.DelayNext(ms));
        private void InsertSingleItem(int i) => BuildItemTaskChain(i, (f, n) => _tm.Insert(f, n), ms => _tm.InsertDelayNext(ms));

        private void BuildItemTaskChain(int index, Action<Func<bool?>, string> add, Action<int> delay)
        {
            add(() => {
                _newPrice = null;
                _skipItem = false;
                _mbHandler.Reset();
                _priceWaitStart = 0;
                return true;
            }, "ResetState");

            if (Plugin.Configuration.EnableAdvancedHumanization && Humanizer.ShouldTakeMicroBreak())
            {
                delay(Random.Shared.Next(4000, 8000));
            }

            if (Plugin.Configuration.EnableMisclicks && Random.Shared.Next(100) < 2)
            {
                add(() => AddonInteractor.OpenContextMenuForSellItem(index + 1), "Misclick_Open");
                delay(Humanizer.GetReactionDelay() + 200);
                add(() => { AddonInteractor.CloseWindow(UIConsts.AddonContextMenu); return true; }, "Misclick_Close");
                delay(Humanizer.GetReactionDelay() + 400);
            }

            delay(Humanizer.GetFittsDelay());
            add(() => AddonInteractor.OpenContextMenuForSellItem(index), $"Ctx_{index}");

            add(WaitForContextMenuVisible, "WaitMenu");
            add(ClickAdjustPrice, $"Adj_{index}");
            add(WaitForRetainerSellVisible, "WaitSellWin");

            add(DelayMarketBoard, $"WaitMB_{index}");
            add(ClickComparePrice, $"Cmp_{index}");
            add(WaitForSearchResultVisible, "WaitSearchWin");
            add(WaitForPriceData, "WaitData");
            add(CheckForBaitInspection, $"BaitCheck_{index}");

            int rnd = RandomDelayGenerator.GetRandomDelay(Plugin.Configuration.MarketBoardKeepOpenMin, Plugin.Configuration.MarketBoardKeepOpenMax, Strategy);
            if (Plugin.Configuration.EnableAdvancedHumanization) rnd = Humanizer.GetCognitiveDelay(rnd);
            if (Plugin.Configuration.EnableFatigue) rnd += (_itemsProcessedSession * 50);
            delay(rnd);

            add(SetNewPrice, $"Set_{index}");
            add(() => { _currentItemIndex++; _itemsProcessedSession++; return true; }, "Inc");
        }
        private bool? WaitForSellListVisible()
        {
            if (AddonInteractor.IsWindowVisible(UIConsts.AddonRetainerSellList))
            {
                _tm.DelayNext(Humanizer.GetReactionDelay());
                return true;
            }
            return false;
        }

        private bool? WaitForContextMenuVisible()
        {
            if (AddonInteractor.IsWindowVisible(UIConsts.AddonContextMenu))
            {
                _tm.DelayNext(Humanizer.GetReactionDelay());
                return true;
            }
            return false;
        }

        private bool? WaitForRetainerSellVisible()
        {
            if (_skipItem) return true;
            if (AddonInteractor.IsWindowVisible(UIConsts.AddonRetainerSell))
            {
                _tm.DelayNext(Humanizer.GetReactionDelay());
                return true;
            }
            return false;
        }

        private bool? WaitForSearchResultVisible()
        {
            if (_skipItem) return true;
            if (_newPrice.HasValue) return true;
            if (AddonInteractor.IsWindowVisible(UIConsts.AddonItemSearchResult))
            {
                _tm.DelayNext(Humanizer.GetReactionDelay());
                return true;
            }
            return false;
        }

        private bool? WaitForPriceData()
        {
            if (_skipItem) return true;
            if (_newPrice.HasValue) return true;

            if (_priceWaitStart == 0) _priceWaitStart = Environment.TickCount64;
            if (Environment.TickCount64 - _priceWaitStart > 4000)
            {
                _skipItem = true;
                _stats?.AddError("Unknown", "Skipped (Market data timeout).");
                return true;
            }
            return false;
        }

        private bool? ClickAdjustPrice()
        {
            var entries = AddonInteractor.GetContextMenuEntries();
            if (IsItemMannequin(entries))
            {
                _skipItem = true;
                AddonInteractor.CloseWindow(UIConsts.AddonContextMenu);
            }
            else
            {
                AddonInteractor.ExecuteContextMenuEntry(UIConsts.ContextMenu_AdjustPrice_Callback);
            }
            return true;
        }

        private bool? DelayMarketBoard()
        {
            if (_skipItem) return true;
            var data = AddonInteractor.GetRetainerSellWindowData();
            if (data.ItemName == "") return false;

            if (!_cachedPrices.TryGetValue(data.ItemName, out int? val) || val <= 0)
            {
                _tm.InsertDelayNext(500);
            }
            return true;
        }

        private bool? ClickComparePrice()
        {
            if (_skipItem) return true;
            var data = AddonInteractor.GetRetainerSellWindowData();
            if (data.ItemName == "") return false;
            _mbHandler.CurrentStackSize = data.Quantity;
            if (_cachedPrices.TryGetValue(data.ItemName, out int? val) && val > 0) _newPrice = val;
            else AddonInteractor.OpenMarketBoardComparison();
            return true;
        }

        private bool? CheckForBaitInspection()
        {
            if (!Plugin.Configuration.EnableComparativeDoubt) return true;
            if (_mbHandler.DetectedBaitIndex.HasValue)
            {
                if (AddonInteractor.IsWindowVisible(UIConsts.AddonItemSearchResult))
                {
                    AddonInteractor.InspectBaitItem(_mbHandler.DetectedBaitIndex.Value);
                    _tm.InsertDelayNext(Random.Shared.Next(2000, 3500));
                }
            }
            return true;
        }

        private bool? SetNewPrice()
        {
            if (_skipItem)
            {
                AddonInteractor.CloseWindow(UIConsts.AddonItemSearchResult);
                AddonInteractor.CloseWindow(UIConsts.AddonRetainerSell);
                return true;
            }

            AddonInteractor.CloseWindow(UIConsts.AddonItemSearchResult);
            if (!AddonInteractor.IsWindowVisible(UIConsts.AddonRetainerSell)) return false;

            if (_newPrice == MarketBoardHandler.PRICE_IGNORED)
            {
                var d = AddonInteractor.GetRetainerSellWindowData();
                _stats?.AddSkip(Communicator.GetCleanItemName(d.ItemName), "Item Ignored (Config).");
                AddonInteractor.CloseWindow(UIConsts.AddonRetainerSell);
                return true;
            }

            if (_newPrice == MarketBoardHandler.PRICE_NO_DATA || !_newPrice.HasValue)
            {
                var d = AddonInteractor.GetRetainerSellWindowData();
                _stats?.AddSkip(Communicator.GetCleanItemName(d.ItemName), "No market data / No competition.");
                AddonInteractor.CloseWindow(UIConsts.AddonRetainerSell);
                return true;
            }

            var data = AddonInteractor.GetRetainerSellWindowData();
            if (data.ItemName == "" || !data.AskingPrice.HasValue)
            {
                _stats?.AddError("Unknown", "Could not read current price.");
                AddonInteractor.CloseWindow(UIConsts.AddonRetainerSell);
                return true;
            }

            var raw = data.ItemName;
            var clean = Communicator.GetCleanItemName(raw);
            _oldPrice = data.AskingPrice.Value;

            // Failsafe Logic
            var resolvedId = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Item>()
                .FirstOrDefault(i => i.Name.ToString().Equals(clean, StringComparison.OrdinalIgnoreCase)).RowId;

            if (resolvedId > 0 && Plugin.Configuration.ItemConfigs.TryGetValue(resolvedId, out var failsafeSettings))
            {
                if (failsafeSettings.Ignore)
                {
                    _stats?.AddSkip(clean, "Item Ignored (Failsafe).");
                    AddonInteractor.CloseWindow(UIConsts.AddonRetainerSell);
                    return true;
                }

                if (failsafeSettings.MinPrice > 0 && _newPrice.Value < failsafeSettings.MinPrice)
                {
                    _newPrice = failsafeSettings.MinPrice;
                    Svc.Chat.Print($"[Dagobert] Enforcing MinPrice for {clean}: {_newPrice.Value}");
                }
            }

            // Humanization
            if (Plugin.Configuration.EnableAdvancedHumanization)
            {
                int hes = Humanizer.GetValueBasedDelay(_oldPrice.Value);
                if (_newPrice.HasValue) hes += Humanizer.GetTypingDelay(_oldPrice.Value, _newPrice.Value);
                _tm.InsertDelayNext(hes);
            }

            if (_newPrice > 0)
            {
                float cut = ((float)_newPrice.Value - _oldPrice.Value) / _oldPrice.Value * 100f;
                if (cut >= -Plugin.Configuration.MaxUndercutPercentage)
                {
                    _cachedPrices.TryAdd(raw, _newPrice);
                    AddonInteractor.SetRetainerSellPrice(_newPrice.Value);
                    Communicator.PrintPriceUpdate(raw, _oldPrice.Value, _newPrice.Value, cut);

                    if (_oldPrice.Value != _newPrice.Value)
                    {
                        _stats?.AddChange(clean, _oldPrice.Value, _newPrice.Value, MathF.Abs(cut));
                        Plugin.Configuration.Initialize();
                        Plugin.Configuration.Stats.TotalUndercutsMade++;
                        Plugin.Configuration.Save();
                        AddonInteractor.ConfirmRetainerSellPrice(true);
                    }
                    else
                    {
                        _stats?.AddSkip(clean, "Price already optimal.");
                        AddonInteractor.ConfirmRetainerSellPrice(false);
                    }
                }
                else
                {
                    Communicator.PrintAboveMaxCutError(raw);
                    _stats?.AddSkip(clean, "Cut limit exceeded.");
                    AddonInteractor.ConfirmRetainerSellPrice(false);
                }
            }
            else
            {
                _stats?.AddError(clean, "Invalid price calculation.");
                AddonInteractor.ConfirmRetainerSellPrice(false);
            }

            AddonInteractor.CloseWindow(UIConsts.AddonRetainerSell);
            return true;
        }

        private bool? ClickContextWithdraw(bool toRetainer)
        {
            var entries = AddonInteractor.GetContextMenuEntries();
            string[] searchTerms = toRetainer ? ["retainer", "gehilfen", "servant", "リテイナー"] : ["inventory", "besitz", "inventaire", "所持品"];

            for (int i = 0; i < entries.Count; i++)
            {
                string name = entries[i].Name.ToLower();
                if ((name.Contains("return") || name.Contains("take") || name.Contains("rückgabe") || name.Contains("mettre") || name.Contains("戻す"))
                    && searchTerms.Any(t => name.Contains(t)))
                {
                    AddonInteractor.ExecuteContextMenuEntry(i);
                    return true;
                }
            }

            AddonInteractor.CloseWindow(UIConsts.AddonContextMenu);
            return true;
        }

        private void SendReport() { if (_stats != null) DiscordSender.SendLog(_stats.BuildReport()); _stats = null; }
        private void ClearState() { _newPrice = null; _cachedPrices.Clear(); _skipItem = false; _stats = null; _totalItemsRetainer = 0; Humanizer.Reset(); }
        private unsafe void SkipRetainerDialog(AddonEvent t, AddonArgs a) { if (!_tm.IsBusy) RemoveTalkListeners(); else if (((AtkUnitBase*)a.Addon.Address)->IsVisible) new AddonMaster.Talk(a.Addon).Click(); }
        private void RemoveTalkListeners() { Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, UIConsts.AddonTalk, SkipRetainerDialog); Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, UIConsts.AddonTalk, SkipRetainerDialog); }
        private static bool IsItemMannequin(List<ContextMenuEntry> e) => !e.Any(x => x.Name.Contains("price", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("preis", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("価格", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("prix", StringComparison.OrdinalIgnoreCase));
    }
}