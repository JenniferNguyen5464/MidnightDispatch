using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PressureMeterUI : MonoBehaviour
{
    // Fill ticks in order: Fill_01 ... Fill_09
    [SerializeField] private Image[] _fillTicks;

    public void SetPressureTicks(int ticksFilled)
    {
        ticksFilled = Mathf.Clamp(ticksFilled, 0, _fillTicks.Length);

        for (int i = 0; i < _fillTicks.Length; i++)
        {
            _fillTicks[i].gameObject.SetActive(i < ticksFilled);
        }
    }
}
