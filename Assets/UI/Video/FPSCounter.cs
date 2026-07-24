using UnityEngine;
using TMPro;

public class FPSCounter : MonoBehaviour
{
    [SerializeField] private TMP_Text fpsText;
    [SerializeField] private float updateInterval = 0.5f;

    private float _accum = 0;
    private int _frames = 0;
    private float _timeleft;

    void Start()
    {
        if (fpsText == null)
        {
            fpsText = GetComponent<TMP_Text>();
        }
        _timeleft = updateInterval;
    }

    void Update()
    {
        _timeleft -= Time.unscaledDeltaTime;
        _accum += 1.0f / Time.unscaledDeltaTime;
        ++_frames;

        if (_timeleft <= 0.0)
        {
            float fps = _accum / _frames;
            fpsText.text = string.Format("{0:F0} FPS", fps);

            _timeleft = updateInterval;
            _accum = 0.0f;
            _frames = 0;
        }
    }
}
