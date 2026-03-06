using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PaperBagClickable : MonoBehaviour
{
    [SerializeField] private CallManager callManager;
    [SerializeField] private PressureManager pressureManager;
    [SerializeField] private BreathingMinigame breathingMinigame;

    [SerializeField] private InteractableVisual visual;
    [SerializeField] private bool startClickable = false;

    private bool canClick;

    private void Awake()
    {
        if (visual == null)
            visual = GetComponent<InteractableVisual>();

        SetCanClick(startClickable);
    }

    // CallManager uses this to turn the bag on/off and update the dimming
    public void SetCanClick(bool value)
    {
        canClick = value;

        if (visual != null)
            visual.SetInteractable(value);
    }

    private void OnMouseDown()
    {
        if (!canClick) return;

        // Only usable during cooldown between calls
        if (callManager == null || !callManager.IsCooldown) return;

        // Only once per cooldown window
        if (callManager.BagUsedThisCooldown) return;

        // No point using it if pressure is already 0
        if (pressureManager == null || pressureManager.CurrentTicks <= 0) return;

        // Check per-night usage limit
        if (breathingMinigame == null || !breathingMinigame.HasUsesLeft()) return;

        // Prevent opening twice
        if (breathingMinigame.IsActive) return;

        // Lock the use for this cooldown and grey it out immediately
        callManager.MarkBagUsedThisCooldown();
        SetCanClick(false);

        // Open the minigame
        breathingMinigame.Open();
    }
}
