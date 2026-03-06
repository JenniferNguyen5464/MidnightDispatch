using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Typewriter : MonoBehaviour
{
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private float charsPerSecond = 45f;

    private Coroutine typingRoutine;
    private string fullText = "";

    // True while the typewriter is running
    public bool IsTyping => typingRoutine != null;

    public void SetTarget(TMP_Text text)
    {
        targetText = text;
    }

    public void StartTyping(string text)
    {
        StopTyping();

        if (targetText == null) return;

        fullText = text == null ? "" : text;

        // Set full text once, then reveal it over time
        targetText.text = fullText;
        targetText.maxVisibleCharacters = 0;

        // Makes sure TMP knows how many visible characters there are (handles rich text)
        targetText.ForceMeshUpdate();

        typingRoutine = StartCoroutine(TypeRoutine());
    }

    public void StopTyping()
    {
        if (typingRoutine != null)
            StopCoroutine(typingRoutine);

        typingRoutine = null;
    }

    public void SkipToEnd()
    {
        if (targetText == null) return;

        StopTyping();
        targetText.text = fullText;
        targetText.maxVisibleCharacters = int.MaxValue;
    }

    private IEnumerator TypeRoutine()
    {
        int totalChars = targetText.textInfo.characterCount;

        float delay = 1f / Mathf.Max(1f, charsPerSecond);

        for (int visible = 1; visible <= totalChars; visible++)
        {
            targetText.maxVisibleCharacters = visible;
            yield return new WaitForSeconds(delay);
        }

        typingRoutine = null;
    }
}