using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
// TODO this needs to be reworked/improved HQ items amongst other string specific things are cooked
namespace Dagobert
{
    public static class DiscordSender
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public static void SendLog(string message)
        {
            if (!Plugin.Configuration.EnableDiscordLogging || string.IsNullOrWhiteSpace(Plugin.Configuration.DiscordWebhookUrl))
                return;

            Task.Run(async () => await SendPayloadAsync(new { content = message, username = "Dagobert" }));
        }

        public static void SendAlert(string senderName, string messageContent)
        {
            if (!Plugin.Configuration.EnableDiscordLogging || string.IsNullOrWhiteSpace(Plugin.Configuration.DiscordWebhookUrl))
                return;

            string alertMsg = $"**EMERGENCY STOP** @everyone\n**From:** {senderName}\n**Message:** `{messageContent}`";
            Task.Run(async () => await SendPayloadAsync(new { content = alertMsg, username = "Dagobert" }));
        }

        public static void SendSaleNotification(string itemName, int price, string city, bool isHq, long totalEarned, int totalSold)
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
                            new { name = "Item", value = $"{itemName} {(isHq ? "(HQ)" : "")}", inline = true },
                            new { name = "Price", value = $"{price:N0} gil", inline = true },
                            new { name = "Market", value = city, inline = true },
                            new { name = "Lifetime Stats", value = $"Total Earned: {totalEarned:N0} gil\nItems Sold: {totalSold:N0}", inline = false }
                        },
                        footer = new { text = $"Dagobert | {DateTime.Now:g}" }
                    }
                }
            };

            Task.Run(async () => await SendPayloadAsync(payload));
        }

        private static async Task SendPayloadAsync(object payload)
        {
            await _semaphore.WaitAsync();
            try
            {
                string json = JsonSerializer.Serialize(payload);
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(Plugin.Configuration.DiscordWebhookUrl, httpContent);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    await Task.Delay(1000);
                }
            }
            catch { 

            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}