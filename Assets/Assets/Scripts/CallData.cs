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
    [Header("Call Info")]
    [Tooltip("Optional label for the caller (can be shown in UI if needed).")]
    public string callerId = "Unknown Caller";

    [Tooltip("The full transcript that gets typed out during the call.")]
    [TextArea(4, 12)]
    public string transcript;

    [Header("Answer")]
    [Tooltip("The correct dispatch choice for this call.")]
    public DispatchService correctService = DispatchService.Police;

    [Header("Timing")]
    [Tooltip("How long the player has to choose a service (before pressure penalties).")]
    [Min(1f)]
    public float decisionTimeSeconds = 8f;
}
