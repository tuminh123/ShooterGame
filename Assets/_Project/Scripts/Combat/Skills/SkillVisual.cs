using System;
using UnityEngine;

[Serializable]
public sealed class SkillVisual
{
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;
}
