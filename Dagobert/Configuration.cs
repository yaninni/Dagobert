using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace Dagobert;

public enum UndercutMode { FixedAmount, Percentage }
public enum PricingPersonality { Standard, CleanNumbers, PoliteMatch }

[Serializable]
public class SaleRecord
{
    public string ItemName { get; set; } = "";
    public int Price { get; set; }
    public bool IsHq { get; set; }
    public long Timestamp { get; set; }
    public string City { get; set; } = "";
}

[Serializable]
public class ItemSettings
{
    public int MinPrice { get; set; } = 0;
    public bool Ignore { get; set; } = false;
    public bool MatchOnly { get; set; } = false;
}

[Serializable]
public class GlobalStats
{
    public long TotalGilEarned { get; set; } = 0;
    public int TotalItemsSold { get; set; } = 0;
    public int TotalUndercutsMade { get; set; } = 0;
    public List<SaleRecord> SalesHistory { get; set; } = new();
}

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 4;

    public bool HQ { get; set; } = true;
    public int GetMBPricesDelayMin { get; set; } = 2500;
    public int GetMBPricesDelayMax { get; set; } = 3500;
    public int MarketBoardKeepOpenMin { get; set; } = 1000;
    public int MarketBoardKeepOpenMax { get; set; } = 2000;
    public DelayStrategy DelayStrategy { get; set; } = DelayStrategy.Uniform;
    public bool ShowRandomnessGraph { get; set; } = false;
    public bool EnableFatigue { get; set; } = true;
    public bool EnableAdvancedHumanization { get; set; } = false;
    public bool EnableFittsLaw { get; set; } = false;
    public bool EnableMisclicks { get; set; } = false;
    public bool EnableComparativeDoubt { get; set; } = false;
    public PricingPersonality PricingPersonality { get; set; } = PricingPersonality.Standard;
    public UndercutMode UndercutMode { get; set; } = UndercutMode.FixedAmount;
    public int UndercutAmount { get; set; } = 1;
    public float MaxUndercutPercentage { get; set; } = 100.0f;
    public bool UndercutSelf { get; set; } = false;
    public bool SmartOutlierProtection { get; set; } = true;
    public bool MatchStackSize { get; set; } = true;
    public Dictionary<uint, ItemSettings> ItemConfigs { get; set; } = new();
    public GlobalStats Stats { get; set; } = new();
    public bool EnableDiscordLogging { get; set; } = false;
    public string DiscordWebhookUrl { get; set; } = "";
    public bool StopOnTell { get; set; } = true;
    public bool MouseEntropyCheck { get; set; } = true;

    // --- UI ---
    public bool ShowErrorsInChat { get; set; } = true;
    public bool ShowPriceAdjustmentsMessages { get; set; } = true;
    public bool ShowRetainerNames { get; set; } = true;

    public const string ALL_DISABLED_SENTINEL = "__ALL_DISABLED__";
    public HashSet<string> EnabledRetainerNames { get; set; } = [];
    public List<string> LastKnownRetainerNames { get; set; } = [];

    public void Initialize() { if (Stats == null) Stats = new GlobalStats(); }
    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}