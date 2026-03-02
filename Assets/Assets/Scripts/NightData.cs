using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Midnight Dispatch/Night Data")]
public class NightData : ScriptableObject
{
    [Header("Call Pool (bigger list)")]
    public List<CallData> callPool = new List<CallData>();

    [Header("How many calls this night will actually use")]
    [Min(1)] public int callsPerNight = 6;

    [Header("Selection")]
    public bool randomizeSelection = true;
}


