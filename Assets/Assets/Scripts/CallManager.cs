using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CallManager : MonoBehaviour
{
    // Ringing  = phone is waiting to be picked up
    // Typing   = transcript is being typed out
    // Decision = player must choose a dispatch service before the timer runs out
    // Cooldown = downtime between calls (paper bag can be used here)
    // NightEnd = night finished OR game over
    private enum State { Ringing, Typing, Decision, Cooldown, NightEnd }

    [Header("Data")]
    [SerializeField] private NightData _nightData;

    [Header("UI - Call")]
    [SerializeField] private TMP_Text _transcriptText;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private Image _timerFill; // Decision timer (fill amount)
    [SerializeField] private Button _policeButton;
    [SerializeField] private Button _fireButton;
    [SerializeField] private Button _ambulanceButton;
    [SerializeField] private GameOverUI _gameOverUI;

    [Header("UI - Cooldown Timer")]
    [SerializeField] private Image _cooldownFill;          // Image Type should be "Filled"
    [SerializeField] private TMP_Text _cooldownText;       // Optional: "Next call: 3.2s"
    [SerializeField] private string _cooldownTextFormat = "Next call: {0:0.0}s";

    [Header("Systems")]
    [SerializeField] private Typewriter _typewriter;
    [SerializeField] private PressureManager _pressureManager;

    [Header("Minigames")]
    [SerializeField] private BreathingMinigame _breathingMinigame;

    [Header("Tuning")]
    [SerializeField, Min(0f)] private float _downtimeSeconds = 6f;
    [SerializeField, Min(0f)] private float _feedbackSeconds = 1.0f;

    private State _state = State.Ringing;
    private List<CallData> _sequence;
    private int _index = -1;
    private CallData _current;

    private bool _choiceMade;
    private DispatchService _chosenService;

    public bool IsCooldown => _state == State.Cooldown;

    // Used to prevent spamming the paper bag during a single cooldown window
    public bool BagUsedThisCooldown { get; private set; } = false;

    // PaperBagClickable calls this right before opening the breathing minigame
    public void MarkBagUsedThisCooldown()
    {
        BagUsedThisCooldown = true;
    }

    private void Awake()
    {
        if (_policeButton != null) _policeButton.onClick.AddListener(() => OnDispatch(DispatchService.Police));
        if (_fireButton != null) _fireButton.onClick.AddListener(() => OnDispatch(DispatchService.Fire));
        if (_ambulanceButton != null) _ambulanceButton.onClick.AddListener(() => OnDispatch(DispatchService.Ambulance));

        SetButtons(false);
        SetCallTimerFill(0f);
        SetCooldownUIActive(false);
    }

    private void Start()
    {
        // Basic validation so the night doesn't silently fail
        if (_nightData == null)
        {
            Debug.LogError("CallManager: NightData is not assigned.");
            if (_statusText != null) _statusText.text = "ERROR: NightData not assigned";
            return;
        }

        if (_nightData.callPool == null || _nightData.callPool.Count == 0)
        {
            Debug.LogError("CallManager: NightData callPool is empty.");
            if (_statusText != null) _statusText.text = "ERROR: callPool empty";
            return;
        }

        // Night setup
        if (_pressureManager != null)
            _pressureManager.ResetPressure();

        if (_breathingMinigame != null)
            _breathingMinigame.ResetForNight();

        BuildNightSequence_NoRepeats();
        NextCall();
    }

    private void BuildNightSequence_NoRepeats()
    {
        // Copies the call pool, optionally shuffles, then takes the first N calls
        List<CallData> pool = new List<CallData>(_nightData.callPool);

        if (_nightData.randomizeSelection)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                int j = Random.Range(i, pool.Count);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
        }

        int count = Mathf.Clamp(_nightData.callsPerNight, 1, pool.Count);

        _sequence = new List<CallData>(count);
        for (int i = 0; i < count; i++)
            _sequence.Add(pool[i]);
    }

    // Called by clicking the phone object
    public void PhonePickup()
    {
        if (_state != State.Ringing) return;
        StartCoroutine(RunCallRoutine());
    }

    private void NextCall()
    {
        _index++;

        // End the night when there are no calls left
        if (_index >= _sequence.Count)
        {
            _state = State.NightEnd;

            if (_statusText != null) _statusText.text = "Night Complete!";
            if (_transcriptText != null) _transcriptText.text = "";

            SetCallTimerFill(0f);
            SetCooldownUIActive(false);
            SetButtons(false);
            return;
        }

        _current = _sequence[_index];

        _state = State.Ringing;
        if (_statusText != null) _statusText.text = "Phone Ringing...";
        if (_transcriptText != null) _transcriptText.text = "";

        SetCallTimerFill(0f);
        SetCooldownUIActive(false);
        SetButtons(false);
    }

    private IEnumerator RunCallRoutine()
    {
        // -------------------------
        // Typing phase (transcript)
        // -------------------------
        _state = State.Typing;
        SetCooldownUIActive(false);

        if (_statusText != null)
            _statusText.text = $"Call {_index + 1}/{_sequence.Count}";

        if (_typewriter != null)
        {
            _typewriter.SetTarget(_transcriptText);
            _typewriter.StartTyping(_current.transcript);

            while (_typewriter.IsTyping)
                yield return null;
        }
        else
        {
            if (_transcriptText != null) _transcriptText.text = _current.transcript;
        }

        // -------------------------
        // Decision phase (buttons)
        // -------------------------
        _state = State.Decision;
        SetCooldownUIActive(false);

        _choiceMade = false;
        SetButtons(true);

        float baseDuration = Mathf.Max(1f, _current.decisionTimeSeconds);
        float duration = (_pressureManager != null) ? _pressureManager.ApplyTimePenalty(baseDuration) : baseDuration;

        float t = 0f;
        while (t < duration && !_choiceMade)
        {
            t += Time.deltaTime;
            SetCallTimerFill(1f - (t / duration));
            yield return null;
        }

        SetButtons(false);
        SetCallTimerFill(0f);

        // -------------------------
        // Result + pressure update
        // -------------------------
        if (!_choiceMade)
        {
            // Timeout counts as a mistake
            if (_statusText != null) _statusText.text = "Missed call (timeout)!";

            if (RegisterMistakeAndCheckGameOver())
                yield break;

            if (_transcriptText != null)
                _transcriptText.text = "";
        }
        else
        {
            bool correct = _chosenService == _current.correctService;
            if (_statusText != null) _statusText.text = correct ? "Correct dispatch!" : "Wrong dispatch!";

            if (!correct)
            {
                if (RegisterMistakeAndCheckGameOver())
                    yield break;
            }

            if (_transcriptText != null)
                _transcriptText.text = "";
        }

        // Short pause so the result message is readable
        yield return new WaitForSeconds(_feedbackSeconds);

        // If this was the last call, end the night immediately (no cooldown screen)
        if (_sequence != null && _index >= _sequence.Count - 1)
        {
            SetCooldownUIActive(false);
            NextCall(); // increments index and hits the NightEnd branch
            yield break;
        }

        // -------------------------
        // Cooldown phase
        // -------------------------
        _state = State.Cooldown;
        BagUsedThisCooldown = false;

        if (_statusText != null) _statusText.text = "Waiting for next call...";

        float total = Mathf.Max(0.01f, _downtimeSeconds);
        float remaining = total;

        SetCooldownUIActive(true);
        UpdateCooldownUI(remaining, total);

        while (remaining > 0f)
        {
            // Cooldown timer pauses while the breathing minigame is open
            if (_breathingMinigame != null && _breathingMinigame.IsActive)
            {
                UpdateCooldownUI(remaining, total);
                yield return null;
                continue;
            }

            remaining -= Time.deltaTime;
            if (remaining < 0f) remaining = 0f;

            UpdateCooldownUI(remaining, total);
            yield return null;
        }

        SetCooldownUIActive(false);
        NextCall();
    }

    private void OnDispatch(DispatchService service)
    {
        if (_state != State.Decision) return;

        _chosenService = service;
        _choiceMade = true;

        // Transcript clears once a service is selected
        if (_transcriptText != null)
            _transcriptText.text = "";
    }

    private void SetButtons(bool on)
    {
        if (_policeButton != null) _policeButton.interactable = on;
        if (_fireButton != null) _fireButton.interactable = on;
        if (_ambulanceButton != null) _ambulanceButton.interactable = on;
    }

    private void SetCallTimerFill(float value)
    {
        if (_timerFill != null)
            _timerFill.fillAmount = Mathf.Clamp01(value);
    }

    private void SetCooldownUIActive(bool active)
    {
        if (_cooldownFill != null) _cooldownFill.gameObject.SetActive(active);
        if (_cooldownText != null) _cooldownText.gameObject.SetActive(active);
    }

    private void UpdateCooldownUI(float remaining, float total)
    {
        float pct = Mathf.Clamp01(remaining / Mathf.Max(0.0001f, total));

        if (_cooldownFill != null)
            _cooldownFill.fillAmount = pct;

        if (_cooldownText != null)
            _cooldownText.text = string.Format(_cooldownTextFormat, remaining);
    }

    // -------------------------
    // Game over helpers
    // -------------------------
    private bool RegisterMistakeAndCheckGameOver()
    {
        if (_pressureManager == null) return false;

        _pressureManager.AddMistake();

        if (_pressureManager.IsMaxed)
        {
            TriggerGameOver();
            return true;
        }

        return false;
    }

    private void TriggerGameOver()
    {
        _state = State.NightEnd;

        SetButtons(false);
        SetCallTimerFill(0f);
        SetCooldownUIActive(false);

        if (_transcriptText != null) _transcriptText.text = "";
        if (_statusText != null) _statusText.text = "GAME OVER";

        if (_typewriter != null) _typewriter.StopTyping();

        if (_gameOverUI != null)
            _gameOverUI.ShowGameOver();
        else
            Debug.LogError("CallManager: GameOverUI reference is not assigned.");
    }
}