using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dagobert.Utilities;

namespace Dagobert
{
    public static class DiscordSender
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private static bool _disposed = false;

        public static void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _httpClient.Dispose();
            _semaphore.Dispose();
        }

        public static void SendLog(string message)
        {
            if (!Plugin.Configuration.EnableDiscordLogging || string.IsNullOrWhiteSpace(Plugin.Configuration.DiscordWebhookUrl))
                return;

            _ = SendPayloadAsync(new { content = message, username = "Dagobert" }, CancellationToken.None);
        }

        public static void SendAlert(string senderName, string messageContent)
        {
            if (!Plugin.Configuration.EnableDiscordLogging || string.IsNullOrWhiteSpace(Plugin.Configuration.DiscordWebhookUrl))
                return;

            string alertMsg = $"**EMERGENCY STOP** @everyone\n**From:** {senderName}\n**Message:** `{messageContent}`";
            _ = SendPayloadAsync(new { content = alertMsg, username = "Dagobert" }, CancellationToken.None);
        }

        public static void SendSaleNotification(string itemName, uint itemId, int price, string city, bool isHq, long totalEarned, int totalSold)
        {
            if (!Plugin.Configuration.EnableDiscordLogging || string.IsNullOrWhiteSpace(Plugin.Configuration.DiscordWebhookUrl))
                return;

            var payload = new
            {
                username = "Dagobert Sales",
                embeds = new[]
                {
                    new
                    {
                        title = "💰 Item Sold!",
                        color = 5763719,
                        fields = new[]
                        {
                            new { name = "Item", value = $"[{itemName}]({ItemUtils.GetGarlandToolsLink(itemId)}) {(isHq ? "(HQ)" : "")}", inline = true },
                            new { name = "Price", value = $"{price:N0} gil", inline = true },
                            new { name = "Market", value = city, inline = true },
                            new { name = "Lifetime Stats", value = $"Total Earned: {totalEarned:N0} gil\nItems Sold: {totalSold:N0}", inline = false }
                        },
                        footer = new { text = $"Dagobert | {DateTime.Now:g}" }
                    }
                }
            };

            _ = SendPayloadAsync(payload, CancellationToken.None);
        }

        private static async Task SendPayloadAsync(object payload, CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                string json = JsonSerializer.Serialize(payload);
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(Plugin.Configuration.DiscordWebhookUrl, httpContent, cancellationToken);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // ignore silent
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Discord webhook error: {ex.Message}");
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
