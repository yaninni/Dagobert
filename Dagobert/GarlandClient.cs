using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Linq;

namespace Dagobert;

public class GarlandClient
{
    private static readonly HttpClient Http = new();
    private static readonly Dictionary<uint, (GarlandResponse Data, DateTime Expiry)> Cache = new();
    private static readonly Dictionary<uint, DateTime> CacheTimestamps = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);
    private const int MaxCacheSize = 100;

    public static async Task<GarlandResponse?> GetItem(uint itemId)
    {
        // 1. Check Cache
        if (Cache.TryGetValue(itemId, out var cached) && cached.Expiry > DateTime.Now)
        {
            return cached.Data;
        }

        try
        {
            string url = $"https://www.garlandtools.org/db/doc/item/en/3/{itemId}.json";
            var response = await Http.GetStringAsync(url);
            
            var options = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new FlexibleStringConverter());
            
            var result = JsonSerializer.Deserialize<GarlandResponse>(response, options);
            if (result != null)
            {
                Cache[itemId] = (result, DateTime.Now.Add(CacheDuration));
                CacheTimestamps[itemId] = DateTime.Now;
                
                if (Cache.Count > MaxCacheSize)
                {
                    var entriesToRemove = CacheTimestamps
                        .OrderBy(kvp => kvp.Value)
                        .Take(MaxCacheSize / 10)
                        .Select(kvp => kvp.Key)
                        .ToList();
                    
                    foreach (var key in entriesToRemove)
                    {
                        Cache.Remove(key);
                        CacheTimestamps.Remove(key);
                    }
                }
            }
            
            return result;
        }
        catch
        {
            return null;
        }
    }

    #region Models
    public class FlexibleStringConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetInt64(out long l)) return l.ToString();
                if (reader.TryGetDouble(out double d)) return d.ToString();
                return "0";
            }
            if (reader.TokenType == JsonTokenType.String) return reader.GetString();
            return null;
        }
        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
    }

    public class GarlandResponse
    {
        public GarlandItem? Item { get; set; }
        public List<GarlandPartial>? Partials { get; set; }
        public List<GarlandItem>? Ingredients { get; set; }
    }

    public class GarlandItem
    {
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int Ilvl { get; set; }
        public int Price { get; set; }
        
        [JsonPropertyName("sell_price")]
        public int SellPrice { get; set; }
        public int StackSize { get; set; }
        public int Tradeable { get; set; }
        
        [JsonPropertyName("icon")]
        public uint Icon { get; set; }

        [JsonPropertyName("attr")]
        public Dictionary<string, object>? Attributes { get; set; }

        [JsonPropertyName("attr_hq")]
        public Dictionary<string, object>? AttributesHq { get; set; }

        public List<object>? Drops { get; set; }
        public List<object>? Instances { get; set; }
        public List<object>? Nodes { get; set; }
        public List<object>? Quests { get; set; }
        public List<object>? Ventures { get; set; }
        public List<object>? Seeds { get; set; }
        public List<object>? Voyages { get; set; }
        
        [JsonPropertyName("usedInQuest")]
        public List<object>? UsedInQuest { get; set; }
        
        [JsonPropertyName("requiredByLeves")]
        public List<object>? RequiredByLeves { get; set; }
        
        [JsonPropertyName("vendors")]
        public List<object>? Vendors { get; set; }
        
        [JsonPropertyName("ingredient_of")]
        public Dictionary<string, int>? IngredientOf { get; set; }
        
        [JsonPropertyName("craft")]
        public List<CraftRecipe>? Craft { get; set; }
        
        public List<TradeShop>? TradeShops { get; set; }
    }

    public class CraftRecipe
    {
        public int Job { get; set; }
        public int Lvl { get; set; }
        public List<Ingredient>? Ingredients { get; set; }
    }

    public class Ingredient
    {
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? Id { get; set; }
        public int Amount { get; set; }
    }

    public class TradeShop
    {
        public string? Shop { get; set; }
        public List<long>? Npcs { get; set; }
        public List<TradeListing>? Listings { get; set; }
    }

    public class TradeListing
    {
        public List<TradeItem>? Item { get; set; }
        public List<TradeItem>? Currency { get; set; }
    }

    public class TradeItem
    {
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? Id { get; set; }
        public int Amount { get; set; }
    }

    public class GarlandPartial
    {
        public string? Type { get; set; }
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? Id { get; set; }
        public PartialObj? Obj { get; set; }
    }

    public class PartialObj
    {
        public string? N { get; set; }
        public string? Name { get; set; }
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? L { get; set; }
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string? Z { get; set; }
    }
    #endregion
}
