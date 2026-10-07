using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gives the XP box fill a rolling wave. Put on the Image that ProgressBarCircle uses as its fill bar. At
/// runtime it switches the Image to a shader that draws the fill from Image.fillAmount (which the XP
/// tweens keep updating), so nothing else about how XP works changes. In the editor the Image stays a
/// plain filled box.
/// </summary>
[RequireComponent(typeof(Image))]
public class XPWaveFill : MonoBehaviour
{
    [SerializeField] private Shader shader;
    [Tooltip("How many art pixels the box is across; the wave snaps to this grid.")]
    [SerializeField] private float pixelCount = 30f;
    [SerializeField] private float waveHeight = 0.04f;
    [Tooltip("How strongly the wave sloshes when XP is gained, and how fast that calms down.")]
    [SerializeField] private float splashPerFill = 10f;
    [SerializeField] private float splashDecay = 1.5f;

    private Image _image;
    private Material _mat;
    private float _lastFill;
    private float _splash;

    private void Awake()
    {
        _image = GetComponent<Image>();
        if (shader == null) return;

        _mat = new Material(shader);
        _image.type = Image.Type.Simple; // the shader does the filling now
        _image.material = _mat;
        _lastFill = _image.fillAmount;
    }

    private void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);
    }

    private void LateUpdate()
    {
        if (_mat == null) return;

        float fill = _image.fillAmount;
        float dt = Time.unscaledDeltaTime;

        if (fill > _lastFill) _splash = Mathf.Min(1f, _splash + (fill - _lastFill) * splashPerFill);
        _splash = Mathf.Max(0f, _splash - splashDecay * dt);
        _lastFill = fill;

        Vector4 uvRect = _image.sprite != null ? UnityEngine.Sprites.DataUtility.GetOuterUV(_image.sprite) : new Vector4(0, 0, 1, 1);
        _mat.SetFloat("_Fill", fill);
        _mat.SetFloat("_Splash", _splash);
        _mat.SetFloat("_PixelCount", pixelCount);
        _mat.SetFloat("_WaveAmp", waveHeight);
        _mat.SetFloat("_UnscaledTime", Time.unscaledTime);
        _mat.SetVector("_UVRect", uvRect);
    }
}
