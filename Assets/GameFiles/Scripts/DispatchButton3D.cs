using UnityEngine;

public class DispatchButton3D : MonoBehaviour
{
    public CallManager callManager;
    public DispatchService service;

    private void OnMouseDown()
    {
        if (callManager == null) return;
        callManager.Dispatch(service);
    }
}
