using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Midnight Dispatch/Night Data")]
public class NightData : ScriptableObject
{
    // Calls that can appear during this night
    public List<CallData> callPool = new List<CallData>();

    // How many calls to play this night
    public int callsPerNight = 6;

    // If true, shuffle the call list before choosing
    public bool randomizeSelection = true;
}