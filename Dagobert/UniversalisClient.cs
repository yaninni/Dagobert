using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dagobert.Utilities;
using ECommons.DalamudServices;

namespace Dagobert;

public static class UniversalisClient
{
    private static readonly HttpClient _httpClient = new();
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private static readonly Dictionary<uint, UniversalisCacheEntry> _cache = new();
    private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    private const string ApiBaseUrl = "https://universalis.app/api/v2";

    // world to DC mapping <--- i hate this hardcoding but whatever its fiine for now
    private static readonly Dictionary<string, string> WorldToDatacenter = new(StringComparer.OrdinalIgnoreCase)
    {
        // North America - Aether
        ["Adamantoise"] = "Aether", ["Cactuar"] = "Aether", ["Faerie"] = "Aether",
        ["Gilgamesh"] = "Aether", ["Jenova"] = "Aether", ["Midgardsormr"] = "Aether",
        ["Sargatanas"] = "Aether", ["Siren"] = "Aether",
        // North America - Primal
        ["Behemoth"] = "Primal", ["Excalibur"] = "Primal", ["Exodus"] = "Primal",
        ["Famfrit"] = "Primal", ["Hyperion"] = "Primal", ["Lamia"] = "Primal",
        ["Leviathan"] = "Primal", ["Ultros"] = "Primal",
        // North America - Crystal
        ["Balmung"] = "Crystal", ["Brynhildr"] = "Crystal", ["Coeurl"] = "Crystal",
        ["Diabolos"] = "Crystal", ["Goblin"] = "Crystal", ["Malboro"] = "Crystal",
        ["Mateus"] = "Crystal", ["Zalera"] = "Crystal",
        // North America - Dynamis
        ["Cuchulainn"] = "Dynamis", ["Golem"] = "Dynamis", ["Halicarnassus"] = "Dynamis",
        ["Kraken"] = "Dynamis", ["Maduin"] = "Dynamis", ["Marilith"] = "Dynamis",
        ["Rafflesia"] = "Dynamis", ["Seraph"] = "Dynamis",
        // Europe - Chaos
        ["Cerberus"] = "Chaos", ["Louisoix"] = "Chaos", ["Moogle"] = "Chaos",
        ["Omega"] = "Chaos", ["Phantom"] = "Chaos", ["Ragnarok"] = "Chaos",
        ["Sagittarius"] = "Chaos", ["Spriggan"] = "Chaos",
        // Europe - Light
        ["Alpha"] = "Light", ["Lich"] = "Light", ["Odin"] = "Light",
        ["Phoenix"] = "Light", ["Raiden"] = "Light", ["Shiva"] = "Light",
        ["Twintania"] = "Light", ["Zodiark"] = "Light",
        // Japan - Elemental
        ["Aegis"] = "Elemental", ["Atomos"] = "Elemental", ["Carbuncle"] = "Elemental",
        ["Garuda"] = "Elemental", ["Gungnir"] = "Elemental", ["Kujata"] = "Elemental",
        ["Ramuh"] = "Elemental", ["Tonberry"] = "Elemental", ["Typhon"] = "Elemental",
        ["Unicorn"] = "Elemental",
        // Japan - Gaia
        ["Alexander"] = "Gaia", ["Bahamut"] = "Gaia", ["Durandal"] = "Gaia",
        ["Fenrir"] = "Gaia", ["Ifrit"] = "Gaia", ["Ridill"] = "Gaia",
        ["Tiamat"] = "Gaia", ["Ultima"] = "Gaia", ["Valefor"] = "Gaia",
        ["Yojimbo"] = "Gaia", ["Zeromus"] = "Gaia",
        // Japan - Mana
        ["Anima"] = "Mana", ["Asura"] = "Mana", ["Belias"] = "Mana",
        ["Chocobo"] = "Mana", ["Hades"] = "Mana", ["Ixion"] = "Mana",
        ["Mandragora"] = "Mana", ["Masamune"] = "Mana", ["Pandaemonium"] = "Mana",
        ["Shinryu"] = "Mana", ["Titan"] = "Mana",
        // Japan - Meteor
        ["Belial"] = "Meteor", ["Cuchulainn"] = "Meteor", ["Fenrir"] = "Meteor",
        ["Hecatoncheir"] = "Meteor", ["Maduin"] = "Meteor", ["Nephilim"] = "Meteor",
        ["Quetzalcoatl"] = "Meteor", ["Rubicante"] = "Meteor", ["Shinryu"] = "Meteor",
        ["Unicorn"] = "Meteor", ["Valefor"] = "Meteor", ["Yojimbo"] = "Meteor",
        ["Zeromus"] = "Meteor",
        // Oceania - Materia
        ["Bismarck"] = "Materia", ["Ravana"] = "Materia", ["Sephirot"] = "Materia",
        ["Sophia"] = "Materia", ["Zurvan"] = "Materia",
    };

    private static readonly Dictionary<string, string> DatacenterToRegion = new(StringComparer.OrdinalIgnoreCase)
    {
        // North America
        ["Aether"] = "NorthAmerica",
        ["Primal"] = "NorthAmerica",
        ["Crystal"] = "NorthAmerica",
        ["Dynamis"] = "NorthAmerica",
        // Europe
        ["Chaos"] = "Europe",
        ["Light"] = "Europe",
        // Japan
        ["Elemental"] = "Japan",
        ["Gaia"] = "Japan",
        ["Mana"] = "Japan",
        ["Meteor"] = "Japan",
        // Oceania
        ["Materia"] = "Oceania",
    };

    public static string? GetDatacenterForWorld(string worldName)
    {
        if (string.IsNullOrEmpty(worldName)) return null;
        return WorldToDatacenter.TryGetValue(worldName, out var dc) ? dc : null;
    }

    public static string? GetRegionForDatacenter(string datacenterName)
    {
        if (string.IsNullOrEmpty(datacenterName)) return null;
        return DatacenterToRegion.TryGetValue(datacenterName, out var region) ? region : null;
    }

    public static List<string> GetDatacentersInRegion(string datacenterName)
    {
        var region = GetRegionForDatacenter(datacenterName);
        if (string.IsNullOrEmpty(region)) return new List<string>();
        
        return DatacenterToRegion
            .Where(kvp => kvp.Value.Equals(region, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Key)
            .ToList();
    }

    public static async Task<UniversalisMarketData?> GetMarketData(uint itemId, string worldName, bool hqOnly = false)
    {
        if (_cache.TryGetValue(itemId, out var cached) && cached.Expiry > DateTime.Now)
        {
            return cached.Data;
        }

        await _semaphore.WaitAsync();
        try
        {
            string url = $"{ApiBaseUrl}/{worldName}/{itemId}";
            if (hqOnly) url += "?hq=true";

            var response = await _httpClient.GetStringAsync(url);
            var data = JsonSerializer.Deserialize<UniversalisMarketData>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (data != null)
            {
                if (_cache.Count > 100)
                {
                    var oldest = _cache.OrderBy(kvp => kvp.Value.Timestamp).First().Key;
                    _cache.Remove(oldest);
                }

                _cache[itemId] = new UniversalisCacheEntry
                {
                    Data = data,
                    Timestamp = DateTime.Now,
                    Expiry = DateTime.Now.Add(_cacheDuration)
                };
            }

            return data;
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"Failed to fetch Universalis data for item {itemId}: {ex.Message}");
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public static async Task<UniversalisMarketData?> GetDatacenterData(uint itemId, string datacenterName)
    {
        try
        {
            string url = $"{ApiBaseUrl}/{datacenterName}/{itemId}";
            var response = await _httpClient.GetStringAsync(url);
            return JsonSerializer.Deserialize<UniversalisMarketData>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"Failed to fetch datacenter data for item {itemId}: {ex.Message}");
            return null;
        }
    }

    public static async Task<List<CrossWorldListing>?> GetCrossWorldListings(uint itemId, string worldOrDatacenterName, int limit = 50)
    {
        try
        {
            string url = $"{ApiBaseUrl}/{worldOrDatacenterName}/{itemId}?entries={limit}";
            var response = await _httpClient.GetStringAsync(url);
            var data = JsonSerializer.Deserialize<UniversalisMarketData>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (data?.Listings == null) return null;

            return data.Listings.Select(l => new CrossWorldListing
            {
                PricePerUnit = l.PricePerUnit,
                Quantity = l.Quantity,
                TotalPrice = l.PricePerUnit * l.Quantity,
                Hq = l.Hq,
                WorldName = !string.IsNullOrEmpty(l.WorldName) ? l.WorldName : data.WorldName ?? worldOrDatacenterName,
                RetainerName = l.RetainerName,
                LastReviewTime = l.LastReviewTime
            }).ToList();
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"Failed to fetch cross-world listings for item {itemId}: {ex.Message}");
            return null;
        }
    }

    public static List<string> GetAllDatacenters()
    {
        return DatacenterToRegion.Keys.ToList();
    }

    private static readonly Dictionary<(uint ItemId, string Datacenter), CrossDcCacheEntry> _crossDcCache = new();
    private static readonly TimeSpan _crossDcCacheDuration = TimeSpan.FromMinutes(10);

    public static CrossDcData? GetCachedCrossDcData(uint itemId, string currentDatacenterName)
    {
        var allDatacenters = GetAllDatacenters();
        var otherDatacenters = allDatacenters
            .Where(dc => !dc.Equals(currentDatacenterName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var result = new CrossDcData
        {
            Region = "Global",
            CurrentDatacenter = currentDatacenterName,
            OtherDatacenterListings = new List<CrossDcListing>()
        };

        bool hasAllCached = true;
        foreach (var dc in otherDatacenters)
        {
            var key = (itemId, dc);
            if (_crossDcCache.TryGetValue(key, out var cached) && cached.Expiry > DateTime.Now)
            {
                result.OtherDatacenterListings.Add(cached.Data);
            }
            else
            {
                hasAllCached = false;
                break;
            }
        }

        return hasAllCached ? result : null;
    }

    public static async Task<CrossDcData?> GetCrossDcListings(uint itemId, string currentDatacenterName, int limit = 50, bool useCache = true)
    {
        try
        {
            if (useCache)
            {
                var cached = GetCachedCrossDcData(itemId, currentDatacenterName);
                if (cached != null)
                {
                    Svc.Log.Debug($"Using cached cross-DC data for item {itemId}");
                    return cached;
                }
            }

            var allDatacenters = GetAllDatacenters();
            var otherDatacenters = allDatacenters
                .Where(dc => !dc.Equals(currentDatacenterName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!otherDatacenters.Any())
            {
                return new CrossDcData
                {
                    Region = "Global",
                    CurrentDatacenter = currentDatacenterName,
                    OtherDatacenterListings = new List<CrossDcListing>()
                };
            }

            var result = new CrossDcData
            {
                Region = "Global",
                CurrentDatacenter = currentDatacenterName,
                OtherDatacenterListings = new List<CrossDcListing>()
            };

            var options = new ParallelOptions { MaxDegreeOfParallelism = 4 };
            var listingsLock = new object();

            await Parallel.ForEachAsync(otherDatacenters, options, async (dc, ct) =>
            {
                var cacheKey = (itemId, dc);
                if (useCache && _crossDcCache.TryGetValue(cacheKey, out var cachedEntry) && cachedEntry.Expiry > DateTime.Now)
                {
                    lock (listingsLock)
                    {
                        result.OtherDatacenterListings.Add(cachedEntry.Data);
                    }
                    return;
                }

                try
                {
                    string url = $"{ApiBaseUrl}/{dc}/{itemId}?entries={limit}";
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var response = await _httpClient.GetStringAsync(url, cts.Token);
                    var data = JsonSerializer.Deserialize<UniversalisMarketData>(response, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    CrossDcListing dcListing;
                    if (data?.Listings != null && data.Listings.Any())
                    {
                        var listings = data.Listings.Select(l => new CrossWorldListing
                        {
                            PricePerUnit = l.PricePerUnit,
                            Quantity = l.Quantity,
                            TotalPrice = l.PricePerUnit * l.Quantity,
                            Hq = l.Hq,
                            WorldName = !string.IsNullOrEmpty(l.WorldName) ? l.WorldName : data.WorldName ?? dc,
                            RetainerName = l.RetainerName,
                            LastReviewTime = l.LastReviewTime
                        }).ToList();

                        dcListing = new CrossDcListing
                        {
                            DatacenterName = dc,
                            Region = GetRegionForDatacenter(dc) ?? "Unknown",
                            Listings = listings,
                            MinPrice = listings.Min(l => l.PricePerUnit),
                            MaxPrice = listings.Max(l => l.PricePerUnit),
                            AvgPrice = (int)listings.Average(l => l.PricePerUnit),
                            ListingCount = listings.Count
                        };
                    }
                    else
                    {
                        dcListing = new CrossDcListing
                        {
                            DatacenterName = dc,
                            Region = GetRegionForDatacenter(dc) ?? "Unknown",
                            Listings = new List<CrossWorldListing>(),
                            MinPrice = 0,
                            MaxPrice = 0,
                            AvgPrice = 0,
                            ListingCount = 0
                        };
                    }

                    if (useCache)
                    {
                        lock (_crossDcCache)
                        {
                            if (_crossDcCache.Count > 500)
                            {
                                var oldest = _crossDcCache.OrderBy(kvp => kvp.Value.Timestamp).First().Key;
                                _crossDcCache.Remove(oldest);
                            }

                            _crossDcCache[cacheKey] = new CrossDcCacheEntry
                            {
                                Data = dcListing,
                                Timestamp = DateTime.Now,
                                Expiry = DateTime.Now.Add(_crossDcCacheDuration)
                            };
                        }
                    }

                    lock (listingsLock)
                    {
                        result.OtherDatacenterListings.Add(dcListing);
                    }
                }
                catch (Exception ex)
                {
                    Svc.Log.Debug($"Failed to fetch data for datacenter {dc}: {ex.Message}");
                    lock (listingsLock)
                    {
                        result.OtherDatacenterListings.Add(new CrossDcListing
                        {
                            DatacenterName = dc,
                            Region = GetRegionForDatacenter(dc) ?? "Unknown",
                            Listings = new List<CrossWorldListing>(),
                            MinPrice = 0,
                            MaxPrice = 0,
                            AvgPrice = 0,
                            ListingCount = 0
                        });
                    }
                }
            });

            return result;
        }
        catch (Exception ex)
        {
            Svc.Log.Warning($"Failed to fetch cross-DC listings for item {itemId}: {ex.Message}");
            return null;
        }
    }

    public static void ClearCrossDcCache() => _crossDcCache.Clear();

    public static PriceSuggestion GetPriceSuggestion(UniversalisMarketData data, bool isHq)
    {
        var listings = isHq 
            ? data.Listings?.Where(l => l.Hq).ToList() 
            : data.Listings;

        if (listings == null || !listings.Any())
        {
            return new PriceSuggestion { HasData = false };
        }

        var prices = listings.Select(l => l.PricePerUnit).ToList();
        
        return new PriceSuggestion
        {
            HasData = true,
            CurrentWorldMin = prices.Min(),
            CurrentWorldAvg = (int)prices.Average(),
            CurrentWorldMedian = MathUtils.GetMedian(prices),
            CurrentWorldMax = prices.Max(),
            CurrentWorldListingCount = listings.Count,
            CrossWorldMin = data.MinPrice ?? 0,
            CrossWorldAvg = data.AveragePrice ?? 0,
            CrossWorldMedian = data.MedianPrice ?? 0,
            SalesPerDay = data.RegularSaleVelocity ?? 0,
            LastUploadTime = data.LastUploadTime
        };
    }

    public static void ClearCache() => _cache.Clear();
}

public class UniversalisCacheEntry
{
    public UniversalisMarketData Data { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public DateTime Expiry { get; set; }
}

public class UniversalisMarketData
{
    public uint ItemId { get; set; }
    public string WorldName { get; set; } = "";
    public string DatacenterName { get; set; } = "";
    public List<UniversalisListing>? Listings { get; set; }
    public List<UniversalisSale>? RecentHistory { get; set; }
    [System.Text.Json.Serialization.JsonConverter(typeof(FlexibleLongConverter))]
    public long? LastUploadTime { get; set; }
    [System.Text.Json.Serialization.JsonConverter(typeof(FlexibleIntConverter))]
    public int? MinPrice { get; set; }
    [System.Text.Json.Serialization.JsonConverter(typeof(FlexibleIntConverter))]
    public int? MinPriceNq { get; set; }
    [System.Text.Json.Serialization.JsonConverter(typeof(FlexibleIntConverter))]
    public int? MinPriceHq { get; set; }
    [System.Text.Json.Serialization.JsonConverter(typeof(FlexibleIntConverter))]
    public int? MaxPrice { get; set; }
    [System.Text.Json.Serialization.JsonConverter(typeof(FlexibleIntConverter))]
    public int? AveragePrice { get; set; }
    [System.Text.Json.Serialization.JsonConverter(typeof(FlexibleIntConverter))]
    public int? AveragePriceNq { get; set; }
    [System.Text.Json.Serialization.JsonConverter(typeof(FlexibleIntConverter))]
    public int? AveragePriceHq { get; set; }
    [System.Text.Json.Serialization.JsonConverter(typeof(FlexibleIntConverter))]
    public int? MedianPrice { get; set; }
    [System.Text.Json.Serialization.JsonConverter(typeof(FlexibleIntConverter))]
    public int? MedianPriceNq { get; set; }
    [System.Text.Json.Serialization.JsonConverter(typeof(FlexibleIntConverter))]
    public int? MedianPriceHq { get; set; }
    public float? RegularSaleVelocity { get; set; }
    public float? NqSaleVelocity { get; set; }
    public float? HqSaleVelocity { get; set; }
}

public class FlexibleLongConverter : System.Text.Json.Serialization.JsonConverter<long?>
{
    public override long? Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType == System.Text.Json.JsonTokenType.Null)
            return null;
        if (reader.TokenType == System.Text.Json.JsonTokenType.Number)
        {
            if (reader.TryGetInt64(out long l))
                return l;
            if (reader.TryGetInt32(out int i))
                return i;
        }
        if (reader.TokenType == System.Text.Json.JsonTokenType.String)
        {
            var str = reader.GetString();
            if (long.TryParse(str, out long result))
                return result;
        }
        return null;
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, long? value, System.Text.Json.JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteNumberValue(value.Value);
        else
            writer.WriteNullValue();
    }
}

public class FlexibleIntConverter : System.Text.Json.Serialization.JsonConverter<int?>
{
    public override int? Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType == System.Text.Json.JsonTokenType.Null)
            return null;
        if (reader.TokenType == System.Text.Json.JsonTokenType.Number)
        {
            if (reader.TryGetInt32(out int i))
                return i;
            if (reader.TryGetInt64(out long l) && l >= int.MinValue && l <= int.MaxValue)
                return (int)l;
        }
        if (reader.TokenType == System.Text.Json.JsonTokenType.String)
        {
            var str = reader.GetString();
            if (int.TryParse(str, out int result))
                return result;
        }
        return null;
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, int? value, System.Text.Json.JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteNumberValue(value.Value);
        else
            writer.WriteNullValue();
    }
}

public class UniversalisListing
{
    public int PricePerUnit { get; set; }
    public int Quantity { get; set; }
    public bool Hq { get; set; }
    public string RetainerName { get; set; } = "";
    public string WorldName { get; set; } = "";
    public long LastReviewTime { get; set; }
}

public class UniversalisSale
{
    public int PricePerUnit { get; set; }
    public int Quantity { get; set; }
    public bool Hq { get; set; }
    public long Timestamp { get; set; }
    public string BuyerName { get; set; } = "";
}

public class CrossWorldListing
{
    public int PricePerUnit { get; set; }
    public int Quantity { get; set; }
    public int TotalPrice { get; set; }
    public bool Hq { get; set; }
    public string WorldName { get; set; } = "";
    public string RetainerName { get; set; } = "";
    public long LastReviewTime { get; set; }

    public int PriceToListForTarget(int targetAmount) => (int)(targetAmount / 0.95);
    public int TaxOnTotal => (int)(TotalPrice * 0.05);
    public int NetAfterTax => (int)(TotalPrice * 0.95);
}

public class CrossDcData
{
    public string Region { get; set; } = "";
    public string CurrentDatacenter { get; set; } = "";
    public List<CrossDcListing> OtherDatacenterListings { get; set; } = new();
    
    public int OverallMinPrice => OtherDatacenterListings.Any(l => l.ListingCount > 0) 
        ? OtherDatacenterListings.Where(l => l.ListingCount > 0).Min(l => l.MinPrice) 
        : 0;
    public int OverallMaxPrice => OtherDatacenterListings.Any(l => l.ListingCount > 0) 
        ? OtherDatacenterListings.Where(l => l.ListingCount > 0).Max(l => l.MaxPrice) 
        : 0;
    public int OverallAvgPrice => OtherDatacenterListings.Any(l => l.ListingCount > 0)
        ? (int)OtherDatacenterListings.Where(l => l.ListingCount > 0).Average(l => l.AvgPrice)
        : 0;
    public int TotalListingCount => OtherDatacenterListings.Sum(l => l.ListingCount);
}

public class CrossDcCacheEntry
{
    public CrossDcListing Data { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public DateTime Expiry { get; set; }
}

public class CrossDcListing
{
    public string DatacenterName { get; set; } = "";
    public string Region { get; set; } = "";
    public List<CrossWorldListing> Listings { get; set; } = new();
    public int MinPrice { get; set; }
    public int MaxPrice { get; set; }
    public int AvgPrice { get; set; }
    public int ListingCount { get; set; }
}

public class PriceSuggestion
{
    public bool HasData { get; set; }
    
    public int CurrentWorldMin { get; set; }
    public int CurrentWorldAvg { get; set; }
    public int CurrentWorldMedian { get; set; }
    public int CurrentWorldMax { get; set; }
    public int CurrentWorldListingCount { get; set; }
    public int CrossWorldMin { get; set; }
    public int CrossWorldAvg { get; set; }
    public int CrossWorldMedian { get; set; }
    
    public float SalesPerDay { get; set; }
    public long? LastUploadTime { get; set; }

    public string GetRecommendation()
    {
        if (!HasData) return "No market data available";
        
        if (CrossWorldMin < CurrentWorldMin * 0.8)
        {
            return $"Price at {CurrentWorldMin - 1} gil (cross-world is cheaper)";
        }
        
        if (SalesPerDay > 5)
        {
            return $"Price at {CurrentWorldMin - 1} gil (high demand)";
        }
        
        return $"Price at {CurrentWorldMedian} gil (market rate)";
    }
}
