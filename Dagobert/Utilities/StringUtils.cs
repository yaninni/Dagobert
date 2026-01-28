using System;
using System.Buffers;
using System.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace Dagobert.Utilities;

public static class StringUtils
{
    public static string GetCleanItemName(string rawName)
    {
        if (string.IsNullOrEmpty(rawName)) return "Unknown Item";

        try
        {
            var se = SeString.Parse(Encoding.UTF8.GetBytes(rawName));

            string clean;
            if (se.Payloads.Count == 1 && se.Payloads[0] is TextPayload tp)
            {
                clean = tp.Text ?? "";
            }
            else
            {
                var sb = new StringBuilder(rawName.Length);
                foreach (var p in se.Payloads)
                {
                    if (p is TextPayload textP) sb.Append(textP.Text);
                }
                clean = sb.ToString();
            }

            if (string.IsNullOrEmpty(clean)) return "Unknown Item";

            ReadOnlySpan<char> span = clean.AsSpan();

            char[]? rented = null;
            Span<char> buffer = span.Length <= 128 ? stackalloc char[128] : (rented = ArrayPool<char>.Shared.Rent(span.Length));

            int writeIdx = 0;
            for (int i = 0; i < span.Length; i++)
            {
                char c = span[i];
                if (char.IsControl(c) || c == '\uE03C' || c == '\uE03B') continue;
                buffer[writeIdx++] = c;
            }

            ReadOnlySpan<char> processed = buffer.Slice(0, writeIdx);

            int startIdx = 0;
            while (startIdx < processed.Length)
            {
                char c = processed[startIdx];
                if (char.IsLetterOrDigit(c) || c == '(' || c == '[' || c == '{' || c == '<') break;
                startIdx++;
            }

            string result;
            if (startIdx >= processed.Length)
            {
                result = "";
            }
            else
            {
                result = processed.Slice(startIdx).Trim().ToString();
            }

            if (rented != null) ArrayPool<char>.Shared.Return(rented);
            return string.IsNullOrEmpty(result) ? "Unknown Item" : result;
        }
        catch { return "Unknown Item"; }
    }

    public static bool ContainsHqIcon(string text) => text.Contains('\uE03C');

    public static string FormatGil(long amount) => $"{amount:N0} gil";

    public static string FormatPercent(double value, int decimals) => value.ToString($"F{decimals}") + "%";
}
