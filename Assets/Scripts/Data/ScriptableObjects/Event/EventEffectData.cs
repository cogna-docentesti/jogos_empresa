using System;

[Serializable]
public class EventEffectData
{
    public float cashDelta = 0f;
    public float demandMultiplier = 1f;
    public float capacityMultiplier = 1f;
    public float stockMultiplier = 1f;
    public float ingredientCostMultiplier = 1f;
    public float averageTicketMultiplier = 1f;
    public int reputationDelta = 0;
    public EventEffectDuration duration = EventEffectDuration.IMMEDIATE;
}
