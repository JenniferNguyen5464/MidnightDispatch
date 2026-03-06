using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BreathingMinigame : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;     // Root UI object for the minigame
    [SerializeField] private Slider slider;            // Marker that moves left/right (0..1)
    [SerializeField] private RectTransform trackArea;  // Full track width (used to convert 0..1 to an X position)
    [SerializeField] private RectTransform zonesStrip; // Holds 5 zone images in order: Red, Yellow, Green, Yellow, Red
    [SerializeField] private PressureManager pressureManager;

    [SerializeField] private TMP_Text resultText;      // Feedback like "Perfect breath!"
    [SerializeField] private TMP_Text usesText;        // Uses text inside the minigame
    [SerializeField] private TMP_Text nightUsesText;   // Optional HUD uses text outside the minigame
    [SerializeField] private string nightUsesFormat = "Paper Bags: {0}/{1}";

    [SerializeField] private Image timerFill;          // Optional countdown fill image
    [SerializeField] private float timeLimitSeconds = 3.0f;

    [SerializeField] private int maxUsesPerNight = 3;
    public int UsesLeft { get; private set; }
    public int MaxUsesPerNight => Mathf.Max(1, maxUsesPerNight);

    [SerializeField] private float speed = 1.2f;       // How fast the marker moves

    // Zone sizing
    [SerializeField] private float stripWidthOfTrack = 0.22f; // Strip width relative to the full track
    [SerializeField] private float greenFracOfStrip = 0.25f;  // Percent of strip that is green
    [SerializeField] private float yellowFracOfStrip = 0.18f; // Percent of strip for each yellow
    [SerializeField] private bool randomizeEachOpen = true;   // Random strip position each time the bag is used

    private bool _active;
    private bool _locked;
    private float _t;
    private float _timeRemaining;

    // Cached zone boundaries in TRACK-LOCAL X space (used for scoring)
    private float _greenLeftX, _greenRightX;
    private float _yellowLeft1X, _yellowRight1X;
    private float _yellowLeft2X, _yellowRight2X;

    // Cached strip boundaries (anything outside the strip gives 0 relief)
    private float _stripLeftX, _stripRightX;

    private Coroutine _closeRoutine;

    public bool IsActive => _active;

    private void Awake()
    {
        if (maxUsesPerNight < 1) maxUsesPerNight = 1;

        // Each night starts with full uses, and the panel starts closed
        ResetForNight();
        CloseInstant();
    }

    public void ResetForNight()
    {
        if (maxUsesPerNight < 1) maxUsesPerNight = 1;

        UsesLeft = maxUsesPerNight;
        RefreshUsesUI();
    }

    public bool HasUsesLeft()
    {
        return UsesLeft > 0;
    }

    public void Open()
    {
        // If important references are missing, stop instead of failing silently
        if (panelRoot == null || slider == null || trackArea == null || zonesStrip == null || pressureManager == null)
        {
            Debug.LogError("BreathingMinigame: Missing references in Inspector.");
            return;
        }

        if (_active) return;
        if (UsesLeft <= 0) return;

        // Cancel any pending close from a previous run
        if (_closeRoutine != null) StopCoroutine(_closeRoutine);
        _closeRoutine = null;

        _active = true;
        _locked = false;
        _t = 0f;

        // Reset timer
        timeLimitSeconds = Mathf.Max(0.5f, timeLimitSeconds);
        _timeRemaining = timeLimitSeconds;
        UpdateTimerUI();

        // Reset marker movement
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;

        // Layout the 5 zones using anchors (keeps your UI looking the same)
        SetupStripChildrenLayout();

        // Set strip size + position and cache the zone boundaries for scoring
        RandomizeStripPositionAndCacheBounds();

        if (resultText != null) resultText.text = "Press SPACE to breathe";
        RefreshUsesUI();

        panelRoot.SetActive(true);
    }

    private void Update()
    {
        if (!_active || _locked) return;

        // Countdown
        _timeRemaining -= Time.deltaTime;
        UpdateTimerUI();

        // Move the marker back and forth
        speed = Mathf.Max(0.1f, speed);
        _t += Time.deltaTime * speed;
        slider.value = Mathf.PingPong(_t, 1f);

        // Player stops the marker (or it stops automatically when time runs out)
        if (Input.GetKeyDown(KeyCode.Space) || _timeRemaining <= 0f)
        {
            _locked = true;
            ResolveStop(slider.value);
        }
    }

    private void ResolveStop(float value01)
    {
        // Convert marker 0..1 into a track-local X position
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

        // Green = best (-3), Yellow = medium (-2), Red = worst (-1), Outside strip = 0
        int reduceBy = EvaluateReduction(markerX);

        // Apply the pressure reduction and consume one use
        pressureManager.ReduceTicks(reduceBy);

        UsesLeft = Mathf.Max(0, UsesLeft - 1);
        RefreshUsesUI();

        if (resultText != null)
        {
            if (reduceBy == 3) resultText.text = "Perfect breath! (-3)";
            else if (reduceBy == 2) resultText.text = "Good breath! (-2)";
            else if (reduceBy == 1) resultText.text = "Breath caught. (-1)";
            else resultText.text = "Missed the strip. (-0)";
        }

        // End the timer UI and close after a short delay
        _timeRemaining = 0f;
        UpdateTimerUI();

        _closeRoutine = StartCoroutine(CloseAfterSeconds(0.6f));
    }

    private int EvaluateReduction(float markerX)
    {
        // If the marker is outside the zone strip, give 0 relief
        if (markerX < _stripLeftX || markerX > _stripRightX)
            return 0;

        // Marker position gets compared against cached zone bounds
        if (markerX >= _greenLeftX && markerX <= _greenRightX) return 3;

        bool inYellow1 = markerX >= _yellowLeft1X && markerX <= _yellowRight1X;
        bool inYellow2 = markerX >= _yellowLeft2X && markerX <= _yellowRight2X;
        if (inYellow1 || inYellow2) return 2;

        return 1;
    }

    private void SetupStripChildrenLayout()
    {
        // zonesStrip needs 5 child images in order: R, Y, G, Y, R
        if (zonesStrip.childCount < 5)
        {
            Debug.LogError("BreathingMinigame: zonesStrip needs 5 children (R,Y,G,Y,R).");
            return;
        }

        // Convert the fractions (green/yellow) into final red/yellow/green fractions that fit
        GetZoneFractions(out float redF, out float yellowF, out float greenF);

        // Build anchor ranges across the strip (0..1)
        float a0 = 0f;
        float a1 = a0 + redF;
        float a2 = a1 + yellowF;
        float a3 = a2 + greenF;
        float a4 = a3 + yellowF;
        float a5 = 1f;

        SetChildAnchors(zonesStrip.GetChild(0) as RectTransform, a0, a1); // Red
        SetChildAnchors(zonesStrip.GetChild(1) as RectTransform, a1, a2); // Yellow
        SetChildAnchors(zonesStrip.GetChild(2) as RectTransform, a2, a3); // Green
        SetChildAnchors(zonesStrip.GetChild(3) as RectTransform, a3, a4); // Yellow
        SetChildAnchors(zonesStrip.GetChild(4) as RectTransform, a4, a5); // Red
    }

    private void SetChildAnchors(RectTransform rt, float minX, float maxX)
    {
        if (rt == null) return;

        // Anchors define what % of the strip width each zone takes up
        rt.anchorMin = new Vector2(minX, 0f);
        rt.anchorMax = new Vector2(maxX, 1f);

        // Keep it flush inside the strip
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

        // Center-anchored so we can slide left/right within the track
        zonesStrip.pivot = new Vector2(0.5f, 0.5f);
        zonesStrip.anchorMin = new Vector2(0.5f, 0.5f);
        zonesStrip.anchorMax = new Vector2(0.5f, 0.5f);

        // Strip size based on track width
        float stripWidth = Mathf.Clamp(stripWidthOfTrack, 0.05f, 0.95f) * trackWidth;

        zonesStrip.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, stripWidth);
        zonesStrip.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, trackArea.rect.height);

        // Choose a center position that keeps the strip fully on the track
        float centerX = 0f;

        if (randomizeEachOpen)
        {
            float minCenterX = -trackWidth * 0.5f + stripWidth * 0.5f;
            float maxCenterX = trackWidth * 0.5f - stripWidth * 0.5f;
            centerX = Random.Range(minCenterX, maxCenterX);
        }

        zonesStrip.anchoredPosition = new Vector2(centerX, 0f);

        // Cache zone bounds for scoring (matches the visual layout)
        GetZoneFractions(out float redF, out float yellowF, out float greenF);

        float stripLeftX = centerX - stripWidth * 0.5f;

        // Cache full strip bounds (outside this = 0)
        _stripLeftX = stripLeftX;
        _stripRightX = stripLeftX + stripWidth;

        float redW = stripWidth * redF;
        float yellowW = stripWidth * yellowF;
        float greenW = stripWidth * greenF;

        _yellowLeft1X = stripLeftX + redW;
        _yellowRight1X = _yellowLeft1X + yellowW;

        _greenLeftX = _yellowRight1X;
        _greenRightX = _greenLeftX + greenW;

        _yellowLeft2X = _greenRightX;
        _yellowRight2X = _yellowLeft2X + yellowW;
    }

    private void GetZoneFractions(out float redF, out float yellowF, out float greenF)
    {
        // Clamp input values
        greenF = Mathf.Clamp01(greenFracOfStrip);
        yellowF = Mathf.Clamp01(yellowFracOfStrip);

        // Red is whatever is left, split on both sides
        redF = (1f - greenF - 2f * yellowF) * 0.5f;

        // If values don't fit, force some red and scale yellow/green down
        if (redF < 0.02f)
        {
            redF = 0.02f;

            float remaining = 1f - 2f * redF;
            float totalGY = (2f * yellowF + greenF);
            float scale = remaining / Mathf.Max(0.0001f, totalGY);

            yellowF *= scale;
            greenF *= scale;
        }
    }

    private void UpdateTimerUI()
    {
        if (timerFill == null) return;

        float pct = Mathf.Clamp01(_timeRemaining / Mathf.Max(0.0001f, timeLimitSeconds));
        timerFill.fillAmount = pct;
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
        // Minigame uses text
        if (usesText != null)
            usesText.text = "Uses: " + UsesLeft + "/" + MaxUsesPerNight;

        // Optional HUD uses text
        if (nightUsesText != null)
            nightUsesText.text = string.Format(nightUsesFormat, UsesLeft, MaxUsesPerNight);
    }
}