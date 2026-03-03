using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PaperBagClickable : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CallManager callManager;               // Assigned in Inspector
    [SerializeField] private PressureManager pressureManager;       // Assigned in Inspector
    [SerializeField] private BreathingMinigame breathingMinigame;   // Assigned in Inspector

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;                 // Toggle console logs

    private void OnMouseDown()
    {
        // Safety checks so this doesn't silently fail if something wasn't wired up
        if (callManager == null)
        {
            if (logDebug) Debug.LogWarning("PaperBagClickable: CallManager reference missing.");
            return;
        }

        if (pressureManager == null)
        {
            if (logDebug) Debug.LogWarning("PaperBagClickable: PressureManager reference missing.");
            return;
        }

        if (breathingMinigame == null)
        {
            if (logDebug) Debug.LogWarning("PaperBagClickable: BreathingMinigame reference missing.");
            return;
        }

        // Paper bag is only usable during the cooldown window between calls
        if (!callManager.IsCooldown)
        {
            if (logDebug) Debug.Log("PaperBagClickable: Not in cooldown. Bag disabled.");
            return;
        }

        // Only allow one use per cooldown window (prevents spam between calls)
        if (callManager.BagUsedThisCooldown)
        {
            if (logDebug) Debug.Log("PaperBagClickable: Bag already used during this cooldown.");
            return;
        }

        // No reason to use it if pressure is already at 0
        if (pressureManager.CurrentTicks <= 0)
        {
            if (logDebug) Debug.Log("PaperBagClickable: Pressure is 0. Bag disabled.");
            return;
        }

        // Enforce per-night uses limit (tracked by the minigame)
        if (!breathingMinigame.HasUsesLeft())
        {
            if (logDebug) Debug.Log("PaperBagClickable: No uses left for the night.");
            return;
        }

        // Prevent opening the minigame twice
        if (breathingMinigame.IsActive)
        {
            if (logDebug) Debug.Log("PaperBagClickable: Minigame already active.");
            return;
        }

        // Lock the cooldown use immediately so repeated clicks can't reopen it
        callManager.MarkBagUsedThisCooldown();

        // Open the breathing minigame UI
        breathingMinigame.Open();
    }
}
