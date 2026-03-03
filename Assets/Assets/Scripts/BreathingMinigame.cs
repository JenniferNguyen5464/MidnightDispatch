using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BreathingMinigame : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject panelRoot;          // Root object for the breathing minigame UI
    [SerializeField] private Slider slider;                // Moving indicator (handle/marker)
    [SerializeField] private RectTransform trackArea;       // The full visible track width (used for converting 0..1 to X position)
    [SerializeField] private RectTransform zonesStrip;      // Parent that holds 5 zone Images in order: Red, Yellow, Green, Yellow, Red
    [SerializeField] private PressureManager pressureManager;

    [Header("UI Text (optional)")]
    [SerializeField] private TMP_Text resultText;           // Feedback text inside the minigame
    [SerializeField] private TMP_Text usesText;             // Uses text inside the minigame
    [SerializeField] private TMP_Text nightUsesText;        // HUD text (if showing uses outside the minigame)
    [SerializeField] private string nightUsesFormat = "Paper Bags: {0}/{1}";

    [Header("Timer UI (optional)")]
    [SerializeField] private Image timerFill;               // Fill Amount image used as a countdown indicator
    [SerializeField, Min(0.5f)] private float timeLimitSeconds = 3.0f;

    [Header("Uses")]
    [SerializeField] private int maxUsesPerNight = 3;
    public int UsesLeft { get; private set; }
    public int MaxUsesPerNight => Mathf.Max(1, maxUsesPerNight);

    [Header("Bar Movement")]
    [SerializeField, Min(0.1f)] private float speed = 1.2f;

    [Header("Zones Size + Randomization")]
    [SerializeField, Range(0.05f, 0.9f)] private float stripWidthOfTrack = 0.22f;  // How wide the zone strip is relative to the full track
    [SerializeField, Range(0.05f, 0.8f)] private float greenFracOfStrip = 0.25f;   // Fraction of the strip that is green
    [SerializeField, Range(0.05f, 0.45f)] private float yellowFracOfStrip = 0.18f; // Fraction of the strip for each yellow side
    [SerializeField] private bool randomizeEachOpen = true;                        // If true, the strip slides to a random X each time

    private bool _active;
    private bool _locked;
    private float _t;

    private float _timeRemaining;

    // Cached boundaries in TRACK-LOCAL X coordinates (same space used by markerX)
    private float _greenLeftX, _greenRightX;
    private float _yellowLeft1X, _yellowRight1X;
    private float _yellowLeft2X, _yellowRight2X;

    private Coroutine _closeRoutine;

    public bool IsActive => _active;

    private void Awake()
    {
        maxUsesPerNight = Mathf.Max(1, maxUsesPerNight);
        ResetForNight();
        CloseInstant();
    }

    public void ResetForNight()
    {
        maxUsesPerNight = Mathf.Max(1, maxUsesPerNight);
        UsesLeft = maxUsesPerNight;
        RefreshUsesUI();
    }

    public bool HasUsesLeft() => UsesLeft > 0;

    public void Open()
    {
        // Hard stop if something important wasn't wired up in the Inspector
        if (panelRoot == null || slider == null || trackArea == null || zonesStrip == null || pressureManager == null)
        {
            Debug.LogError("BreathingMinigame: Missing references. Assign panelRoot, slider, trackArea, zonesStrip, pressureManager.");
            return;
        }

        if (_active) return;
        if (UsesLeft <= 0) return;

        // Cancel any delayed close that might still be running
        if (_closeRoutine != null) StopCoroutine(_closeRoutine);
        _closeRoutine = null;

        _active = true;
        _locked = false;
        _t = 0f;

        // Reset timer
        timeLimitSeconds = Mathf.Max(0.5f, timeLimitSeconds);
        _timeRemaining = timeLimitSeconds;
        UpdateTimerUI();

        // Slider uses 0..1, which later gets converted into an X position on the track
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;

        // Layout the 5 zone children based on the configured fractions
        SetupStripChildrenLayout();

        // Move the strip somewhere random and cache the zone bounds for scoring
        if (randomizeEachOpen) RandomizeStripPositionAndCacheBounds();
        else RandomizeStripPositionAndCacheBounds(); // Still cache bounds even if strip stays centered (keeps one code path)

        if (resultText != null) resultText.text = "Press SPACE to breathe";
        RefreshUsesUI();

        panelRoot.SetActive(true);
    }

    private void Update()
    {
        if (!_active) return;
        if (_locked) return;

        // Timer countdown
        _timeRemaining -= Time.deltaTime;
        UpdateTimerUI();

        // Move marker back and forth
        _t += Time.deltaTime * speed;
        slider.value = Mathf.PingPong(_t, 1f);

        // Manual stop
        if (Input.GetKeyDown(KeyCode.Space))
        {
            _locked = true;
            ResolveStop(slider.value);
            return;
        }

        // Time-out stop
        if (_timeRemaining <= 0f)
        {
            _locked = true;
            ResolveStop(slider.value);
            return;
        }
    }

    private void UpdateTimerUI()
    {
        if (timerFill == null) return;

        float pct = Mathf.Clamp01(_timeRemaining / Mathf.Max(0.0001f, timeLimitSeconds));
        timerFill.fillAmount = pct;
    }

    private void ResolveStop(float value01)
    {
        // Convert slider 0..1 into an X coordinate on the track
        float trackWidth = trackArea.rect.width;
        if (trackWidth <= 0.001f)
        {
            Debug.LogError("BreathingMinigame: trackArea width is 0. Check RectTransform sizing.");
            CloseInstant();
            return;
        }

        float minX = -trackWidth * 0.5f;
        float maxX = trackWidth * 0.5f;
        float markerX = Mathf.Lerp(minX, maxX, Mathf.Clamp01(value01));

        // Score the stop position against cached zone boundaries
        int reduceBy = EvaluateReduction(markerX);

        // Apply relief to pressure
        pressureManager.ReduceTicks(reduceBy);

        // Consume one use
        UsesLeft = Mathf.Max(0, UsesLeft - 1);
        RefreshUsesUI();

        if (resultText != null)
        {
            resultText.text =
                reduceBy == 3 ? "Perfect breath! (-3)" :
                reduceBy == 2 ? "Good breath! (-2)" :
                                "Breath caught. (-1)";
        }

        // End timer visually and close after a short delay
        _timeRemaining = 0f;
        UpdateTimerUI();

        _closeRoutine = StartCoroutine(CloseAfterSeconds(0.6f));
    }

    private int EvaluateReduction(float markerX)
    {
        // Green = best
        if (markerX >= _greenLeftX && markerX <= _greenRightX) return 3;

        // Yellow = medium
        bool inYellow1 = markerX >= _yellowLeft1X && markerX <= _yellowRight1X;
        bool inYellow2 = markerX >= _yellowLeft2X && markerX <= _yellowRight2X;
        if (inYellow1 || inYellow2) return 2;

        // Red/outside = worst
        return 1;
    }

    private void SetupStripChildrenLayout()
    {
        if (zonesStrip.childCount < 5)
        {
            Debug.LogError("BreathingMinigame: zonesStrip needs 5 child Images (R,Y,G,Y,R).");
            return;
        }

        float greenF = Mathf.Clamp01(greenFracOfStrip);
        float yellowF = Mathf.Clamp01(yellowFracOfStrip);

        // Whatever is left becomes red, split evenly on both sides
        float redF = (1f - greenF - 2f * yellowF) * 0.5f;

        // Safety clamp so red never collapses to nothing (avoids weird layouts)
        if (redF < 0.02f)
        {
            redF = 0.02f;
            float remaining = 1f - 2f * redF;
            float totalGY = (2f * yellowF + greenF);
            float scale = remaining / Mathf.Max(0.0001f, totalGY);
            yellowF *= scale;
            greenF *= scale;
        }

        // Anchor ranges across 0..1 within the strip
        float a0 = 0f;
        float a1 = a0 + redF;
        float a2 = a1 + yellowF;
        float a3 = a2 + greenF;
        float a4 = a3 + yellowF;
        float a5 = 1f;

        SetChildAnchors(zonesStrip.GetChild(0) as RectTransform, a0, a1); // Red (Left)
        SetChildAnchors(zonesStrip.GetChild(1) as RectTransform, a1, a2); // Yellow (Left)
        SetChildAnchors(zonesStrip.GetChild(2) as RectTransform, a2, a3); // Green (Center)
        SetChildAnchors(zonesStrip.GetChild(3) as RectTransform, a3, a4); // Yellow (Right)
        SetChildAnchors(zonesStrip.GetChild(4) as RectTransform, a4, a5); // Red (Right)
    }

    private void SetChildAnchors(RectTransform rt, float minX, float maxX)
    {
        if (rt == null) return;

        // Anchors define the portion of the strip each zone occupies
        rt.anchorMin = new Vector2(minX, 0f);
        rt.anchorMax = new Vector2(maxX, 1f);

        // Keep zones flush with the strip bounds
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    private void RandomizeStripPositionAndCacheBounds()
    {
        float trackWidth = trackArea.rect.width;
        if (trackWidth <= 0.001f)
        {
            Debug.LogError("BreathingMinigame: trackArea width is 0. Check RectTransform sizing.");
            return;
        }

        // Strip uses centered anchors so it can slide horizontally within the track
        zonesStrip.pivot = new Vector2(0.5f, 0.5f);
        zonesStrip.anchorMin = new Vector2(0.5f, 0.5f);
        zonesStrip.anchorMax = new Vector2(0.5f, 0.5f);

        float stripWidth = Mathf.Clamp(stripWidthOfTrack, 0.05f, 0.95f) * trackWidth;

        zonesStrip.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, stripWidth);
        zonesStrip.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, trackArea.rect.height);

        // Pick a random center X that keeps the strip fully inside the track
        float minCenterX = -trackWidth * 0.5f + stripWidth * 0.5f;
        float maxCenterX = trackWidth * 0.5f - stripWidth * 0.5f;
        float centerX = Random.Range(minCenterX, maxCenterX);

        zonesStrip.anchoredPosition = new Vector2(centerX, 0f);

        // Rebuild the same fractions used for layout so the cached bounds match the visuals
        float greenF = Mathf.Clamp01(greenFracOfStrip);
        float yellowF = Mathf.Clamp01(yellowFracOfStrip);
        float redF = (1f - greenF - 2f * yellowF) * 0.5f;

        if (redF < 0.02f)
        {
            redF = 0.02f;
            float remaining = 1f - 2f * redF;
            float totalGY = (2f * yellowF + greenF);
            float scale = remaining / Mathf.Max(0.0001f, totalGY);
            yellowF *= scale;
            greenF *= scale;
        }

        // Strip-left in track-local X
        float stripLeftX = centerX - stripWidth * 0.5f;

        // Widths in pixels
        float redW = stripWidth * redF;
        float yellowW = stripWidth * yellowF;
        float greenW = stripWidth * greenF;

        // Cache yellow + green boundaries in track-local X
        _yellowLeft1X = stripLeftX + redW;
        _yellowRight1X = _yellowLeft1X + yellowW;

        _greenLeftX = _yellowRight1X;
        _greenRightX = _greenLeftX + greenW;

        _yellowLeft2X = _greenRightX;
        _yellowRight2X = _yellowLeft2X + yellowW;
    }

    private IEnumerator CloseAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        CloseInstant();
    }

    public void CloseInstant()
    {
        _active = false;
        _locked = false;

        if (_closeRoutine != null) StopCoroutine(_closeRoutine);
        _closeRoutine = null;

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void RefreshUsesUI()
    {
        // Inside-minigame text
        if (usesText != null)
            usesText.text = $"Uses: {UsesLeft}/{MaxUsesPerNight}";

        // HUD / always-visible text
        if (nightUsesText != null)
            nightUsesText.text = string.Format(nightUsesFormat, UsesLeft, MaxUsesPerNight);
    }
}