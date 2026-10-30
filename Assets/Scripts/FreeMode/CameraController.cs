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

    [Tooltip("Each wheel notch zooms by this factor.")]
    public float zoomStep = 1.15f;

    [Tooltip("Keyboard pan speed in screen-heights per second.")]
    public float panSpeed = 0.9f;

    [Tooltip("The camera centre stays inside this world area.")]
    public Rect bounds = new Rect(-8, -8, 56, 38);

    private Camera cam;
    private Vector3 lastDragWorld;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Update()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f && !BuildingPlacer.IsPointerOverUI())
        {
            ZoomAt(Input.mousePosition, Mathf.Pow(zoomStep, -scroll));
        }
        if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
        {
            ZoomIn();
        }
        if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
        {
            ZoomOut();
        }

        Vector3 move = new Vector3(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"), 0);
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
        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * factor, minZoom, maxZoom);
        Vector3 after = MouseWorld(screenPoint);
        transform.position += before - after;
        Clamp();
    }

    private void Clamp()
    {
        Vector3 p = transform.position;
        p.x = Mathf.Clamp(p.x, bounds.xMin, bounds.xMax);
        p.y = Mathf.Clamp(p.y, bounds.yMin, bounds.yMax);
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
