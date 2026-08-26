using UnityEngine;

/// <summary>
/// Aspect-ratio-aware FOV controller for a portrait mobile game.
///
/// QUÉ HACE:
///   Calcula el Field of View vertical correcto para que el ancho del
///   playfield (xMin..xMax del caldero) sea siempre completamente visible
///   sin importar el aspect ratio del dispositivo.
///
/// QUÉ NO HACE (corrección v3):
///   NO mueve ni rota la cámara. La posición y rotación se configuran
///   manualmente en el Editor y este script las respeta.
///   Solo ajusta camera.fieldOfView.
///
/// PROBLEMA ORIGINAL que esto resuelve:
///   Con orthographicSize fijo o FOV fijo, en dispositivos con aspect ratio
///   distinto a 16:9 el playfield quedaba cortado o con bandas muertas.
///   Con FOV calculado dinámicamente siempre cabe el ancho completo.
///
/// Coloca este componente en la Main Camera.
/// Asigna playFieldHalfWidth igual a xMax del CauldronController (3.8).
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("Play field width (match CauldronController xMax)")]
    [SerializeField] private float playFieldHalfWidth     = 3.8f;
    [SerializeField] private float horizontalMarginUnits  = 0.5f;

    [Header("FOV limits (safety clamp)")]
    [SerializeField] private float minFOV = 30f;
    [SerializeField] private float maxFOV = 120f;

    // ── Internal ──────────────────────────────────────────────────────────────
    private Camera _cam;
    private int    _lastWidth;
    private int    _lastHeight;

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        _cam = GetComponent<Camera>();
        _cam.orthographic = false;

        // Do NOT move or rotate — respect the position set in the Editor.
        RecalculateFOV();
    }

    private void LateUpdate()
    {
        // Recalculate only when resolution changes (editor resize, device rotation)
        if (Screen.width != _lastWidth || Screen.height != _lastHeight)
            RecalculateFOV();
    }

    // ── FOV calculation ───────────────────────────────────────────────────────
    private void RecalculateFOV()
    {
        _lastWidth  = Screen.width;
        _lastHeight = Screen.height;

        float aspect = (float)Screen.width / Mathf.Max(Screen.height, 1);

        // Distance from camera to the play field centre along the forward axis
        // We use the actual camera Z distance to Z=0 (where objects fall).
        float distToField = Mathf.Abs(transform.position.z);
        if (distToField < 0.1f) distToField = 12f; // fallback if camera is at Z=0

        float requiredHalfWidth = playFieldHalfWidth + horizontalMarginUnits;

        // Horizontal FOV needed to show requiredHalfWidth at distToField
        float hFovRad = 2f * Mathf.Atan(requiredHalfWidth / distToField);

        // Convert to vertical FOV (Unity uses vertical FOV internally)
        float vFovRad = 2f * Mathf.Atan(Mathf.Tan(hFovRad * 0.5f) / aspect);
        float vFovDeg = Mathf.Clamp(vFovRad * Mathf.Rad2Deg, minFOV, maxFOV);

        _cam.fieldOfView = vFovDeg;

        Debug.Log($"[CameraController] aspect={aspect:F3}  vFOV={vFovDeg:F1}°  " +
                  $"camPos={transform.position}");
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Visualise the playfield bounds in the Scene view
        Gizmos.color = Color.cyan;
        float hw = playFieldHalfWidth;
        Gizmos.DrawLine(new Vector3(-hw, -2f, 0f), new Vector3( hw, -2f, 0f));
        Gizmos.DrawLine(new Vector3(-hw, 10f, 0f), new Vector3( hw, 10f, 0f));
        Gizmos.DrawLine(new Vector3(-hw, -2f, 0f), new Vector3(-hw, 10f, 0f));
        Gizmos.DrawLine(new Vector3( hw, -2f, 0f), new Vector3( hw, 10f, 0f));

        // Margin
        Gizmos.color = Color.yellow;
        float mw = hw + horizontalMarginUnits;
        Gizmos.DrawLine(new Vector3(-mw, -2f, 0f), new Vector3(-mw, 10f, 0f));
        Gizmos.DrawLine(new Vector3( mw, -2f, 0f), new Vector3( mw, 10f, 0f));
    }
#endif
}
