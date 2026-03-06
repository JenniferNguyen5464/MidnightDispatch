using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PhoneClickable : MonoBehaviour
{
    [SerializeField] private CallManager callManager;
    [SerializeField] private InteractableVisual visual;
    [SerializeField] private bool startClickable = false;

    private bool canClick;

    private void Awake()
    {
        if (visual == null)
            visual = GetComponent<InteractableVisual>();

        SetCanClick(startClickable);
    }

    // CallManager uses this to enable/disable the phone (ringing = clickable)
    public void SetCanClick(bool value)
    {
        canClick = value;

        if (visual != null)
            visual.SetInteractable(value);
    }

    private void OnMouseDown()
    {
        if (!canClick) return;
        if (callManager == null) return;

        callManager.PhonePickup();

        // Disable until CallManager turns it back on
        SetCanClick(false);
    }
}