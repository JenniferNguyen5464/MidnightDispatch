using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CallManager : MonoBehaviour
{
    private enum State { Ringing, Typing, Decision, Cooldown, NightEnd }

    [SerializeField] private NightData nightData;

    [SerializeField] private TMP_Text transcriptText;
    [SerializeField] private TMP_Text statusText;

    [SerializeField] private Image decisionTimerFill;

    // Old UI buttons 
    [SerializeField] private Button policeButton;
    [SerializeField] private Button fireButton;
    [SerializeField] private Button ambulanceButton;

    // 3D button colliders
    [SerializeField] private Collider policeButton3D;
    [SerializeField] private Collider fireButton3D;
    [SerializeField] private Collider ambulanceButton3D;

    // Cooldown UI 
    [SerializeField] private Image cooldownFill;
    [SerializeField] private TMP_Text cooldownText;

    [SerializeField] private GameOverUI gameOverUI;

    [SerializeField] private Typewriter typewriter;
    [SerializeField] private PressureManager pressureManager;
    [SerializeField] private BreathingMinigame breathingMinigame;

    [SerializeField] private PhoneClickable phoneClickable;
    [SerializeField] private PaperBagClickable paperBagClickable;

    [SerializeField] private float downtimeSeconds = 6f;
    [SerializeField] private float feedbackSeconds = 1.0f;

    private State state = State.Ringing;

    private List<CallData> sequence = new List<CallData>();
    private int callIndex = -1;
    private CallData currentCall;

    private bool choiceMade;
    private DispatchService chosenService;

    public bool IsCooldown => state == State.Cooldown;

    // Used to stop players from spamming the bag during the same cooldown
    public bool BagUsedThisCooldown { get; private set; }

    // PaperBagClickable calls this right before opening the breathing minigame
    public void MarkBagUsedThisCooldown()
    {
        BagUsedThisCooldown = true;
        RefreshInteractables();
    }

    private void Awake()
    {
        if (policeButton != null) policeButton.onClick.AddListener(() => Dispatch(DispatchService.Police));
        if (fireButton != null) fireButton.onClick.AddListener(() => Dispatch(DispatchService.Fire));
        if (ambulanceButton != null) ambulanceButton.onClick.AddListener(() => Dispatch(DispatchService.Ambulance));

        SetButtons(false);
        SetDecisionTimer(0f);
        SetCooldownUI(false);
    }

    private void Start()
    {
        if (nightData == null || nightData.callPool == null || nightData.callPool.Count == 0)
        {
            if (statusText != null) statusText.text = "ERROR: NightData not set";
            return;
        }

        if (pressureManager != null)
            pressureManager.ResetPressure();

        if (breathingMinigame != null)
            breathingMinigame.ResetForNight();

        BuildNightSequence();
        NextCall();
    }

    // Called by clicking the phone object
    public void PhonePickup()
    {
        if (state != State.Ringing) return;
        StartCoroutine(CallRoutine());
    }

    private void BuildNightSequence()
    {
        List<CallData> pool = new List<CallData>(nightData.callPool);

        if (nightData.randomizeSelection)
        {
            // Simple shuffle
            for (int i = 0; i < pool.Count; i++)
            {
                int j = Random.Range(i, pool.Count);
                CallData temp = pool[i];
                pool[i] = pool[j];
                pool[j] = temp;
            }
        }

        int count = Mathf.Clamp(nightData.callsPerNight, 1, pool.Count);

        sequence.Clear();
        for (int i = 0; i < count; i++)
            sequence.Add(pool[i]);
    }

    private void NextCall()
    {
        callIndex++;

        // No calls left = night ends
        if (callIndex >= sequence.Count)
        {
            state = State.NightEnd;

            if (statusText != null) statusText.text = "Night Complete!";
            if (transcriptText != null) transcriptText.text = "";

            SetButtons(false);
            SetDecisionTimer(0f);
            SetCooldownUI(false);

            RefreshInteractables();
            return;
        }

        currentCall = sequence[callIndex];

        state = State.Ringing;
        if (statusText != null) statusText.text = "Phone Ringing...";
        if (transcriptText != null) transcriptText.text = "";

        SetButtons(false);
        SetDecisionTimer(0f);
        SetCooldownUI(false);

        RefreshInteractables();
    }

    private IEnumerator CallRoutine()
    {
        // Typing phase 
        state = State.Typing;
        RefreshInteractables();
        SetCooldownUI(false);

        if (statusText != null)
            statusText.text = "Call " + (callIndex + 1) + "/" + sequence.Count;

        if (typewriter != null)
        {
            typewriter.SetTarget(transcriptText);
            typewriter.StartTyping(currentCall.transcript);

            while (typewriter.IsTyping)
                yield return null;
        }
        else
        {
            if (transcriptText != null) transcriptText.text = currentCall.transcript;
        }

        // Decision phase 
        state = State.Decision;
        RefreshInteractables();

        choiceMade = false;
        SetButtons(true);

        float baseDuration = Mathf.Max(1f, currentCall.decisionTimeSeconds);
        float duration = (pressureManager != null) ? pressureManager.ApplyTimePenalty(baseDuration) : baseDuration;

        float t = 0f;
        while (t < duration && !choiceMade)
        {
            t += Time.deltaTime;
            SetDecisionTimer(1f - (t / duration));
            yield return null;
        }

        SetButtons(false);
        SetDecisionTimer(0f);

        // Result phase
        bool madeMistake = false;

        if (!choiceMade)
        {
            madeMistake = true;
            if (statusText != null) statusText.text = "Missed call!";
        }
        else
        {
            bool correct = (chosenService == currentCall.correctService);
            if (statusText != null) statusText.text = correct ? "Correct dispatch!" : "Wrong dispatch!";
            madeMistake = !correct;
        }

        if (transcriptText != null)
            transcriptText.text = "";

        if (madeMistake)
        {
            if (RegisterMistakeAndCheckGameOver())
                yield break;
        }

        yield return new WaitForSeconds(feedbackSeconds);

        // If this was the last call, end the night (no cooldown)
        if (callIndex >= sequence.Count - 1)
        {
            NextCall();
            yield break;
        }

        // Cooldown phase
        state = State.Cooldown;
        BagUsedThisCooldown = false;
        RefreshInteractables();

        if (statusText != null) statusText.text = "Waiting for next call...";

        float total = Mathf.Max(0.01f, downtimeSeconds);
        float remaining = total;

        SetCooldownUI(true);

        while (remaining > 0f)
        {
            // Cooldown pauses while the breathing minigame is open
            if (breathingMinigame != null && breathingMinigame.IsActive)
            {
                UpdateCooldownUI(remaining, total);
                RefreshInteractables();
                yield return null;
                continue;
            }

            remaining -= Time.deltaTime;
            if (remaining < 0f) remaining = 0f;

            UpdateCooldownUI(remaining, total);
            RefreshInteractables();
            yield return null;
        }

        SetCooldownUI(false);
        NextCall();
    }

    // Called by UI buttons and 3D button click scripts
    public void Dispatch(DispatchService service)
    {
        if (state != State.Decision) return;

        chosenService = service;
        choiceMade = true;

        if (transcriptText != null)
            transcriptText.text = "";
    }

    private void RefreshInteractables()
    {
        // Phone only works when ringing
        if (phoneClickable != null)
            phoneClickable.SetCanClick(state == State.Ringing);

        // Bag only works during cooldown, once per cooldown, and only if it would do something
        bool hasPressure = (pressureManager != null && pressureManager.CurrentTicks > 0);
        bool hasUsesLeft = (breathingMinigame == null) || breathingMinigame.HasUsesLeft();
        bool minigameOpen = (breathingMinigame != null && breathingMinigame.IsActive);

        bool bagCanClick =
            state == State.Cooldown &&
            !BagUsedThisCooldown &&
            hasPressure &&
            hasUsesLeft &&
            !minigameOpen;

        if (paperBagClickable != null)
            paperBagClickable.SetCanClick(bagCanClick);
    }

    private void SetButtons(bool on)
    {
        if (policeButton != null) policeButton.interactable = on;
        if (fireButton != null) fireButton.interactable = on;
        if (ambulanceButton != null) ambulanceButton.interactable = on;

        if (policeButton3D != null) policeButton3D.enabled = on;
        if (fireButton3D != null) fireButton3D.enabled = on;
        if (ambulanceButton3D != null) ambulanceButton3D.enabled = on;
    }

    private void SetDecisionTimer(float value)
    {
        if (decisionTimerFill != null)
            decisionTimerFill.fillAmount = Mathf.Clamp01(value);
    }

    private void SetCooldownUI(bool on)
    {
        if (cooldownFill != null) cooldownFill.gameObject.SetActive(on);
        if (cooldownText != null) cooldownText.gameObject.SetActive(on);
    }

    private void UpdateCooldownUI(float remaining, float total)
    {
        float pct = Mathf.Clamp01(remaining / Mathf.Max(0.0001f, total));

        if (cooldownFill != null)
            cooldownFill.fillAmount = pct;

        if (cooldownText != null)
            cooldownText.text = "Next call: " + remaining.ToString("0.0") + "s";
    }

    private bool RegisterMistakeAndCheckGameOver()
    {
        if (pressureManager == null) return false;

        pressureManager.AddMistake();

        if (pressureManager.IsMaxed)
        {
            TriggerGameOver();
            return true;
        }

        return false;
    }

    private void TriggerGameOver()
    {
        state = State.NightEnd;
        RefreshInteractables();

        SetButtons(false);
        SetDecisionTimer(0f);
        SetCooldownUI(false);

        if (transcriptText != null) transcriptText.text = "";
        if (statusText != null) statusText.text = "GAME OVER";

        if (typewriter != null) typewriter.StopTyping();

        if (gameOverUI != null)
            gameOverUI.ShowGameOver();
    }
}