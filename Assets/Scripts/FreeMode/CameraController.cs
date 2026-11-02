using UnityEngine;

/// <summary>
/// Free mode camera: mouse wheel zooms towards the cursor, +/- keys zoom,
/// WASD / arrow keys or middle-mouse drag pan. Works at any game speed.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    public float minZoom = 4f;
    public float maxZoom = 18f;

    [Tooltip("Mouse wheel zooms faster when far out, so crossing a huge map isn't tedious.")]
    public float farZoomBoost = 1.6f;

    [Tooltip("Each wheel notch zooms by this factor.")]
    public float zoomStep = 1.15f;

    [Tooltip("Keyboard pan speed in screen-heights per second.")]
    public float panSpeed = 0.9f;

    [Tooltip("The camera centre stays inside this world area.")]
    public Rect bounds = new Rect(-8, -8, 56, 38);

    [Tooltip("Art pixels per tile: close in, zoom steps so each art pixel is a whole number of screen pixels.")]
    public float pixelsPerTile = 32f;

    [Tooltip("While set (looking down into a dungeon), the camera stays inside this area instead of the map.")]
    public Rect? overrideBounds;

    private Camera cam;
    private Vector3 lastDragWorld;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        float k = PixelScale(cam.orthographicSize);
        if (k >= 1f) cam.orthographicSize = OrthoFor(Mathf.Round(k));
    }

    /// <summary>Screen pixels per art pixel at this zoom.</summary>
    private float PixelScale(float ortho) => Screen.height / (2f * ortho * pixelsPerTile);

    private float OrthoFor(float pixelScale) => Screen.height / (2f * pixelScale * pixelsPerTile);

    private void Update()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f && !BuildingPlacer.IsPointerOverUI())
        {
            float step = cam.orthographicSize > 60f ? Mathf.Pow(zoomStep, farZoomBoost) : zoomStep;
            ZoomAt(Input.mousePosition, Mathf.Pow(step, -scroll));
        }
        bool typing = UIBuilder.Typing; // keys go to the text box, not the camera
        if (!typing && (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)))
        {
            ZoomIn();
        }
        if (!typing && (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)))
        {
            ZoomOut();
        }

        Vector3 move = typing ? Vector3.zero : new Vector3(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"), 0);
        if (move.sqrMagnitude > 0)
        {
            transform.position += move.normalized * panSpeed * cam.orthographicSize * 2f * Time.unscaledDeltaTime;
        }

        if (Input.GetMouseButtonDown(2))
        {
            lastDragWorld = MouseWorld(Input.mousePosition);
        }
        else if (Input.GetMouseButton(2))
        {
            Vector3 now = MouseWorld(Input.mousePosition);
            transform.position += lastDragWorld - now;
        }

        Clamp();
    }

    public void ZoomIn() => ZoomAt(new Vector3(Screen.width / 2f, Screen.height / 2f), 1f / zoomStep);

    public void ZoomOut() => ZoomAt(new Vector3(Screen.width / 2f, Screen.height / 2f), zoomStep);

    /// <summary>Change zoom by 'factor' (&lt;1 zooms in) keeping the world point under 'screenPoint' fixed.</summary>
    public void ZoomAt(Vector3 screenPoint, float factor)
    {
        Vector3 before = MouseWorld(screenPoint);
        float zoom = NextZoom(cam.orthographicSize, factor);
        if (zoom < minZoom - 0.01f) zoom = factor < 1f ? cam.orthographicSize : minZoom;   // closest whole-pixel step allowed
        cam.orthographicSize = Mathf.Min(zoom, maxZoom);
        Vector3 after = MouseWorld(screenPoint);
        transform.position += before - after;
        Clamp();
    }

    /// <summary>
    /// The next zoom from here. Close in, it steps between whole pixel sizes (each art pixel 1, 2,
    /// 3... screen pixels) so pixel art stays crisp and even; further out it zooms smoothly.
    /// </summary>
    private float NextZoom(float ortho, float factor)
    {
        float k = PixelScale(ortho);
        if (factor < 1f)
        {
            float target = PixelScale(ortho * factor);
            if (target < 1f) return ortho * factor;
            float next = Mathf.Max(Mathf.Floor(k + 0.001f) + 1f, 1f);
            return OrthoFor(next);
        }
        if (k > 1.001f)
        {
            float next = Mathf.Ceil(k - 0.001f) - 1f;
            return next >= 1f ? OrthoFor(next) : ortho * factor;
        }
        return ortho * factor;
    }

    private void Clamp()
    {
        Vector3 p = transform.position;
        var b = overrideBounds ?? bounds;
        p.x = Mathf.Clamp(p.x, b.xMin, b.xMax);
        p.y = Mathf.Clamp(p.y, b.yMin, b.yMax);
        // At whole-pixel zooms, keep the view on the pixel grid so art doesn't shimmer as it pans.
        float k = PixelScale(cam.orthographicSize);
        if (Mathf.Abs(k - Mathf.Round(k)) < 0.01f && k >= 1f)
        {
            float grid = pixelsPerTile * Mathf.Round(k);
            p.x = Mathf.Round(p.x * grid) / grid;
            p.y = Mathf.Round(p.y * grid) / grid;
        }
        transform.position = p;
    }

    private Vector3 MouseWorld(Vector3 screen)
    {
        screen.z = -transform.position.z;
        Vector3 world = cam.ScreenToWorldPoint(screen);
        world.z = 0;
        return world;
    }
}
