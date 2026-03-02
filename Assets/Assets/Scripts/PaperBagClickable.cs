using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PaperBagClickable : MonoBehaviour
{
    [Header("References (drag these in)")]
    [SerializeField] private CallManager callManager;
    [SerializeField] private PressureManager pressureManager;
    [SerializeField] private BreathingMinigame breathingMinigame;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private void OnMouseDown()
    {
        if (callManager == null)
        {
            if (logDebug) Debug.LogWarning("PaperBagClickable: callManager is NOT assigned.");
            return;
        }
        if (pressureManager == null)
        {
            if (logDebug) Debug.LogWarning("PaperBagClickable: pressureManager is NOT assigned.");
            return;
        }
        if (breathingMinigame == null)
        {
            if (logDebug) Debug.LogWarning("PaperBagClickable: breathingMinigame is NOT assigned.");
            return;
        }

        // Only during cooldown
        if (!callManager.IsCooldown)
        {
            if (logDebug) Debug.Log("PaperBagClickable: Not in cooldown, bag disabled.");
            return;
        }

        //NEW: only once per cooldown
        if (callManager.BagUsedThisCooldown)
        {
            if (logDebug) Debug.Log("PaperBagClickable: Already used bag this cooldown.");
            return;
        }

        // Disabled if pressure is 0
        if (pressureManager.CurrentTicks <= 0)
        {
            if (logDebug) Debug.Log("PaperBagClickable: Pressure is 0, bag disabled.");
            return;
        }

        // Uses limit per night
        if (!breathingMinigame.HasUsesLeft())
        {
            if (logDebug) Debug.Log("PaperBagClickable: No uses left.");
            return;
        }

        // Already open
        if (breathingMinigame.IsActive)
        {
            if (logDebug) Debug.Log("PaperBagClickable: Minigame already active.");
            return;
        }

        //Consume the cooldown-use immediately (prevents re-open spam)
        callManager.MarkBagUsedThisCooldown();

        // Open minigame
        breathingMinigame.Open();
    }
}
