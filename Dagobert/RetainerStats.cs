using System.Collections.Generic;
using System.Text;
//TOOD this needs to be reworked perhaps?
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

        public void AddChange(string itemName, int oldPrice, int newPrice, float cutPercent)
        {
            _totalChecked++;
            _changes.Add($"• **{itemName}**: ~~{oldPrice:N0}~~ → **{newPrice:N0}** `({cutPercent:F2}%)`");
        }

        public void AddSkip(string itemName, string reason)
        {
            _totalChecked++;
            _skips.Add($"• {itemName}: *{reason}*");
        }

        public void AddError(string itemName, string error)
        {
            _totalChecked++;
            _skips.Add($"• ⚠️ {itemName}: *{error}*");
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