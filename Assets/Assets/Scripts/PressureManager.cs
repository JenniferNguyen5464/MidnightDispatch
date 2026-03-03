using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PressureManager : MonoBehaviour
{
    [Header("Pressure Settings")]
    [Tooltip("Maximum pressure ticks allowed before the night is considered maxed out.")]
    public int maxTicks = 9;

    [Tooltip("How many ticks get added each time a mistake happens.")]
    public int ticksPerMistake = 3;

    [Header("Timer Scaling (per tick)")]
    [Tooltip("Seconds removed from decision time for EACH tick of current pressure.")]
    public float secondsPenaltyPerTick = 0.25f;

    [Tooltip("Decision time never goes below this minimum.")]
    public float minDecisionTimeSeconds = 1.5f;

    [Header("UI (optional)")]
    [Tooltip("Optional text readout for debugging / simple UI display.")]
    public TMP_Text pressureText;

    // Current pressure level (0..maxTicks)
    public int CurrentTicks { get; private set; } = 0;

    // True when pressure has reached the cap (used for game over checks)
    public bool IsMaxed => CurrentTicks >= maxTicks;

    public void ResetPressure()
    {
        // Resets pressure at the start of a night or after restarting a night
        CurrentTicks = 0;
        RefreshUI();
    }

    public void AddMistake()
    {
        // Mistakes push pressure up by a fixed amount, clamped to maxTicks
        CurrentTicks = Mathf.Min(maxTicks, CurrentTicks + ticksPerMistake);
        RefreshUI();
    }

    public void ReduceTicks(int amount)
    {
        // Relief effects (paper bag / breathing / etc.) pull pressure down, never below 0
        CurrentTicks = Mathf.Max(0, CurrentTicks - Mathf.Abs(amount));
        RefreshUI();
    }

    public float ApplyTimePenalty(float baseTime)
    {
        // More pressure = less decision time
        float penalty = CurrentTicks * secondsPenaltyPerTick;
        return Mathf.Max(minDecisionTimeSeconds, baseTime - penalty);
    }

    private void RefreshUI()
    {
        // Keeps the simple readout synced (if a text reference is assigned)
        if (pressureText != null)
            pressureText.text = $"Pressure: {CurrentTicks}/{maxTicks}";
    }
}
