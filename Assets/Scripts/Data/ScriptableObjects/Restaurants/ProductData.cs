using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Product")]
public class ProductData : ScriptableObject
{
    public string id;
    public string displayName;
    public Sprite productSprite;
    public int price;
    public float minPrice;
    public float maxPrice;

    [Range(0f, 1f)]
    public float inputCostRatio;

    public bool IsPriceInRange(float selectedPrice)
    {
        return selectedPrice >= minPrice && selectedPrice <= maxPrice;
    }

    public float ClampPrice(float selectedPrice)
    {
        if (maxPrice <= minPrice)
            return Mathf.Max(0f, selectedPrice);

        return Mathf.Clamp(selectedPrice, minPrice, maxPrice);
    }
}
