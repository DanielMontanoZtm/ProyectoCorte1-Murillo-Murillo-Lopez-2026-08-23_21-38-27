using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch      = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

/// <summary>
/// Centralises mobile input for the cauldron.
/// Uses the NEW Input System (EnhancedTouch API) — compatible with
/// activeInputHandler = 1 (Input System only).
///
/// Supports two modes:
///   TouchDrag  — swipe left/right anywhere on screen.
///   Buttons    — on-screen left/right buttons (call SetLeftPressed /
///                SetRightPressed from UI Button events).
///
/// Editor fallback uses Keyboard (new Input System) for ← → arrow keys.
/// </summary>
public class MobileInputHandler : MonoBehaviour
{
    public static MobileInputHandler Instance { get; private set; }

    public enum InputMode { TouchDrag, Buttons }

    [Header("Input mode")]
    [SerializeField] private InputMode mode = InputMode.TouchDrag;

    [Header("Touch drag sensitivity")]
    [Tooltip("Higher = faster cauldron response per pixel of swipe.")]
    [SerializeField] private float dragSensitivity = 0.012f;

    // ── Exposed value (-1 … 1) consumed by CauldronController ────────────────
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

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
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

        // Editor / PC keyboard fallback (new Input System)
#if UNITY_EDITOR
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null)
        {
            float kb = 0f;
            if (keyboard.leftArrowKey.isPressed  || keyboard.aKey.isPressed) kb = -1f;
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) kb =  1f;
            if (kb != 0f) HorizontalInput = kb;
        }
#endif
    }

    // ── Touch drag (EnhancedTouch API) ────────────────────────────────────────
    private void HandleTouchDrag()
    {
        var activeTouches = Touch.activeTouches;
        if (activeTouches.Count == 0) return;

        foreach (var touch in activeTouches)
        {
            // Ignore touches over UI elements
            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject(touch.touchId))
                continue;

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    if (_activeTouchId == -1)
                    {
                        _activeTouchId = touch.touchId;
                        _lastTouchX    = touch.screenPosition.x;
                    }
                    break;

                case TouchPhase.Moved:
                    if (touch.touchId == _activeTouchId)
                    {
                        float delta     = touch.screenPosition.x - _lastTouchX;
                        _lastTouchX     = touch.screenPosition.x;
                        HorizontalInput = Mathf.Clamp(delta * dragSensitivity, -1f, 1f);
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (touch.touchId == _activeTouchId)
                        _activeTouchId = -1;
                    break;
            }
        }
    }

    // ── Button mode ───────────────────────────────────────────────────────────
    private void HandleButtons()
    {
        if      (_leftPressed  && !_rightPressed) HorizontalInput = -1f;
        else if (_rightPressed && !_leftPressed)  HorizontalInput =  1f;
    }

    /// <summary>Called by the left on-screen button (PointerDown / PointerUp).</summary>
    public void SetLeftPressed(bool pressed)  => _leftPressed  = pressed;

    /// <summary>Called by the right on-screen button (PointerDown / PointerUp).</summary>
    public void SetRightPressed(bool pressed) => _rightPressed = pressed;

    /// <summary>Switch input mode at runtime.</summary>
    public void SetMode(InputMode newMode) => mode = newMode;
}
