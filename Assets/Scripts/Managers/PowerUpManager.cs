using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PowerUpManager — observador pasivo del estado del power-up.
///
/// CAMBIO v2 (instantaneous power-up):
///   El power-up ya NO se activa mediante un botón de HUD.  Se activa
///   automáticamente en GameManager cuando el caldero recoge el objeto
///   PowerUp y la carga llega al 100%.
///
///   Este componente ahora actúa únicamente como OBSERVADOR:
///     • Escucha OnPowerUpChargeChanged  → actualiza barra de carga (para HUD futuro).
///     • Escucha OnPowerUpStateChanged   → actualiza icono y estado visual.
///     • El botón de HUD, si existe en escena, se deshabilita completamente
///       para que el jugador no pueda confundirse.
///
///   Todas las referencias de UI son opcionales (la jugabilidad funciona
///   aunque el Canvas no esté en escena todavía).
///
/// Coloca este componente en cualquier GameObject de la escena; no requiere
/// Canvas para funcionar.
/// </summary>
public class PowerUpManager : MonoBehaviour
{
    [Header("HUD references (opcional — se ignoran si son null)")]
    [SerializeField] private Button powerUpButton;   // deshabilitado permanentemente en v2
    [SerializeField] private Image  chargeBarFill;
    [SerializeField] private Image  buttonIcon;

    [Header("Visual state colors")]
    [SerializeField] private Color chargedColor  = new Color(1f,  0.85f, 0f);   // gold — active
    [SerializeField] private Color inactiveColor = new Color(0.5f, 0.5f, 0.5f); // grey — idle

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void OnEnable()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        gm.OnPowerUpChargeChanged.AddListener(OnChargeChanged);
        gm.OnPowerUpStateChanged.AddListener(OnPowerUpStateChanged);
    }

    private void OnDisable()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        gm.OnPowerUpChargeChanged.RemoveListener(OnChargeChanged);
        gm.OnPowerUpStateChanged.RemoveListener(OnPowerUpStateChanged);
    }

    private void Start()
    {
        // The button should never be interactive — activation is automatic now.
        // Disable it so players don't expect to tap it.
        if (powerUpButton != null)
        {
            powerUpButton.interactable = false;
            Debug.Log("[PowerUpManager] HUD power-up button permanently disabled " +
                      "(power-up activates instantly on catch).");
        }
    }

    // ── Event callbacks ───────────────────────────────────────────────────────

    private void OnChargeChanged(float charge)
    {
        if (chargeBarFill != null)
            chargeBarFill.fillAmount = charge;
    }

    private void OnPowerUpStateChanged(bool isActive)
    {
        if (buttonIcon != null)
            buttonIcon.color = isActive ? chargedColor : inactiveColor;

        // Keep button non-interactive regardless of state
        if (powerUpButton != null)
            powerUpButton.interactable = false;
    }
}
