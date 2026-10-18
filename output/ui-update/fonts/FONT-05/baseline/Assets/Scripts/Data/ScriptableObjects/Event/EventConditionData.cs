using System;

[Serializable]
public class EventConditionData
{
    public EventConditionType type;
    public EventConditionOperator comparison;
    public float value;
}
