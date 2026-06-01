using UnityEngine;
using UnityEngine.InputSystem;

public class PrimaryButtonColorChanger : MonoBehaviour
{
    [SerializeField] InputActionReference primaryButtonAction;
    [SerializeField] Renderer targetRenderer;

    int colorIndex;
    readonly Color[] colors =
    {
        new Color(0.1f, 0.45f, 1f),
        new Color(1f, 0.35f, 0.2f),
        new Color(0.25f, 0.85f, 0.35f),
        new Color(1f, 0.85f, 0.2f),
    };

    void OnEnable()
    {
        if (primaryButtonAction != null)
        {
            primaryButtonAction.action.performed += OnPrimaryButton;
            primaryButtonAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (primaryButtonAction != null)
            primaryButtonAction.action.performed -= OnPrimaryButton;
    }

    void OnPrimaryButton(InputAction.CallbackContext context)
    {
        if (targetRenderer == null)
            return;

        colorIndex = (colorIndex + 1) % colors.Length;
        targetRenderer.material.color = colors[colorIndex];
    }
}
