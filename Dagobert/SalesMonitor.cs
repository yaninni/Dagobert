using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.DalamudServices;
using System;
using System.Text.RegularExpressions;
using Dagobert.Windows;
using Dagobert.Utilities;

namespace Dagobert
{
    public class SalesMonitor : IDisposable
    {
        private static readonly Regex SaleRx = new(@"sale in the (?<city>.+) markets sold for (?<price>[\d,.]+) gil", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SaleRxDE = new(@"wurde für (?<price>[\d,.]+) Gil .* (?<city>.+) verkauft", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SaleRxFR = new(@"vendu pour (?<price>[\d,.]+) gils .* (?<city>.+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SaleRxJP = new(@"(?<price>[\d,.]+)ギル.+(?<city>.+)で売れました", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private bool _regexErrorLogged = false;

        public SalesMonitor() => Svc.Chat.ChatMessage += OnMsg;
        public void Dispose() => Svc.Chat.ChatMessage -= OnMsg;

        private void OnMsg(XivChatType type, int ts, ref SeString sender, ref SeString msg, ref bool handled)
        {
            string txt = msg.ToString();
            
            bool isSaleMessage = txt.Contains("sold for", StringComparison.OrdinalIgnoreCase) ||
                                txt.Contains("verkauft", StringComparison.OrdinalIgnoreCase) ||  // German
                                txt.Contains("vendu", StringComparison.OrdinalIgnoreCase) ||     // French
                                txt.Contains("売れました", StringComparison.OrdinalIgnoreCase) || // Japanese
                                txt.Contains("gil", StringComparison.OrdinalIgnoreCase);
            
            if (!isSaleMessage) return;
            
            Svc.Log.Debug($"[SalesMonitor] Potential sale message detected: {txt}");

            try
            {
                var m = SaleRx.Match(txt);
                Svc.Log.Debug($"[SalesMonitor] English regex match: {m.Success}");
                if (!m.Success) { m = SaleRxDE.Match(txt); Svc.Log.Debug($"[SalesMonitor] German regex match: {m.Success}"); }
                if (!m.Success) { m = SaleRxFR.Match(txt); Svc.Log.Debug($"[SalesMonitor] French regex match: {m.Success}"); }
                if (!m.Success) { m = SaleRxJP.Match(txt); Svc.Log.Debug($"[SalesMonitor] Japanese regex match: {m.Success}"); }
                if (!m.Success)
                {
                    Svc.Log.Debug("[SalesMonitor] No regex pattern matched");
                    return;
                }

                string pre = txt[..m.Index];
                if (pre.StartsWith("The ", StringComparison.OrdinalIgnoreCase)) pre = pre[4..];
                int idx = pre.IndexOf(" you put up", StringComparison.OrdinalIgnoreCase);
                string rawItem = (idx > 0 ? pre[..idx] : pre).Trim();

                string priceStr = m.Groups["price"].Value.Replace(",", "").Replace(".", "");
                Svc.Log.Debug($"[SalesMonitor] Extracted price string: '{priceStr}'");
                if (int.TryParse(priceStr, out int price))
                {
                    Svc.Log.Debug($"[SalesMonitor] Parsed price: {price}");
                    
                    var cfg = Plugin.Configuration;
                    cfg.Initialize();
                    cfg.Stats.TotalGilEarned += price;
                    cfg.Stats.TotalItemsSold++;

                    bool hq = StringUtils.ContainsHqIcon(rawItem) || txt.Contains("(HQ)");
                    string clean = StringUtils.GetCleanItemName(rawItem);
                    string city = m.Groups["city"].Value;
                    uint resolvedId = ItemUtils.GetItemIdByName(clean);

                    cfg.Stats.SalesHistory.Add(new SaleRecord
                    {
                        ItemId = resolvedId,
                        ItemName = clean,
                        Price = price,
                        IsHq = hq,
                        City = city,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    });

                    if (cfg.Stats.SalesHistory.Count > 100) cfg.Stats.SalesHistory.RemoveAt(0);
                    cfg.Save();

                    VisualMonitor.LogActivity(ActivityType.Success, $"Sale! {clean} ({price:N0} gil)");
                    DiscordSender.SendSaleNotification(clean, resolvedId, price, city, hq, cfg.Stats.TotalGilEarned, cfg.Stats.TotalItemsSold);
                }
            }
            catch (Exception ex)
            {
                if (!_regexErrorLogged) { Svc.Log.Error(ex, "Sales parse error"); _regexErrorLogged = true; }
            }
        }
    }
}