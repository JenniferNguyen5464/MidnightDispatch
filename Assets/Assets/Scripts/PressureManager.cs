using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PressureManager : MonoBehaviour
{
    [Header("Pressure Settings")]
    public int maxTicks = 9;
    public int ticksPerMistake = 3;

    [Header("Timer Scaling (per tick)")]
    [Tooltip("Seconds removed from decision time for EACH tick of pressure.")]
    public float secondsPenaltyPerTick = 0.25f;

    [Tooltip("Decision time will never go below this.")]
    public float minDecisionTimeSeconds = 1.5f;

    [Header("UI (optional)")]
    public TMP_Text pressureText;

    public int CurrentTicks { get; private set; } = 0;

    public void ResetPressure()
    {
        CurrentTicks = 0;
        RefreshUI();
    }

    public void AddMistake()
    {
        CurrentTicks = Mathf.Min(maxTicks, CurrentTicks + ticksPerMistake);
        RefreshUI();
    }

    public void ReduceTicks(int amount)
    {
        CurrentTicks = Mathf.Max(0, CurrentTicks - Mathf.Abs(amount));
        RefreshUI();
    }

    public bool IsMaxed => CurrentTicks >= maxTicks;

    public float ApplyTimePenalty(float baseTime)
    {
        float penalty = CurrentTicks * secondsPenaltyPerTick;
        return Mathf.Max(minDecisionTimeSeconds, baseTime - penalty);
    }

    private void RefreshUI()
    {
        if (pressureText != null)
            pressureText.text = $"Pressure: {CurrentTicks}/{maxTicks}";
    }
}
