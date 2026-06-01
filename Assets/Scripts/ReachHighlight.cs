using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRBaseInteractable))]
public class ReachHighlight : MonoBehaviour
{
    [SerializeField] Color hoverColor = new Color(1f, 0.9f, 0.15f, 1f);
    [SerializeField] float hoverScale = 1.08f;

    XRBaseInteractable interactable;
    Renderer[] renderers;
    Color[] originalColors;
    Vector3 originalScale;

    void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (var i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].material.color;

        originalScale = transform.localScale;
    }

    void OnEnable()
    {
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
    }

    void OnDisable()
    {
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        SetHighlighted(true);
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        SetHighlighted(false);
    }

    void SetHighlighted(bool highlighted)
    {
        transform.localScale = highlighted ? originalScale * hoverScale : originalScale;

        for (var i = 0; i < renderers.Length; i++)
            renderers[i].material.color = highlighted ? hoverColor : originalColors[i];
    }
}
