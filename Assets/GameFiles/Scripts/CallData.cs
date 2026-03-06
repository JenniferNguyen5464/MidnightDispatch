using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// What the player can dispatch for a call
public enum DispatchService
{
    Police,
    Fire,
    Ambulance
}

[CreateAssetMenu(menuName = "Midnight Dispatch/Call Data")]
public class CallData : ScriptableObject
{
    // Optional label (you can show this in UI if you want)
    public string callerId = "Unknown Caller";

    [TextArea(4, 12)]
    public string transcript;

    // The correct dispatch choice
    public DispatchService correctService = DispatchService.Police;

    // Time allowed to choose (before pressure penalty)
    public float decisionTimeSeconds = 8f;
}
