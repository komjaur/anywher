using UnityEngine;

[DefaultExecutionOrder(-200)]    // run very early
[DisallowMultipleComponent]
public sealed class GameCamera : MonoBehaviour
{
    /* ───── inspector ───── */
    [Header("Follow Behaviour")]
    [Min(0.01f)] public float lerpSpeed = 5f;

    [Header("View Settings")]
    public Vector3 cameraOffset = new(0, 0, -10);
    public float   orthoSize    = 14f;
    public Color   clearColor   = new(0f, 0.871f, 0.886f);

    /* ───── runtime ───── */
    private Camera   cam;
    private Transform target;
    private Vector3  velocity;
    private ParallaxBackground parallaxBg;

    /* ───── factory ───── */
    public static GameCamera Initialize(Transform followTarget,
                                        Vector3? offset = null,
                                        float?   size   = null,
                                        Color?   back   = null,
                                        string   name   = "GameCamera")
    {
        var go = new GameObject(name);
        var gc = go.AddComponent<GameCamera>();

        if (offset.HasValue) gc.cameraOffset = offset.Value;
        if (size  .HasValue) gc.orthoSize    = size.Value;
        if (back  .HasValue) gc.clearColor   = back.Value;

        gc.Setup(followTarget);
        return gc;
    }

    /* ───── setup ───── */
    private void Setup(Transform followTarget)
    {
        EnsureMainCameraTag();

        cam = TryGetComponent(out Camera c) ? c
                                            : gameObject.AddComponent<Camera>();

        cam.orthographic     = true;
        cam.orthographicSize = orthoSize;
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor  = clearColor;

        if (!FindObjectOfType<AudioListener>())
            gameObject.AddComponent<AudioListener>();

        target = followTarget;
        transform.position = target ? target.position + cameraOffset
                                    : cameraOffset;

      
        SpawnParallaxBackground();
    }

    private void EnsureMainCameraTag()
    {
        var existing = GameObject.FindWithTag("MainCamera");
        if (existing && existing != gameObject) Destroy(existing);
        gameObject.tag = "MainCamera";
    }

    private void SpawnParallaxBackground()
    {
        parallaxBg = FindObjectOfType<ParallaxBackground>();
        if (!parallaxBg)
            parallaxBg = new GameObject("ParallaxBackground")
                             .AddComponent<ParallaxBackground>();

        parallaxBg.Initialise(cam, null);
    }

    /* ───── external API ───── */
    public void SetParallaxData(ParallaxData data) =>
        parallaxBg?.ApplyData(data);

   

    /* ───── follow ───── */
    private void LateUpdate()
    {
        if (!target) return;

        Vector3 goal = target.position + cameraOffset;
        transform.position = Vector3.SmoothDamp(transform.position,
                                                goal,
                                                ref velocity,
                                                1f / lerpSpeed);
    }

    /* ───── accessor ───── */
    public Camera Cam => cam;
}
