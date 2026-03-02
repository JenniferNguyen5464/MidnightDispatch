using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum DispatchService { Police, Fire, Ambulance }

[CreateAssetMenu(menuName = "Midnight Dispatch/Call Data")]
public class CallData : ScriptableObject
{
    public string callerId = "Unknown Caller";

    [TextArea(4, 12)]
    public string transcript;

    public DispatchService correctService = DispatchService.Police;

    [Header("Timing")]
    [Min(1f)] public float decisionTimeSeconds = 8f;
}
