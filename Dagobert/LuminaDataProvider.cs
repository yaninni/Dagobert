using System;
using System.Collections.Generic;
using System.Linq;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;

namespace Dagobert;

public static class LuminaDataProvider
{
    private static bool _isIndexed = false;
    private static readonly Dictionary<uint, List<Recipe>> RecipeIndex = new();
    private static readonly Dictionary<uint, List<uint>> ItemToGilShops = new();
    private static readonly Dictionary<uint, List<GatheringPoint>> GatheringIndex = new();

    public static void Initialize()
    {
        if (_isIndexed) return;

        var recipeSheet = Svc.Data.GetExcelSheet<Recipe>();
        if (recipeSheet != null)
        {
            foreach (var recipe in recipeSheet)
            {
                var resultId = recipe.ItemResult.RowId;
                if (resultId == 0) continue;
                
                if (!RecipeIndex.TryGetValue(resultId, out var list))
                {
                    list = new List<Recipe>();
                    RecipeIndex[resultId] = list;
                }
                list.Add(recipe);
            }
        }

        var gilShopItemSheet = Svc.Data.GetSubrowExcelSheet<GilShopItem>();
        if (gilShopItemSheet != null)
        {
            foreach (var mainRow in gilShopItemSheet)
            {
                foreach (var itemSubRow in mainRow)
                {
                    var itemId = itemSubRow.Item.RowId;
                    if (itemId == 0) continue;
                    
                    if (!ItemToGilShops.TryGetValue(itemId, out var shopList))
                    {
                        shopList = new List<uint>();
                        ItemToGilShops[itemId] = shopList;
                    }
                    if (!shopList.Contains(mainRow.RowId))
                        shopList.Add(mainRow.RowId);
                }
            }
        }

        var gpSheet = Svc.Data.GetExcelSheet<GatheringPoint>();
        var gpbSheet = Svc.Data.GetExcelSheet<GatheringPointBase>();
        var giSheet = Svc.Data.GetExcelSheet<GatheringItem>();
        
        if (gpSheet != null && gpbSheet != null && giSheet != null)
        {
            var baseToPoints = new Dictionary<uint, List<GatheringPoint>>();
            foreach (var gp in gpSheet)
            {
                if (gp.GatheringPointBase.RowId == 0) continue;
                if (!baseToPoints.TryGetValue(gp.GatheringPointBase.RowId, out var points))
                {
                    points = new List<GatheringPoint>();
                    baseToPoints[gp.GatheringPointBase.RowId] = points;
                }
                points.Add(gp);
            }

            var giToItemId = new Dictionary<uint, uint>();
            foreach (var giRow in giSheet)
            {
                if (giRow.Item.RowId > 0)
                    giToItemId[giRow.RowId] = giRow.Item.RowId;
            }

            foreach (var gpb in gpbSheet)
            {
                if (!baseToPoints.TryGetValue(gpb.RowId, out var points)) continue;
                
                foreach (var itemRef in gpb.Item)
                {
                    if (itemRef.RowId == 0) continue;
                    
                    if (giToItemId.TryGetValue(itemRef.RowId, out var itemId))
                    {
                        if (!GatheringIndex.TryGetValue(itemId, out var pointList))
                        {
                            pointList = new List<GatheringPoint>();
                            GatheringIndex[itemId] = pointList;
                        }
                        pointList.AddRange(points);
                    }
                }
            }
        }

        _isIndexed = true;
    }

    public static List<Recipe> GetRecipesForItem(uint itemId)
    {
        Initialize();
        return RecipeIndex.TryGetValue(itemId, out var recipes) ? recipes : new List<Recipe>();
    }

    public static List<uint> GetGilShopsForItem(uint itemId)
    {
        Initialize();
        return ItemToGilShops.TryGetValue(itemId, out var shops) ? shops : new List<uint>();
    }

    public static List<GatheringPoint> GetGatheringPointsForItem(uint itemId)
    {
        Initialize();
        return GatheringIndex.TryGetValue(itemId, out var points) ? points.DistinctBy(p => p.RowId).ToList() : new List<GatheringPoint>();
    }

    public static Item? GetItem(uint itemId)
    {
        return Svc.Data.GetExcelSheet<Item>()?.GetRow(itemId);
    }
}
