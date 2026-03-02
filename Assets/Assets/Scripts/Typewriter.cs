using System.Collections;
using TMPro;
using UnityEngine;

public class Typewriter : MonoBehaviour
{
    [SerializeField] private TMP_Text targetText;
    [SerializeField, Min(1)] private float charsPerSecond = 45f;

    private Coroutine _running;

    public void SetTarget(TMP_Text text) => targetText = text;

    public void StopTyping()
    {
        if (_running != null) StopCoroutine(_running);
        _running = null;
    }

    public void StartTyping(string fullText)
    {
        StopTyping();
        _running = StartCoroutine(TypeRoutine(fullText));
    }

    private IEnumerator TypeRoutine(string fullText)
    {
        if (targetText == null) yield break;

        targetText.text = "";
        float delay = 1f / charsPerSecond;

        for (int i = 0; i < fullText.Length; i++)
        {
            targetText.text += fullText[i];
            yield return new WaitForSeconds(delay);
        }

        _running = null;
    }

    public bool IsTyping => _running != null;
}
