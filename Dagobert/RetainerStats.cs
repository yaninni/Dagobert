using System.Collections.Generic;
using System.Text;
using Dagobert.Utilities;

namespace Dagobert
{
    public class RetainerStats
    {
        public string RetainerName { get; private set; }
        private List<string> _changes = new();
        private List<string> _skips = new();
        private int _totalChecked = 0;

        public RetainerStats(string retainerName)
        {
            RetainerName = retainerName;
        }

        public void AddChange(string itemName, uint itemId, int oldPrice, int newPrice, float cutPercent)
        {
            _totalChecked++;
            string link = ItemUtils.GetGarlandToolsLink(itemId);
            _changes.Add($"• **[{itemName}]({link})**: ~~{oldPrice:N0}~~ → **{newPrice:N0}** `({cutPercent:F2}%)`");
        }

        public void AddSkip(string itemName, uint itemId, string reason)
        {
            _totalChecked++;
            string link = ItemUtils.GetGarlandToolsLink(itemId);
            _skips.Add($"• [{itemName}]({link}): *{reason}*");
        }

        public void AddError(string itemName, uint itemId, string error)
        {
            _totalChecked++;
            string link = ItemUtils.GetGarlandToolsLink(itemId);
            _skips.Add($"• ⚠️ [{itemName}]({link}): *{error}*");
        }

        public string BuildReport()
        {
            var sb = new StringBuilder();

            sb.AppendLine($"📊 **Summary**: {_totalChecked} items | ✅ {_changes.Count} adjusted | ⏭️ {_skips.Count} unchanged");
            sb.AppendLine();

            if (_changes.Count > 0)
            {
                sb.AppendLine("**PRICE ADJUSTMENTS**");
                foreach (var line in _changes)
                    sb.AppendLine(line);
                sb.AppendLine();
            }

            if (_skips.Count > 0)
            {
                sb.AppendLine("**UNCHANGED / SKIPPED**");
                foreach (var line in _skips)
                    sb.AppendLine(line);
            }

            return sb.ToString();
        }
    }
}