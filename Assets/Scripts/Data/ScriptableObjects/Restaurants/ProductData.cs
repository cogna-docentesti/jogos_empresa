using System;


[Serializable]
public class ProductData
{
    public string id;
    public string name;
    public int price;

    [UnityEngine.Range(0f, 1f)]
    public float inputCostRatio;
}