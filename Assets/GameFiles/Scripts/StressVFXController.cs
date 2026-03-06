using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StressVFXController : MonoBehaviour
{
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private CanvasGroup blackoutGroup;

    [SerializeField] private float wobbleStrengthAtZero = 0f;
    [SerializeField] private float wobbleStrengthAtMax = 0.36f;
    [SerializeField] private float wobbleSpeedAtZero = 1.2f;
    [SerializeField] private float wobbleSpeedAtMax = 3f;

    [SerializeField] private float jitterStrength = 0.06f;
    [SerializeField] private float jitterDuration = 0.12f;

    [SerializeField] private float blackoutAlpha = 0.85f;
    [SerializeField] private float fadeInTime = 0.05f;
    [SerializeField] private float holdTime = 0.08f;
    [SerializeField] private float fadeOutTime = 0.25f;

    [SerializeField] private bool enableDebugKeys = false;
    [SerializeField] private int debugMaxTicks = 9;

    private float intensity01 = 0f;
    private Vector3 startLocalPos;
    private Vector3 jitterOffset = Vector3.zero;

    private Coroutine jitterRoutine;
    private Coroutine blackoutRoutine;

    private void Awake()
    {
        if (cameraTarget != null)
            startLocalPos = cameraTarget.localPosition;

        if (blackoutGroup != null)
        {
            blackoutGroup.alpha = 0f;
            blackoutGroup.blocksRaycasts = false;
            blackoutGroup.interactable = false;
        }
    }

    private void Update()
    {
        if (!enableDebugKeys) return;

        float step = 1f / Mathf.Max(1, debugMaxTicks);

        if (Input.GetKeyDown(KeyCode.UpArrow))
            SetPressureIntensity01(intensity01 + step);

        if (Input.GetKeyDown(KeyCode.DownArrow))
            SetPressureIntensity01(intensity01 - step);

        if (Input.GetKeyDown(KeyCode.M))
            PlayMistakeEffects();
    }

    private void LateUpdate()
    {
        if (cameraTarget == null) return;

        float strength = Mathf.Lerp(wobbleStrengthAtZero, wobbleStrengthAtMax, intensity01);
        float speed = Mathf.Lerp(wobbleSpeedAtZero, wobbleSpeedAtMax, intensity01);

        // Smooth wobble
        float x = (Mathf.PerlinNoise(Time.time * speed, 10f) - 0.5f) * 2f;
        float y = (Mathf.PerlinNoise(20f, Time.time * speed) - 0.5f) * 2f;

        Vector3 wobbleOffset = new Vector3(x, y, 0f) * strength;

        cameraTarget.localPosition = startLocalPos + wobbleOffset + jitterOffset;
    }

    // Called by PressureManager (CurrentTicks / maxTicks)
    public void SetPressureIntensity01(float newIntensity01)
    {
        intensity01 = Mathf.Clamp01(newIntensity01);
    }

    // Called when a mistake happens
    public void PlayMistakeEffects()
    {
        if (jitterRoutine != null) StopCoroutine(jitterRoutine);
        jitterRoutine = StartCoroutine(Jitter());

        if (blackoutRoutine != null) StopCoroutine(blackoutRoutine);
        blackoutRoutine = StartCoroutine(BlackFlash());
    }

    private IEnumerator Jitter()
    {
        float t = 0f;

        while (t < jitterDuration)
        {
            t += Time.deltaTime;

            float x = Random.Range(-1f, 1f) * jitterStrength;
            float y = Random.Range(-1f, 1f) * jitterStrength;
            jitterOffset = new Vector3(x, y, 0f);

            yield return null;
        }

        jitterOffset = Vector3.zero;
    }

    private IEnumerator BlackFlash()
    {
        if (blackoutGroup == null) yield break;

        blackoutGroup.alpha = 0f;

        float t = 0f;
        while (t < fadeInTime)
        {
            t += Time.deltaTime;
            blackoutGroup.alpha = Mathf.Lerp(0f, blackoutAlpha, t / fadeInTime);
            yield return null;
        }

        blackoutGroup.alpha = blackoutAlpha;
        yield return new WaitForSeconds(holdTime);

        t = 0f;
        while (t < fadeOutTime)
        {
            t += Time.deltaTime;
            blackoutGroup.alpha = Mathf.Lerp(blackoutAlpha, 0f, t / fadeOutTime);
            yield return null;
        }

        blackoutGroup.alpha = 0f;
    }
}