using UnityEngine;
using System;

public enum ObjectiveLogic  { All, Any }
public enum ObjectiveState  { Inactive, Active, Complete, Failed }
public enum RewardType      { Item, Currency, Experience, Unlock }

[Serializable]
public struct Reward
{
    public RewardType type;
    public int        amount;
    public string     id;      // itemID, unlockID, etc.
}
