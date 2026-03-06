using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PressureManager : MonoBehaviour
{
    public int maxTicks = 9;
    public int ticksPerMistake = 3;

    public float secondsPenaltyPerTick = 0.25f;
    public float minDecisionTimeSeconds = 1.5f;

    public TMP_Text pressureText;
    public PressureMeterUI pressureMeterUI;

    [SerializeField] private StressVFXController stressVFX;

    public int CurrentTicks { get; private set; }
    public bool IsMaxed => CurrentTicks >= maxTicks;

    private void Start()
    {
        RefreshUI();
        UpdateStressVFX();
    }

    public void ResetPressure()
    {
        CurrentTicks = 0;
        RefreshUI();
        UpdateStressVFX();
    }

    public void AddMistake()
    {
        CurrentTicks = Mathf.Min(maxTicks, CurrentTicks + ticksPerMistake);

        RefreshUI();
        UpdateStressVFX();

        if (stressVFX != null)
            stressVFX.PlayMistakeEffects();
    }

    public void ReduceTicks(int amount)
    {
        CurrentTicks = Mathf.Max(0, CurrentTicks - Mathf.Abs(amount));

        RefreshUI();
        UpdateStressVFX();
    }

    public float ApplyTimePenalty(float baseTime)
    {
        float penalty = CurrentTicks * secondsPenaltyPerTick;
        return Mathf.Max(minDecisionTimeSeconds, baseTime - penalty);
    }

    private void RefreshUI()
    {
        if (pressureText != null)
            pressureText.text = CurrentTicks + "/" + maxTicks;

        if (pressureMeterUI != null)
            pressureMeterUI.SetPressureTicks(CurrentTicks);
    }

    private void UpdateStressVFX()
    {
        if (stressVFX == null) return;

        float intensity01 = (maxTicks <= 0) ? 0f : (float)CurrentTicks / maxTicks;
        stressVFX.SetPressureIntensity01(intensity01);
    }
}
