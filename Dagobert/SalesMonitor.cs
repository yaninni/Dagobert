using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.DalamudServices;
using System;
using System.Text.RegularExpressions;
// this shit is broken for now TODO
namespace Dagobert
{
    public class SalesMonitor : IDisposable
    {
        private static readonly Regex SaleRx = new(@"sale in the (?<city>.+) markets sold for (?<price>[\d,.]+) gil", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private bool _regexErrorLogged = false;

        public SalesMonitor() => Svc.Chat.ChatMessage += OnMsg;
        public void Dispose() => Svc.Chat.ChatMessage -= OnMsg;

        private void OnMsg(XivChatType type, int ts, ref SeString sender, ref SeString msg, ref bool handled)
        {
            if (type != XivChatType.SystemMessage && type != XivChatType.RetainerSale) return;
            string txt = msg.ToString();
            if (!txt.Contains("sold for", StringComparison.OrdinalIgnoreCase)) return;

            try
            {
                var m = SaleRx.Match(txt);
                if (!m.Success) return;

                string pre = txt[..m.Index];
                if (pre.StartsWith("The ", StringComparison.OrdinalIgnoreCase)) pre = pre[4..];
                int idx = pre.IndexOf(" you put up", StringComparison.OrdinalIgnoreCase);
                string rawItem = (idx > 0 ? pre[..idx] : pre).Trim();

                string priceStr = m.Groups["price"].Value.Replace(",", "").Replace(".", "");
                if (int.TryParse(priceStr, out int price))
                {
                    var cfg = Plugin.Configuration;
                    cfg.Initialize();
                    cfg.Stats.TotalGilEarned += price;
                    cfg.Stats.TotalItemsSold++;

                    bool hq = rawItem.Contains("\uE03C") || txt.Contains("(HQ)");
                    string clean = Communicator.GetCleanItemName(rawItem);
                    string city = m.Groups["city"].Value;

                    cfg.Stats.SalesHistory.Add(new SaleRecord
                    {
                        ItemName = clean,
                        Price = price,
                        IsHq = hq,
                        City = city,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    });

                    if (cfg.Stats.SalesHistory.Count > 100) cfg.Stats.SalesHistory.RemoveAt(0);
                    cfg.Save();

                    DiscordSender.SendSaleNotification(clean, price, city, hq, cfg.Stats.TotalGilEarned, cfg.Stats.TotalItemsSold);
                }
            }
            catch (Exception ex)
            {
                if (!_regexErrorLogged) { Svc.Log.Error(ex, "Sales parse error"); _regexErrorLogged = true; }
            }
        }
    }
}