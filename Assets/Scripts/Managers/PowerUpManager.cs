using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bridges the HUD power-up button with GameManager.TryActivatePowerUp().
/// Attach this to a canvas or manager GameObject.
///
/// The tactile metaphor: player taps the glowing power-up button on the HUD
/// when the charge meter is full.  The button is disabled (greyed out) when
/// the charge is not full, providing clear visual feedback.
/// </summary>
public class PowerUpManager : MonoBehaviour
{
    [Header("HUD Button reference")]
    [SerializeField] private Button powerUpButton;

    [Header("Visual feedback")]
    [SerializeField] private Image  chargeBarFill;      // Image set to Filled type
    [SerializeField] private Image  buttonIcon;
    [SerializeField] private Color  chargedColor   = new Color(0.2f, 1f, 0.5f);
    [SerializeField] private Color  inactiveColor  = new Color(0.5f, 0.5f, 0.5f);

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnPowerUpChargeChanged.AddListener(OnChargeChanged);

        if (powerUpButton != null)
            powerUpButton.onClick.AddListener(OnPowerUpButtonPressed);
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnPowerUpChargeChanged.RemoveListener(OnChargeChanged);

        if (powerUpButton != null)
            powerUpButton.onClick.RemoveListener(OnPowerUpButtonPressed);
    }

    // ── Callbacks ─────────────────────────────────────────────────────────────

    private void OnChargeChanged(float charge)
    {
        bool full = charge >= 1f;

        if (chargeBarFill != null)
            chargeBarFill.fillAmount = charge;

        if (buttonIcon != null)
            buttonIcon.color = full ? chargedColor : inactiveColor;

        if (powerUpButton != null)
            powerUpButton.interactable = full && !GameManager.Instance.PowerUpActive;
    }

    private void OnPowerUpButtonPressed()
    {
        bool activated = GameManager.Instance?.TryActivatePowerUp() ?? false;
        if (activated)
            Debug.Log("[PowerUpManager] Power-up activated!");
    }
}
