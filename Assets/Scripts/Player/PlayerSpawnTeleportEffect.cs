using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerSpawnTeleportEffect : MonoBehaviour
{
    [Header("Feature Toggle")]
    [Tooltip("If unchecked, the player spawns normally without any teleport effect or delay.")]
    public bool useTeleportEffect = true;

    [Header("Settings")]
    public GameObject teleportEffectPrefab;
    public float effectDuration = 1.7f;
    public Vector3 spawnOffset = Vector3.zero;

    [Header("Reveal Settings")]
    public bool useRevealEffect = true;
    public Shader revealShader;

    private PlayerMovement _movement;
    private PlayerManager _manager;
    private PlayerCombat _combat;

    private struct RendererData
    {
        public Renderer renderer;
        public Material[] originalMaterials;
        public Material[] revealMaterials;
    }

    private List<RendererData> _rendererDataList = new List<RendererData>();

    private void Start()
    {
        if (!useTeleportEffect) return;

        _movement = GetComponent<PlayerMovement>();
        _manager = GetComponent<PlayerManager>();
        _combat = GetComponent<PlayerCombat>();

        if (revealShader == null)
        {
            revealShader = Shader.Find("Custom/TopDownReveal");
        }

        if (teleportEffectPrefab != null)
        {
            StartCoroutine(PlaySpawnEffect());
        }
    }

    private IEnumerator PlaySpawnEffect()
    {
        // 1. Disable movement and combat
        if (_movement != null) _movement.enabled = false;
        if (_combat != null) _combat.enabled = false;

        // 2. Setup Reveal
        float playerHeight = 2.0f;
        float groundY = transform.position.y;
        
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) playerHeight = cc.height;

        if (useRevealEffect && revealShader != null)
        {
            PrepareReveal();
            SetRevealHeight(groundY + playerHeight + 1f); // Initially hide everything (reveal line above head)
        }
        else
        {
            // Simple invisibility if no shader
            SetRenderersEnabled(false);
        }

        // 3. Camera Sync
        if (PlayerCamera.Instance != null)
        {
            if (PlayerCamera.Instance.playerTarget == null)
                PlayerCamera.Instance.playerTarget = transform;

            PlayerCamera.Instance.SnapToTarget();
            for (int i = 0; i < 20; i++)
            {
                PlayerCamera.Instance.HandleAllCameraActions(Vector2.zero, false);
            }
        }

        // 4. VFX
        if (teleportEffectPrefab != null)
        {
            GameObject effect = Instantiate(teleportEffectPrefab, transform.position + spawnOffset, Quaternion.identity);
            if (effect.TryGetComponent<ReverseVFX>(out var reverse))
            {
                reverse.duration = effectDuration;
            }
        }

        // 5. Animate Reveal
        float elapsed = 0;
        float startRevealY = groundY + playerHeight + 0.1f;
        float endRevealY = groundY - 0.1f;

        while (elapsed < effectDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / effectDuration;
            
            if (useRevealEffect && revealShader != null)
            {
                // Animate from top to bottom
                // _RevealHeight is the threshold: if y < height, discard.
                // So start at Top (shows nothing below top), end at Bottom (shows everything above bottom).
                float currentRevealY = Mathf.Lerp(startRevealY, endRevealY, t);
                SetRevealHeight(currentRevealY);
            }

            yield return null;
        }

        // 6. Cleanup
        if (useRevealEffect && revealShader != null)
        {
            RestoreOriginalMaterials();
        }
        else
        {
            SetRenderersEnabled(true);
        }

        if (_movement != null) _movement.enabled = true;
        if (_combat != null) _combat.enabled = true;
    }

    private void PrepareReveal()
    {
        _rendererDataList.Clear();
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r is ParticleSystemRenderer) continue;

            RendererData data = new RendererData();
            data.renderer = r;
            data.originalMaterials = r.sharedMaterials;
            data.revealMaterials = new Material[r.sharedMaterials.Length];

            for (int i = 0; i < r.sharedMaterials.Length; i++)
            {
                Material m = new Material(revealShader);
                if (r.sharedMaterials[i] != null)
                {
                    m.SetTexture("_BaseMap", r.sharedMaterials[i].GetTexture("_BaseMap") ?? r.sharedMaterials[i].GetTexture("_MainTex"));
                    m.SetColor("_BaseColor", r.sharedMaterials[i].HasProperty("_BaseColor") ? r.sharedMaterials[i].GetColor("_BaseColor") : Color.white);
                }
                data.revealMaterials[i] = m;
            }

            r.materials = data.revealMaterials;
            _rendererDataList.Add(data);
        }
    }

    private void SetRevealHeight(float height)
    {
        foreach (var data in _rendererDataList)
        {
            foreach (var m in data.revealMaterials)
            {
                m.SetFloat("_RevealHeight", height);
            }
        }
    }

    private void OnDisable()
    {
        if (_rendererDataList.Count > 0)
        {
            RestoreOriginalMaterials();
        }
    }

    private void RestoreOriginalMaterials()
    {
        foreach (var data in _rendererDataList)
        {
            if (data.renderer != null)
            {
                data.renderer.materials = data.originalMaterials;
            }
            // Clean up temporary materials
            foreach (var m in data.revealMaterials)
            {
                if (m != null) Destroy(m);
            }
        }
        _rendererDataList.Clear();
    }

    private void SetRenderersEnabled(bool enabled)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r is ParticleSystemRenderer) continue;
            r.enabled = enabled;
        }
    }
}
