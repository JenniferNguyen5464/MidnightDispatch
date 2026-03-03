using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PhoneClickable : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CallManager callManager; // The system that controls call flow (ringing -> pickup -> decision)

    private void OnMouseDown()
    {
        // Clicking the phone attempts to pick up the current ringing call.
        // CallManager handles ignoring the input if the phone isn't actually ringing.
        if (callManager == null) return;

        callManager.PhonePickup();
    }
}
