using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Scrollbar))]
public class ScrollbarSizeLimiter : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float minSize = 0.1f;
    [SerializeField, Range(0f, 1f)] private float maxSize = 1.0f;

    private Scrollbar _scrollbar;

    private void Awake()
    {
        _scrollbar = GetComponent<Scrollbar>();
    }

    private void LateUpdate()
    {
        if (_scrollbar != null)
        {
            float clampedSize = Mathf.Clamp(_scrollbar.size, minSize, maxSize);
            if (!Mathf.Approximately(_scrollbar.size, clampedSize))
            {
                _scrollbar.size = clampedSize;
            }
        }
    }
}