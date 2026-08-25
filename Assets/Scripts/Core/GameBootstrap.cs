using UnityEngine;

/// <summary>
/// Scene-level bootstrap.  Place this on an empty GameObject called
/// "Bootstrap" in Main.unity.
///
/// It ensures all singleton managers are present (creates them if missing),
/// positions the main camera, and starts the spawn system when the game
/// transitions to Playing state.
///
/// Camera setup:
///   - Orthographic, looking straight down the Z axis (side/front view)
///   - Positioned so the full play lane is visible on a mobile portrait screen
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    [Header("Camera settings")]
    [SerializeField] private float cameraOrthographicSize = 6f;
    [SerializeField] private Vector3 cameraPosition       = new Vector3(0f, 4f, -12f);
    [SerializeField] private Vector3 cameraRotation       = new Vector3(10f, 0f, 0f);

    [Header("Auto-start game on scene load")]
    [SerializeField] private bool autoStartOnLoad = false;

    private void Awake()
    {
        EnsureManagers();
        ConfigureCamera();
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

    private void ConfigureCamera()
    {
        Camera main = Camera.main;
        if (main == null)
        {
            // No camera in scene — create one
            GameObject camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            main      = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
            Debug.Log("[GameBootstrap] Created Main Camera.");
        }

        main.orthographic     = false;
        main.fieldOfView      = 60f;
        main.nearClipPlane    = 0.1f;
        main.farClipPlane     = 100f;
        main.transform.position = cameraPosition;
        main.transform.eulerAngles = cameraRotation;
        main.backgroundColor  = new Color(0.06f, 0.04f, 0.12f); // dark purple sky
    }

    // ── Manager presence check ────────────────────────────────────────────────

    private void EnsureManagers()
    {
        Ensure<GameManager>("GameManager");
        Ensure<LevelManager>("LevelManager");
        Ensure<ObjectSpawner>("ObjectSpawner");
        Ensure<MobileInputHandler>("MobileInputHandler");
    }

    private static T Ensure<T>(string goName) where T : Component
    {
        T existing = FindFirstObjectByType<T>();
        if (existing != null) return existing;

        GameObject go = new GameObject(goName);
        T comp = go.AddComponent<T>();
        Debug.Log($"[GameBootstrap] Created {goName}.");
        return comp;
    }
}
