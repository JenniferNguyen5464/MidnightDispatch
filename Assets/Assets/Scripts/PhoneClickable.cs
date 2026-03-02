using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhoneClickable : MonoBehaviour
{
    [SerializeField] private CallManager callManager;

    private void OnMouseDown()
    {
        if (callManager != null)
            callManager.PhonePickup();
    }
}
