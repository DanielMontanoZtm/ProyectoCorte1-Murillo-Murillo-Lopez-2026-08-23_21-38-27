using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Centralises mobile input for the cauldron.
/// Supports two modes (set via <see cref="InputMode"/>):
///
///   TouchDrag  — swipe left/right anywhere on screen to move the cauldron.
///   Buttons    — press left/right on-screen buttons (assign via UI buttons
///                calling SetLeftPressed / SetRightPressed).
///
/// HorizontalInput is consumed by CauldronController each FixedUpdate.
/// A virtual joystick alternative can be layered on top later.
/// </summary>
public class MobileInputHandler : MonoBehaviour
{
    public static MobileInputHandler Instance { get; private set; }

    public enum InputMode { TouchDrag, Buttons }

    [Header("Input mode")]
    [SerializeField] private InputMode mode = InputMode.TouchDrag;

    [Header("Touch drag sensitivity")]
    [SerializeField] private float dragSensitivity = 0.01f;

    // ── Exposed value (-1 … 1) ────────────────────────────────────────────────
    public float HorizontalInput { get; private set; }

    // ── Internal state ────────────────────────────────────────────────────────
    private bool  _leftPressed;
    private bool  _rightPressed;
    private int   _activeTouchId = -1;
    private float _lastTouchX;

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        HorizontalInput = 0f;

        switch (mode)
        {
            case InputMode.TouchDrag:
                HandleTouchDrag();
                break;

            case InputMode.Buttons:
                HandleButtons();
                break;
        }

        // Editor / PC fallback
#if UNITY_EDITOR
        float kb = Input.GetAxis("Horizontal");
        if (Mathf.Abs(kb) > 0.01f) HorizontalInput = kb;
#endif
    }

    // ── Touch drag ────────────────────────────────────────────────────────────

    private void HandleTouchDrag()
    {
        if (Input.touchCount == 0) return;

        foreach (Touch touch in Input.touches)
        {
            // Ignore touches over UI elements
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                continue;

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    if (_activeTouchId == -1)
                    {
                        _activeTouchId = touch.fingerId;
                        _lastTouchX    = touch.position.x;
                    }
                    break;

                case TouchPhase.Moved:
                    if (touch.fingerId == _activeTouchId)
                    {
                        float delta      = touch.position.x - _lastTouchX;
                        _lastTouchX      = touch.position.x;
                        HorizontalInput  = Mathf.Clamp(delta * dragSensitivity, -1f, 1f);
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (touch.fingerId == _activeTouchId)
                        _activeTouchId = -1;
                    break;
            }
        }
    }

    // ── Button presses (called by Unity UI Button events) ────────────────────

    private void HandleButtons()
    {
        if      (_leftPressed  && !_rightPressed) HorizontalInput = -1f;
        else if (_rightPressed && !_leftPressed)  HorizontalInput =  1f;
        else                                      HorizontalInput =  0f;
    }

    /// <summary>Called by the left on-screen button (PointerDown event).</summary>
    public void SetLeftPressed(bool pressed)  => _leftPressed  = pressed;

    /// <summary>Called by the right on-screen button (PointerDown event).</summary>
    public void SetRightPressed(bool pressed) => _rightPressed = pressed;

    /// <summary>Switch input mode at runtime (e.g. from settings menu).</summary>
    public void SetMode(InputMode newMode) => mode = newMode;
}
