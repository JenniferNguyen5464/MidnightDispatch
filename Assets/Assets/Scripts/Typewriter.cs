using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Typewriter : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private TMP_Text targetText;                 // The text field that displays the transcript
    [SerializeField, Min(1f)] private float charsPerSecond = 45f; // Typing speed (visible characters per second)

    private Coroutine _running;
    private string _currentFullText = "";

    // True while the typing coroutine is running
    public bool IsTyping => _running != null;

    // Allows swapping the output text field at runtime (ex: different UI panels)
    public void SetTarget(TMP_Text text) => targetText = text;

    public void StartTyping(string fullText)
    {
        // Starts a fresh typing run (any previous run gets cancelled)
        StopTyping();

        if (targetText == null) return;

        _currentFullText = fullText ?? string.Empty;

        // TextMeshPro can “reveal” characters without constantly rebuilding strings.
        // This avoids per-character string allocations and stays smooth on long transcripts.
        targetText.text = _currentFullText;
        targetText.maxVisibleCharacters = 0;

        // Forces TMP to calculate character count immediately (important for rich text / tags)
        targetText.ForceMeshUpdate();

        _running = StartCoroutine(TypeRoutine());
    }

    public void StopTyping()
    {
        // Cancels typing without changing whatever is currently visible
        if (_running != null) StopCoroutine(_running);
        _running = null;
    }

    public void SkipToEnd()
    {
        // Instantly finishes typing and shows the full text
        if (targetText == null) return;

        StopTyping();
        targetText.text = _currentFullText;
        targetText.maxVisibleCharacters = int.MaxValue;
    }

    private IEnumerator TypeRoutine()
    {
        // Total visible characters (excludes markup tags)
        int totalChars = targetText.textInfo.characterCount;

        float delay = 1f / Mathf.Max(1f, charsPerSecond);
        WaitForSeconds wait = new WaitForSeconds(delay);

        for (int visible = 1; visible <= totalChars; visible++)
        {
            targetText.maxVisibleCharacters = visible;
            yield return wait;
        }

        // Typing complete
        _running = null;
    }
}