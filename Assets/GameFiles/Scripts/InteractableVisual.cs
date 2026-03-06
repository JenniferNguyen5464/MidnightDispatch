using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class InteractableVisual : MonoBehaviour
{
    [SerializeField] private Collider targetCollider;
    [SerializeField] private Renderer[] renderersToDim;

    [SerializeField] private float disabledBrightness = 0.35f;
    [SerializeField] private bool alsoDisableCollider = true;

    private bool initialized = false;

    private Material[][] materials;
    private string[][] colorProperty;     // "_BaseColor" (URP) or "_Color" (built-in)
    private Color[][] originalColors;

    private void Awake()
    {
        InitIfNeeded();
    }

    private void InitIfNeeded()
    {
        if (initialized) return;

        if (targetCollider == null)
            targetCollider = GetComponent<Collider>();

        if (renderersToDim == null || renderersToDim.Length == 0)
            renderersToDim = GetComponentsInChildren<Renderer>(true);

        materials = new Material[renderersToDim.Length][];
        colorProperty = new string[renderersToDim.Length][];
        originalColors = new Color[renderersToDim.Length][];

        for (int r = 0; r < renderersToDim.Length; r++)
        {
            Renderer rend = renderersToDim[r];
            if (rend == null) continue;

            // Use .materials so changes only affect this object (not shared across the project)
            Material[] mats = rend.materials;

            materials[r] = mats;
            colorProperty[r] = new string[mats.Length];
            originalColors[r] = new Color[mats.Length];

            for (int m = 0; m < mats.Length; m++)
            {
                Material mat = mats[m];
                if (mat == null) continue;

                string prop = null;
                if (mat.HasProperty("_BaseColor")) prop = "_BaseColor"; // URP
                else if (mat.HasProperty("_Color")) prop = "_Color";    // Built-in

                colorProperty[r][m] = prop;
                originalColors[r][m] = (prop != null) ? mat.GetColor(prop) : Color.white;
            }
        }

        initialized = true;
    }

    public void SetInteractable(bool canClick)
    {
        InitIfNeeded();

        if (alsoDisableCollider && targetCollider != null)
            targetCollider.enabled = canClick;

        float mult = canClick ? 1f : disabledBrightness;

        for (int r = 0; r < materials.Length; r++)
        {
            if (materials[r] == null) continue;

            for (int m = 0; m < materials[r].Length; m++)
            {
                Material mat = materials[r][m];
                if (mat == null) continue;

                string prop = colorProperty[r][m];
                if (prop == null) continue;

                Color baseCol = originalColors[r][m];
                Color dimCol = new Color(baseCol.r * mult, baseCol.g * mult, baseCol.b * mult, baseCol.a);
                mat.SetColor(prop, dimCol);
            }
        }
    }
}