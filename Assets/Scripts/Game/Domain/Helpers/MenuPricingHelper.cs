using System.Collections.Generic;
using UnityEngine;

public static class MenuPricingHelper
{
    public static MenuPricingData FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new MenuPricingData();

        return JsonUtility.FromJson<MenuPricingData>(json) ?? new MenuPricingData();
    }

    public static string ToJson(MenuPricingData data)
    {
        return JsonUtility.ToJson(data ?? new MenuPricingData());
    }

    public static MenuPricingData FromProducts(IEnumerable<ProductData> products)
    {
        var data = new MenuPricingData();

        if (products == null)
            return data;

        foreach (var product in products)
        {
            if (product == null || string.IsNullOrWhiteSpace(product.id))
                continue;

            data.items.Add(new MenuPricingItem
            {
                productId = product.id,
                selectedPrice = product.ClampPrice(product.price)
            });
        }

        return data;
    }

    public static bool TryGetPrice(MenuPricingData data, string productId, out float price)
    {
        price = 0f;

        if (data?.items == null || string.IsNullOrWhiteSpace(productId))
            return false;

        foreach (var item in data.items)
        {
            if (item.productId == productId)
            {
                price = item.selectedPrice;
                return true;
            }
        }

        return false;
    }
}
