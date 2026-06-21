using UnityEngine;

/// <summary>
/// Locks the camera to an exact 9:16 (900x1600) view on any device.
/// On taller phones (9:18, 9:20, 9:21) black bars appear top and bottom.
/// On wider screens (tablets) black bars appear left and right.
/// The game content always looks pixel-identical to the 900x1600 editor setup.
///
/// SETUP:
///   1. Attach to Main Camera.
///   2. Set _referenceOrthoSize to the exact Camera.orthographicSize
///      value from the Inspector (the one you used while building at 900x1600).
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFit : MonoBehaviour
{
    [Tooltip("Camera.orthographicSize set while designing the scene at 900x1600.")]
    [SerializeField] private float _referenceOrthoSize = 5f;

    private const float RefWidth  = 900f;
    private const float RefHeight = 1600f;

    private Camera _cam;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        SpawnBackgroundCamera();
    }

    private void Start()
    {
        ApplyFit();
    }

    private void ApplyFit()
    {
        _cam.orthographicSize = _referenceOrthoSize;

        float targetAspect = RefWidth / RefHeight;

        // Always treat the shorter edge as width so a temporarily-swapped
        // Screen.width/height (seen on some devices before orientation settles)
        // never incorrectly triggers the pillarbox branch.
        float sw           = Mathf.Min(Screen.width, Screen.height);
        float sh           = Mathf.Max(Screen.width, Screen.height);
        float screenAspect = sw / sh;
        float ratio        = screenAspect / targetAspect;

        if (ratio < 1f)
        {
            // Screen is narrower than 9:16 (taller phone — most modern devices).
            // Shrink viewport height so the content keeps its 9:16 shape.
            // Black bars appear top and bottom.
            _cam.rect = new Rect(0f, (1f - ratio) * 0.5f, 1f, ratio);
        }
        else if (ratio > 1f)
        {
            // Screen is wider than 9:16 (tablet or landscape).
            // Shrink viewport width. Black bars appear left and right.
            float w = 1f / ratio;
            _cam.rect = new Rect((1f - w) * 0.5f, 0f, w, 1f);
        }
        else
        {
            _cam.rect = new Rect(0f, 0f, 1f, 1f);
        }
    }

    /// <summary>
    /// Fills the regions outside the viewport with solid black.
    /// Without this the bar areas show garbage from the previous frame.
    /// </summary>
    private void SpawnBackgroundCamera()
    {
        var go  = new GameObject("CameraFit_Background");
        go.transform.SetParent(transform, false);

        var bg = go.AddComponent<Camera>();
        bg.clearFlags      = CameraClearFlags.SolidColor;
        bg.backgroundColor = Color.black;
        bg.cullingMask     = 0;                  // render no geometry
        bg.depth           = _cam.depth - 1;     // draws first, main camera draws on top
        bg.rect            = new Rect(0, 0, 1, 1);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_cam == null) _cam = GetComponent<Camera>();
        if (_cam != null) ApplyFit();
    }
#endif
}
