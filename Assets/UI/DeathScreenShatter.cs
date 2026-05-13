using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using DG.Tweening;
using UnityEngine.SceneManagement;

public class DeathScreenShatter : MonoBehaviour
{
    [Header("UI References")]
    public CanvasGroup shatterCanvasGroup;
    public RawImage leftHalf;
    public RawImage rightHalf;

    [Header("New Elements")]
    public GameObject mainHUD;
    public CanvasGroup gameOverPanel;

    [Header("Player & Shader References")]
    public PlayerMovement playerMovement;
    [Tooltip("Drag your ScreenCrack_Mat here so the script can talk to it directly!")]
    public Material crackMaterial;

    [Header("Animation Settings")]
    public float crackSpreadDuration = 0.4f;
    public float maxDistortion = 0.15f;
    public float freezeTimeBeforeFall = 0.5f;
    public float fallDuration = 1.5f;
    public float fallDistance = 1500f;

    public static DeathScreenShatter Instance { get; private set; }

    private void Awake()
    {
        // Safety check: Ensure only one instance ever exists!
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    private void OnDisable()
    {
        if (crackMaterial != null)
        {
            crackMaterial.SetFloat("_Crack_Distortion", 0f);
        }
    }

    public void TriggerDeathShatter()
    {
        Debug.Log("DEATH TRIGGERED: Starting Shatter Coroutine!");
        StartCoroutine(CaptureAndShatter());
    }

    private IEnumerator CaptureAndShatter()
    {
        if (playerMovement != null) playerMovement.enabled = false;

        if (mainHUD != null) mainHUD.SetActive(false);
        if (gameOverPanel != null)
        {
            gameOverPanel.alpha = 0f;
            gameOverPanel.blocksRaycasts = false;
            gameOverPanel.gameObject.SetActive(false);
        }

        Debug.Log("Starting DOTween Crack Spread...");

        // Animate the Material directly
        DOVirtual.Float(0f, maxDistortion, crackSpreadDuration, (currentDistortion) =>
        {
            // DEBUG: This will spam your console to prove the numbers are climbing!
            Debug.Log($"Tweening Distortion Value: {currentDistortion}");

            if (crackMaterial != null)
            {
                crackMaterial.SetFloat("_Crack_Distortion", currentDistortion);
            }
            else
            {
                Debug.LogError("CRITICAL: You forgot to assign the Material in the Inspector!");
            }
        }).SetUpdate(true);

        yield return new WaitForSecondsRealtime(crackSpreadDuration);

        Debug.Log("Crack spread finished. Taking screenshot...");
        yield return new WaitForEndOfFrame();

        Texture2D screenShot = ScreenCapture.CaptureScreenshotAsTexture();
        leftHalf.texture = screenShot;
        rightHalf.texture = screenShot;

        // Turn OFF the live shader distortion
        if (crackMaterial != null) crackMaterial.SetFloat("_Crack_Distortion", 0f);

        shatterCanvasGroup.alpha = 1f;
        Time.timeScale = 0f;

        yield return new WaitForSecondsRealtime(freezeTimeBeforeFall);

        Debug.Log("Dropping screen halves...");
        leftHalf.rectTransform.DOAnchorPosY(-fallDistance, fallDuration).SetEase(Ease.InBack).SetUpdate(true);
        leftHalf.rectTransform.DORotate(new Vector3(0, 0, 45f), fallDuration).SetUpdate(true);

        rightHalf.rectTransform.DOAnchorPosY(-fallDistance, fallDuration).SetEase(Ease.InBack).SetUpdate(true);
        rightHalf.rectTransform.DORotate(new Vector3(0, 0, -45f), fallDuration).SetUpdate(true);

        yield return new WaitForSecondsRealtime(fallDuration - 0.5f);

        if (gameOverPanel != null)
        {
            gameOverPanel.gameObject.SetActive(true);
            gameOverPanel.blocksRaycasts = true;

            gameOverPanel.DOFade(1f, 1f).SetUpdate(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}