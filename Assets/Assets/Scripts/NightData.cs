using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Midnight Dispatch/Night Data")]
public class NightData : ScriptableObject
{
    [Header("Call Pool (full list)")]
    [Tooltip("All possible calls that can be picked for this night.")]
    public List<CallData> callPool = new List<CallData>();

    [Header("Night Length")]
    [Tooltip("How many calls this night will actually play (pulled from the call pool).")]
    [Min(1)]
    public int callsPerNight = 6;

    [Header("Selection")]
    [Tooltip("If true, the call pool is shuffled before selecting calls for the night.")]
    public bool randomizeSelection = true;
}


