using System;
using UnityEngine;

[Serializable]
public class EventOption
{
    public string id;
    public string title;

    [TextArea]
    public string description;

    public EventEffectData effects;
}
