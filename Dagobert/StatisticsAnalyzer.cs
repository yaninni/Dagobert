using System;
using System.Collections.Generic;
using System.Linq;
using ECommons.DalamudServices;

namespace Dagobert;

public static class StatisticsAnalyzer
{
    public static double GetAverageSaleTime(uint itemId)
    {
        var sales = Plugin.Configuration.Stats.SalesHistory
            .Where(s => s.ItemId == itemId)
            .OrderBy(s => s.Timestamp)
            .ToList();

        if (sales.Count < 2) return 0;

        var timeDifferences = new List<double>();
        for (int i = 1; i < sales.Count; i++)
        {
            var diff = sales[i].Timestamp - sales[i - 1].Timestamp;
            timeDifferences.Add(diff / 3600.0); // Convert to hours
        }

        return timeDifferences.Count > 0 ? timeDifferences.Average() : 0;
    }

    public static double GetOverallAverageSaleTime()
    {
        var groupedSales = Plugin.Configuration.Stats.SalesHistory
            .GroupBy(s => s.ItemId)
            .Where(g => g.Count() >= 2)
            .ToList();

        if (!groupedSales.Any()) return 0;

        var allAverages = groupedSales
            .Select(g => GetAverageSaleTime(g.Key))
            .Where(avg => avg > 0)
            .ToList();

        return allAverages.Count > 0 ? allAverages.Average() : 0;
    }

    public static List<ItemPerformance> GetBestSellingItems(int topN = 10)
    {
        return Plugin.Configuration.Stats.SalesHistory
            .GroupBy(s => s.ItemId)
            .Select(g => new ItemPerformance
            {
                ItemId = g.Key,
                ItemName = g.First().ItemName,
                TotalSold = g.Count(),
                TotalRevenue = g.Sum(s => s.Price),
                AveragePrice = (int)g.Average(s => s.Price),
                LastSold = g.Max(s => s.Timestamp)
            })
            .OrderByDescending(x => x.TotalSold)
            .Take(topN)
            .ToList();
    }

    public static List<ItemPerformance> GetWorstPerformingItems(int topN = 10)
    {
        return Plugin.Configuration.Stats.SalesHistory
            .GroupBy(s => s.ItemId)
            .Select(g => new ItemPerformance
            {
                ItemId = g.Key,
                ItemName = g.First().ItemName,
                TotalSold = g.Count(),
                TotalRevenue = g.Sum(s => s.Price),
                AveragePrice = (int)g.Average(s => s.Price),
                LastSold = g.Max(s => s.Timestamp),
                AverageSaleTime = GetAverageSaleTime(g.Key)
            })
            .OrderBy(x => x.TotalSold)
            .ThenByDescending(x => x.AverageSaleTime)
            .Take(topN)
            .ToList();
    }

    public static List<ItemPerformance> GetTopRevenueItems(int topN = 10)
    {
        return Plugin.Configuration.Stats.SalesHistory
            .GroupBy(s => s.ItemId)
            .Select(g => new ItemPerformance
            {
                ItemId = g.Key,
                ItemName = g.First().ItemName,
                TotalSold = g.Count(),
                TotalRevenue = g.Sum(s => s.Price),
                AveragePrice = (int)g.Average(s => s.Price),
                LastSold = g.Max(s => s.Timestamp)
            })
            .OrderByDescending(x => x.TotalRevenue)
            .Take(topN)
            .ToList();
    }

    public static List<ItemPerformance> GetStaleItems(int daysThreshold = 7)
    {
        var threshold = DateTimeOffset.UtcNow.AddDays(-daysThreshold).ToUnixTimeSeconds();
        
        return Plugin.Configuration.Stats.SalesHistory
            .GroupBy(s => s.ItemId)
            .Select(g => new ItemPerformance
            {
                ItemId = g.Key,
                ItemName = g.First().ItemName,
                TotalSold = g.Count(),
                TotalRevenue = g.Sum(s => s.Price),
                AveragePrice = (int)g.Average(s => s.Price),
                LastSold = g.Max(s => s.Timestamp)
            })
            .Where(x => x.LastSold < threshold)
            .OrderBy(x => x.LastSold)
            .ToList();
    }

    public static double GetPriceVolatility(uint itemId)
    {
        var prices = Plugin.Configuration.Stats.SalesHistory
            .Where(s => s.ItemId == itemId)
            .Select(s => (double)s.Price)
            .ToList();

        if (prices.Count < 2) return 0;

        var avg = prices.Average();
        var sumSquares = prices.Sum(p => Math.Pow(p - avg, 2));
        return Math.Sqrt(sumSquares / prices.Count);
    }

    public static TrendAnalysis GetSalesTrend(int days = 7)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days).ToUnixTimeSeconds();
        var recentSales = Plugin.Configuration.Stats.SalesHistory
            .Where(s => s.Timestamp >= cutoff)
            .ToList();

        var previousCutoff = DateTimeOffset.UtcNow.AddDays(-days * 2).ToUnixTimeSeconds();
        var previousSales = Plugin.Configuration.Stats.SalesHistory
            .Where(s => s.Timestamp >= previousCutoff && s.Timestamp < cutoff)
            .ToList();

        var recentRevenue = recentSales.Sum(s => s.Price);
        var previousRevenue = previousSales.Sum(s => s.Price);

        double percentChange = previousRevenue > 0 
            ? ((recentRevenue - previousRevenue) / (double)previousRevenue) * 100 
            : 0;

        return new TrendAnalysis
        {
            PeriodRevenue = recentRevenue,
            PeriodSales = recentSales.Count,
            PreviousPeriodRevenue = previousRevenue,
            PreviousPeriodSales = previousSales.Count,
            RevenueChangePercent = percentChange,
            IsPositiveTrend = percentChange > 0
        };
    }
}

public class ItemPerformance
{
    public uint ItemId { get; set; }
    public string ItemName { get; set; } = "";
    public int TotalSold { get; set; }
    public long TotalRevenue { get; set; }
    public int AveragePrice { get; set; }
    public long LastSold { get; set; }
    public double AverageSaleTime { get; set; }

    public DateTime LastSoldDateTime => DateTimeOffset.FromUnixTimeSeconds(LastSold).LocalDateTime;
}

public class TrendAnalysis
{
    public long PeriodRevenue { get; set; }
    public int PeriodSales { get; set; }
    public long PreviousPeriodRevenue { get; set; }
    public int PreviousPeriodSales { get; set; }
    public double RevenueChangePercent { get; set; }
    public bool IsPositiveTrend { get; set; }
}
