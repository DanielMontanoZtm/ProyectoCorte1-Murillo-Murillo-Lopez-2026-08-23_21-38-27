using UnityEngine;

/// <summary>
/// Scene-level bootstrap. Attach to an empty GameObject named "Bootstrap"
/// in the Main scene.
///
/// Responsibilities:
///   1. Ensures all singleton managers exist (creates them if missing).
///   2. Attaches CameraController to the Main Camera (aspect-ratio-aware setup).
///   3. Optionally auto-starts game and spawner (useful during dev/testing).
///
/// CAMBIO v2:
///   - Camera setup delegado a CameraController (ya no fija orthographic size
///     ni posición hardcodeada aquí).  GameBootstrap solo garantiza que el
///     componente exista en la cámara.
///   - CollisionFeedback añadido a la lista de managers garantizados.
///   - autoStartOnLoad útil para testear jugabilidad sin menú todavía.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    [Header("Auto-start (useful while testing without a menu)")]
    [SerializeField] private bool autoStartOnLoad = true;

    private void Awake()
    {
        EnsureManagers();
        EnsureCamera();
    }

    private void Start()
    {
        if (autoStartOnLoad)
        {
            GameManager.Instance?.StartGame();
            ObjectSpawner.Instance?.StartSpawning();
        }
    }

    // ── Camera ────────────────────────────────────────────────────────────────

    private void EnsureCamera()
    {
        Camera main = Camera.main;

        if (main == null)
        {
            // Only create a camera if there genuinely isn't one in the scene
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            main      = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
            Debug.Log("[GameBootstrap] Created Main Camera.");
        }

        // Add CameraController only if not already present.
        // It will NOT move the camera — only adjusts FOV for the device aspect ratio.
        if (main.GetComponent<CameraController>() == null)
        {
            main.gameObject.AddComponent<CameraController>();
            Debug.Log("[GameBootstrap] Added CameraController to Main Camera.");
        }
    }

    // ── Manager presence check ────────────────────────────────────────────────

    private void EnsureManagers()
    {
        Ensure<GameManager>("GameManager");
        Ensure<LevelManager>("LevelManager");
        Ensure<ObjectSpawner>("ObjectSpawner");
        Ensure<MobileInputHandler>("MobileInputHandler");
        Ensure<CollisionFeedback>("CollisionFeedback");
    }

    private static T Ensure<T>(string goName) where T : Component
    {
        // FindAnyObjectByType is the non-deprecated replacement in Unity 6
        // for FindFirstObjectByType (all overloads of the latter are obsolete).
        T existing = FindAnyObjectByType<T>();
        if (existing != null) return existing;

        var go   = new GameObject(goName);
        var comp = go.AddComponent<T>();
        Debug.Log($"[GameBootstrap] Created {goName}.");
        return comp;
    }
}
