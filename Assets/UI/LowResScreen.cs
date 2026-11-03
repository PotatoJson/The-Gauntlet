using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The game is drawn into a small render texture and shown through this RawImage. The texture is
/// authored 16:9, which left black bars on 4:3, 16:10 or ultrawide screens that full-screen effects
/// spilled into. This keeps the texture's height (so the pixel size stays the same) and widens or
/// narrows it to the screen's shape, then stretches the image over the whole screen.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class LowResScreen : MonoBehaviour
{
    // Shared by every level's canvas: the camera that draws into it survives scene loads.
    private static RenderTexture _authored;
    private static RenderTexture _fitted;

    private RawImage _image;

    private void Awake()
    {
        _image = GetComponent<RawImage>();
        if (_authored == null) _authored = _image.texture as RenderTexture;

        RectTransform rect = _image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RetargetCameras(_authored);

    private void LateUpdate()
    {
        if (_authored == null) return;

        int height = _authored.height;
        int width = Mathf.Max(1, Mathf.RoundToInt(height * (float)Screen.width / Screen.height));

        if (_fitted == null || _fitted.width != width || _fitted.height != height)
        {
            RenderTexture old = _fitted;

            RenderTextureDescriptor desc = _authored.descriptor;
            desc.width = width;
            desc.height = height;
            _fitted = new RenderTexture(desc)
            {
                name = _authored.name + " (fitted)",
                filterMode = _authored.filterMode,
                wrapMode = _authored.wrapMode
            };
            _fitted.Create();

            RetargetCameras(_authored);
            if (old != null)
            {
                RetargetCameras(old);
                old.Release();
                Destroy(old);
            }
        }

        if (_image.texture != _fitted) _image.texture = _fitted;
    }

    private static void RetargetCameras(RenderTexture from)
    {
        if (_fitted == null || from == null) return;

        foreach (Camera cam in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cam.targetTexture != from) continue;
            cam.targetTexture = _fitted;
            cam.ResetAspect();
        }
    }
}
