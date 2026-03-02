using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BreathingMinigame : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject panelRoot;          // Monitor UI panel root
    [SerializeField] private Slider slider;                // Moving indicator (handle)
    [SerializeField] private RectTransform trackArea;       // Visible bar/slot area
    [SerializeField] private RectTransform zonesStrip;      // Contains 5 child Images (R,Y,G,Y,R)
    [SerializeField] private PressureManager pressureManager;

    [Header("UI Text (optional)")]
    [SerializeField] private TMP_Text resultText;           // text inside minigame (optional)
    [SerializeField] private TMP_Text usesText;             // text inside minigame (optional)
    [SerializeField] private TMP_Text nightUsesText;        // text on HUD / always visible (optional)
    [SerializeField] private string nightUsesFormat = "Paper Bags: {0}/{1}";

    [Header("Timer UI (optional)")]
    [SerializeField] private Image timerFill;              // Image with Fill Amount (like your call timer)
    [SerializeField, Min(0.5f)] private float timeLimitSeconds = 3.0f;

    [Header("Uses")]
    [SerializeField] private int maxUsesPerNight = 3;
    public int UsesLeft { get; private set; }
    public int MaxUsesPerNight => Mathf.Max(1, maxUsesPerNight);

    [Header("Bar Movement")]
    [SerializeField, Min(0.1f)] private float speed = 1.2f;

    [Header("Zones Size + Randomization")]
    [SerializeField, Range(0.05f, 0.9f)] private float stripWidthOfTrack = 0.22f;
    [SerializeField, Range(0.05f, 0.8f)] private float greenFracOfStrip = 0.25f;
    [SerializeField, Range(0.05f, 0.45f)] private float yellowFracOfStrip = 0.18f;
    [SerializeField] private bool randomizeEachOpen = true;

    private bool _active;
    private bool _locked;
    private float _t;

    private float _timeRemaining;

    // Cached boundaries in TRACK-LOCAL X coordinates
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
        if (panelRoot == null || slider == null || trackArea == null || zonesStrip == null || pressureManager == null)
        {
            Debug.LogError("BreathingMinigame: Missing references. Assign panelRoot, slider, trackArea, zonesStrip, pressureManager.");
            return;
        }

        if (_active) return;
        if (UsesLeft <= 0) return;

        // Stop any pending close coroutine
        if (_closeRoutine != null) StopCoroutine(_closeRoutine);
        _closeRoutine = null;

        _active = true;
        _locked = false;
        _t = 0f;

        // Timer
        timeLimitSeconds = Mathf.Max(0.5f, timeLimitSeconds);
        _timeRemaining = timeLimitSeconds;
        UpdateTimerUI();

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;

        SetupStripChildrenLayout();
        if (randomizeEachOpen) RandomizeStripPositionAndCacheBounds();

        if (resultText != null) resultText.text = "Press SPACE to breathe";
        RefreshUsesUI();

        panelRoot.SetActive(true);
    }

    private void Update()
    {
        if (!_active) return;

        if (!_locked)
        {
            // Timer countdown
            _timeRemaining -= Time.deltaTime;
            UpdateTimerUI();

            // Move handle back and forth
            _t += Time.deltaTime * speed;
            slider.value = Mathf.PingPong(_t, 1f);

            // Player stop
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _locked = true;
                ResolveStop(slider.value);
                return;
            }

            // Timeout -> auto resolve
            if (_timeRemaining <= 0f)
            {
                _locked = true;
                ResolveStop(slider.value);
                return;
            }
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

        int reduceBy = EvaluateReduction(markerX);

        // Apply relief
        pressureManager.ReduceTicks(reduceBy);

        // Consume a use
        UsesLeft = Mathf.Max(0, UsesLeft - 1);
        RefreshUsesUI();

        if (resultText != null)
            resultText.text = reduceBy == 3 ? "Perfect breath! (-3)" :
                              reduceBy == 2 ? "Good breath! (-2)" :
                                              "Breath caught. (-1)";

        _timeRemaining = 0f;
        UpdateTimerUI();

        _closeRoutine = StartCoroutine(CloseAfterSeconds(0.6f));
    }

    private int EvaluateReduction(float markerX)
    {
        if (markerX >= _greenLeftX && markerX <= _greenRightX) return 3;

        bool inYellow1 = markerX >= _yellowLeft1X && markerX <= _yellowRight1X;
        bool inYellow2 = markerX >= _yellowLeft2X && markerX <= _yellowRight2X;
        if (inYellow1 || inYellow2) return 2;

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

        float a0 = 0f;
        float a1 = a0 + redF;
        float a2 = a1 + yellowF;
        float a3 = a2 + greenF;
        float a4 = a3 + yellowF;
        float a5 = 1f;

        SetChildAnchors(zonesStrip.GetChild(0) as RectTransform, a0, a1); // RedL
        SetChildAnchors(zonesStrip.GetChild(1) as RectTransform, a1, a2); // YellowL
        SetChildAnchors(zonesStrip.GetChild(2) as RectTransform, a2, a3); // Green
        SetChildAnchors(zonesStrip.GetChild(3) as RectTransform, a3, a4); // YellowR
        SetChildAnchors(zonesStrip.GetChild(4) as RectTransform, a4, a5); // RedR
    }

    private void SetChildAnchors(RectTransform rt, float minX, float maxX)
    {
        if (rt == null) return;

        rt.anchorMin = new Vector2(minX, 0f);
        rt.anchorMax = new Vector2(maxX, 1f);
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

        zonesStrip.pivot = new Vector2(0.5f, 0.5f);
        zonesStrip.anchorMin = new Vector2(0.5f, 0.5f);
        zonesStrip.anchorMax = new Vector2(0.5f, 0.5f);

        float stripWidth = Mathf.Clamp(stripWidthOfTrack, 0.05f, 0.95f) * trackWidth;

        zonesStrip.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, stripWidth);
        zonesStrip.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, trackArea.rect.height);

        float minCenterX = -trackWidth * 0.5f + stripWidth * 0.5f;
        float maxCenterX = trackWidth * 0.5f - stripWidth * 0.5f;
        float centerX = Random.Range(minCenterX, maxCenterX);

        zonesStrip.anchoredPosition = new Vector2(centerX, 0f);

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

        float stripLeftX = centerX - stripWidth * 0.5f;

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