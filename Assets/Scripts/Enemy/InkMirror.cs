using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;

/// <summary>
/// The standing mirror at the bottom of Hell, where the final boss comes from (<see cref="InkShadowIntro"/>).
///
/// It reflects the knight and nothing else. A mirrored copy of the knight's model stands behind the glass, posed
/// like the player every frame, and a second camera that sees only that copy draws it onto the glass from the main
/// camera's point of view. The glass is then a window onto a dark room with the knight's reflection in it, and the
/// reflection is a real object the cutscene can take over: it stops copying the player, turns to ink, walks up to
/// the glass, and the glass cracks and bursts.
/// </summary>
[DefaultExecutionOrder(1000)] // after the animators, the player's camera and the cutscene have placed everything
public class InkMirror : MonoBehaviour
{
    private const int ReflectionLayer = 31; // unnamed, so nothing else in the game draws on it
    private const int Bit = 1 << ReflectionLayer;
    private const float ShowWithin = 70f;
    private const int Shards = 34;
    private static readonly Color Ink = new Color(0.03f, 0.03f, 0.05f, 1f);
    private static readonly Color Glass = new Color(0.08f, 0.08f, 0.12f, 1f); // light enough that the ink shows against it

    [SerializeField] private Renderer glass;
    [SerializeField, Tooltip("The knight's model, copied for the reflection")]
    private GameObject knightModel;
    [SerializeField] private Material eyeMaterial;
    [SerializeField, Tooltip("Flat, unlit ink: what the reflection ends up as")]
    private Material inkMaterial;
    [SerializeField] private Material shardMaterial;
    [SerializeField, Tooltip("Where on the glass the cracks start, 0-1 across and up")]
    private Vector2 crackCentre = new Vector2(0.5f, 0.55f);
    [SerializeField, Tooltip("The glass's lowest and highest point above the floor, and its width")]
    private Vector3 glassBottomTopWidth = new Vector3(0.35f, 3.35f, 2f);

    private Transform _source;
    private Transform _reflection;
    private Transform _reflectionHead;
    private readonly List<(Transform from, Transform to)> _bones = new List<(Transform, Transform)>();
    private readonly List<Material> _reflectionMaterials = new List<Material>();
    private bool _frozen, _shattered;
    private float _knightHeight = 2f;

    private Camera _main, _camera;
    private RenderTexture _texture;
    private Material _glassMaterial;
    private Texture2D _cracks;

    /// <summary>Where the middle of the glass is, in the world.</summary>
    public Vector3 GlassCentre => transform.position + Vector3.up * (glassBottomTopWidth.x + glassBottomTopWidth.y) * 0.5f;

    /// <summary>The reflection's head, or the middle of the glass if it has none yet.</summary>
    public Vector3 ReflectionFace => _reflectionHead != null ? _reflectionHead.position : GlassCentre;

    [Header("Looking into it")]
    [Tooltip("The world-space prompt prefab (UI/InteractionPrompt), the one the shrines use.")]
    [SerializeField] private GameObject promptPrefab;
    [SerializeField] private float interactionRadius = 4.5f;
    [SerializeField] private float promptHeight = 5.6f;
    [SerializeField] private LocalizedString promptString = new LocalizedString("UIStrings", "Mirror_Prompt");

    /// <summary>Raised once, when the player chooses to look into it (the Interact key, standing near it).</summary>
    public event System.Action LookedInto;

    private InputAction _interact;
    private Transform _player;
    private GameObject _prompt;
    private TMP_Text _promptText;
    private bool _looked;

    // Until the player looks into it, it's only a mirror: they can walk past it to the shrines and chests first.
    private void Update()
    {
        if (_looked) return;

        if (_player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found == null) return;
            _player = found.transform;
        }

        bool near = Time.timeScale > 0f && Vector3.Distance(transform.position, _player.position) <= interactionRadius;
        if (_prompt != null)
        {
            if (_prompt.activeSelf != near) _prompt.SetActive(near);
            if (near)
            {
                _prompt.transform.position = transform.position + Vector3.up * promptHeight;
                promptString.Arguments = new object[] { ShrineInteractable.InteractKey(_interact) };
                if (_promptText != null) _promptText.text = promptString.GetLocalizedString();
            }
        }

        if (near && _interact != null && _interact.WasPressedThisFrame())
        {
            _looked = true;
            if (_prompt != null) _prompt.SetActive(false);
            LookedInto?.Invoke();
        }
    }

    private void Start()
    {
        _interact = InputSystem.actions != null ? InputSystem.actions.FindAction("Interact") : null;
        if (promptPrefab != null)
        {
            _prompt = Instantiate(promptPrefab);
            _promptText = _prompt.GetComponentInChildren<TMP_Text>();
            _prompt.SetActive(false);
        }

        _glassMaterial = glass.material;
        _cracks = GlassFx.DrawCracks(crackCentre);
        _glassMaterial.SetTexture("_CrackTex", _cracks);
        _glassMaterial.SetVector("_CrackCentre", crackCentre);
        _glassMaterial.SetVector("_GlassSize", new Vector4(glassBottomTopWidth.z, glassBottomTopWidth.y - glassBottomTopWidth.x, 0f, 0f));
        _glassMaterial.SetFloat("_Crack", 0f);

        _camera = new GameObject("MirrorCamera").AddComponent<Camera>();
        _camera.transform.SetParent(transform, false);
        _camera.cullingMask = Bit;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = Glass;
        _camera.enabled = false;
    }

    private void OnDestroy()
    {
        if (_prompt != null) Destroy(_prompt);
        if (_main != null) _main.cullingMask |= Bit;
        if (_texture != null) _texture.Release();
        Destroy(_texture);
        Destroy(_cracks);
        Destroy(_glassMaterial);
        foreach (Material material in _reflectionMaterials) Destroy(material);
    }

    private void LateUpdate()
    {
        if (_shattered) return;
        if (_source == null && !CreateReflection()) return;

        if (!_frozen) CopyPlayer();
        UpdateCamera();
    }

    #region Reflection
    private bool CreateReflection()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Animator animator = player != null ? player.GetComponentInChildren<Animator>() : null;
        if (animator == null) return false;
        _source = animator.transform;

        // Mirrored by its parent: a negative scale through the glass.
        Transform root = new GameObject("Reflection").transform;
        root.SetParent(transform, false);
        root.localScale = new Vector3(1f, 1f, -1f);

        GameObject copy = Instantiate(knightModel, root);
        copy.name = "Knight (reflection)";
        foreach (Animator own in copy.GetComponentsInChildren<Animator>()) Destroy(own); // posed by hand below
        foreach (Transform part in copy.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = ReflectionLayer;
        foreach (Renderer part in copy.GetComponentsInChildren<Renderer>(true))
        {
            part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // its shadow would land behind the mirror
            _reflectionMaterials.AddRange(part.materials);
        }
        _reflection = copy.transform;

        // Bones are matched by name path, the same in both copies of the model.
        Dictionary<string, Transform> copied = new Dictionary<string, Transform>();
        foreach (Transform part in _reflection.GetComponentsInChildren<Transform>(true)) copied[PathOf(part, _reflection)] = part;
        foreach (Transform part in _source.GetComponentsInChildren<Transform>(true))
        {
            if (part != _source && copied.TryGetValue(PathOf(part, _source), out Transform match)) _bones.Add((part, match));
        }

        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (head != null) copied.TryGetValue(PathOf(head, _source), out _reflectionHead);
        Renderer body = _source.GetComponentInChildren<SkinnedMeshRenderer>();
        if (body != null) _knightHeight = body.bounds.size.y;

        CopyPlayer();
        return true;
    }

    private static string PathOf(Transform part, Transform root)
    {
        string path = part.name;
        for (Transform up = part.parent; up != null && up != root; up = up.parent) path = up.name + "/" + path;
        return path;
    }

    // In the mirror's own axes, unmirrored: the parent's negative scale puts it on the far side of the glass.
    private void CopyPlayer()
    {
        Quaternion inverse = Quaternion.Inverse(transform.rotation);
        _reflection.localPosition = inverse * (_source.position - transform.position);
        _reflection.localRotation = inverse * _source.rotation;
        _reflection.localScale = _source.lossyScale;

        foreach ((Transform from, Transform to) in _bones)
        {
            to.localPosition = from.localPosition;
            to.localRotation = from.localRotation;
            to.localScale = from.localScale;
        }
    }

    // The same view as the main camera, seeing only the reflection, drawn first so the glass can show it this frame.
    private void UpdateCamera()
    {
        if (_main == null || !_main.isActiveAndEnabled)
        {
            _main = Camera.main;
            if (_main == null) return;
            _main.cullingMask &= ~Bit;
        }

        bool show = glass.enabled && glass.isVisible &&
                    (_main.transform.position - transform.position).sqrMagnitude < ShowWithin * ShowWithin;
        _camera.enabled = show;
        if (!show) return;

        int width = _main.targetTexture != null ? _main.targetTexture.width : Screen.width / 2;
        int height = _main.targetTexture != null ? _main.targetTexture.height : Screen.height / 2;
        if (_texture == null || _texture.width != width || _texture.height != height)
        {
            if (_texture != null) _texture.Release();
            Destroy(_texture);
            _texture = new RenderTexture(width, height, 24) { name = "MirrorReflection", filterMode = FilterMode.Point };
            _camera.targetTexture = _texture;
            _glassMaterial.SetTexture("_ReflectionTex", _texture);
        }

        _camera.transform.SetPositionAndRotation(_main.transform.position, _main.transform.rotation);
        _camera.nearClipPlane = _main.nearClipPlane;
        _camera.farClipPlane = _main.farClipPlane;
        _camera.fieldOfView = _main.fieldOfView;
        _camera.aspect = _main.aspect;
        _camera.projectionMatrix = _main.projectionMatrix;
        _camera.depth = _main.depth - 1f;
    }
    #endregion

    #region What the cutscene does with it
    /// <summary>The reflection stops copying the player and keeps the pose it has.</summary>
    public void Freeze() => _frozen = true;

    /// <summary>The reflection's colours drain to ink, and white eyes open in its helmet.</summary>
    public IEnumerator TurnToInk(float seconds)
    {
        // The knight's material glows a little on its own (emission), which has to drain away with the colour.
        Color[] from = new Color[_reflectionMaterials.Count];
        Color[] glow = new Color[_reflectionMaterials.Count];
        for (int i = 0; i < from.Length; i++)
        {
            from[i] = _reflectionMaterials[i].GetColor("_BaseColor");
            glow[i] = _reflectionMaterials[i].HasProperty("_EmissionColor") ? _reflectionMaterials[i].GetColor("_EmissionColor") : Color.black;
        }

        bool eyes = false;
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / seconds);
            for (int i = 0; i < from.Length; i++)
            {
                _reflectionMaterials[i].SetColor("_BaseColor", Color.Lerp(from[i], Ink, t));
                if (_reflectionMaterials[i].HasProperty("_EmissionColor"))
                    _reflectionMaterials[i].SetColor("_EmissionColor", Color.Lerp(glow[i], Color.black, t));
            }

            if (!eyes && t > 0.6f)
            {
                eyes = true;
                OpenEyes();
            }
            yield return null;
        }

        // Lit, even black still catches the sky at its edges; flat ink from here on.
        foreach (Renderer part in _reflection.GetComponentsInChildren<Renderer>(true))
        {
            if (part.name == "Eye") continue;
            Material[] flat = new Material[part.sharedMaterials.Length];
            for (int i = 0; i < flat.Length; i++) flat[i] = inkMaterial;
            part.sharedMaterials = flat;
        }
        if (!eyes) OpenEyes();
    }

    private void OpenEyes()
    {
        if (_reflectionHead == null) return;

        // The reflection looks back out through the glass, along the mirror's forward.
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Cube);
            eye.name = "Eye";
            Destroy(eye.GetComponent<Collider>());
            eye.layer = ReflectionLayer;
            eye.GetComponent<Renderer>().sharedMaterial = eyeMaterial;
            // Measured for a knight 2 m tall, and scaled to the real one.
            float k = _knightHeight / 2f;
            eye.transform.position = _reflectionHead.position + transform.forward * (0.2f * k) + Vector3.up * (0.14f * k) + transform.right * (0.08f * side * k);
            eye.transform.rotation = transform.rotation;
            eye.transform.localScale = new Vector3(0.12f, 0.035f, 0.04f) * k;
            eye.transform.SetParent(_reflectionHead, true);
        }
    }

    /// <summary>The reflection walks up to the glass, from its side, until it's this close to it.</summary>
    public IEnumerator StepToGlass(float seconds, float closeness = 0.45f)
    {
        Vector3 start = _reflection.localPosition;
        Vector3 end = new Vector3(start.x, start.y, closeness);
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / seconds);
            _reflection.localPosition = Vector3.Lerp(start, end, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI * 3f) * 0.03f);
            yield return null;
        }
        _reflection.localPosition = end;
    }

    /// <summary>Cracks spread over the glass from where the reflection touched it.</summary>
    public IEnumerator Crack(float seconds)
    {
        FMODUnity.RuntimeManager.PlayOneShot("event:/Combat/Impact_Light", GlassCentre);
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            // In jolts, the way glass gives.
            _glassMaterial.SetFloat("_Crack", Mathf.Floor(elapsed / seconds * 5f) / 5f + 0.08f);
            yield return null;
        }
        _glassMaterial.SetFloat("_Crack", 1f);
    }

    /// <summary>The glass bursts outwards. The reflection goes with it, and the frame is left empty.</summary>
    public void Shatter()
    {
        if (_shattered) return;
        _shattered = true;

        glass.enabled = false;
        _camera.enabled = false;
        if (_reflection != null) Destroy(_reflection.parent.gameObject);

        FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Glass_Shatter", GlassCentre);
        EnemyFx.Impact(transform.position + transform.forward * 0.6f, 2.6f, new Color(0.75f, 0.8f, 1f, 1f));

        GlassFx.Shatter(transform, glassBottomTopWidth.x, glassBottomTopWidth.y, glassBottomTopWidth.z, shardMaterial, Shards);
    }


    // White lines on clear: rays that wander out from the crack centre, some splitting as they go.
    #endregion
}
