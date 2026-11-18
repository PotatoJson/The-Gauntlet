using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Going through a portal: the screen freezes on its last frame and is twisted round the middle, pulled into it as
/// the portal's glow flares there, then goes black, and only then does whatever comes next (a level load) happen.
/// Holds the controls while it plays. <see cref="Play"/> is the whole interface.
/// </summary>
public class PortalTwist : MonoBehaviour
{
    private const float Seconds = 1.5f;
    private const float MaxTwist = 14f; // radians at the centre, by the end

    /// <summary>Plays the effect, then calls <paramref name="then"/> on a black screen.</summary>
    public static void Play(Action then, Color glow)
    {
        var twist = new GameObject("PortalTwist").AddComponent<PortalTwist>();
        DontDestroyOnLoad(twist.gameObject);
        twist.StartCoroutine(twist.Run(then, glow));
    }

    private IEnumerator Run(Action then, Color glow)
    {
        if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.SuspendWhileRunning(this);
        FMODUnity.RuntimeManager.PlayOneShot("event:/Environment/Teleport");

        // The frame as it is now (UI included), to twist.
        yield return new WaitForEndOfFrame();
        Texture2D still = ScreenCapture.CaptureScreenshotAsTexture();

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000; // over everything, cutscene bars included
        RawImage image = new GameObject("Screen", typeof(RectTransform)).AddComponent<RawImage>();
        image.transform.SetParent(transform, false);
        image.rectTransform.anchorMin = Vector2.zero;
        image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        image.texture = still;
        image.raycastTarget = false;
        Material material = new Material(Shader.Find("Hidden/PortalTwist"));
        material.SetFloat("_Aspect", (float)Screen.width / Mathf.Max(1, Screen.height));
        material.SetColor("_Glow", glow);
        image.material = material;

        for (float t = 0f; t < Seconds; t += Time.unscaledDeltaTime)
        {
            float u = t / Seconds;
            material.SetFloat("_Twist", MaxTwist * u * u);           // slow, then spinning hard
            material.SetFloat("_Pull", Mathf.Lerp(1f, 0.04f, u * u * u)); // sucked in at the end
            material.SetFloat("_GlowAmount", Mathf.Sin(u * Mathf.PI) * 1.4f);
            material.SetFloat("_Fade", Mathf.InverseLerp(0.7f, 1f, u));
            yield return null;
        }
        material.SetFloat("_Fade", 1f);

        if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.RestoreWhenReleased(this);
        then?.Invoke();

        // Black for a couple of frames, until what comes next (the loading screen) is up over it.
        yield return null;
        yield return null;
        Destroy(material);
        Destroy(still);
        Destroy(gameObject);
    }
}
