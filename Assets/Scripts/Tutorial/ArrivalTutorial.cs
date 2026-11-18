using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;

/// <summary>
/// The first minutes of the game, between the prologue and the Dungeon: the knight falls into Hell and learns to play
/// by playing. In order:
///  1. Cutscene: he falls into the pit, hits the ground, gets up dazed, shakes his head, looks around, sees a chest.
///  2. Walk to the chest (move / sprint / roll hints, a ring on the floor to aim for), open it: one plain gauntlet.
///     The character screen opens with it on his fist; close it and the barrier on the only exit breaks.
///  3. Through to the next chamber: a grunt rises (light / heavy punch hints).
///  4. Cutscene, no skipping: an eyeball shoots him in the back. Time stops until he drinks a potion.
///  5. Kill the eyeball; a portal opens at the far end and takes him to the Dungeon.
///
/// Played on the first New Game only (<see cref="Done"/>); the main menu's Tutorial button replays it, and then the
/// portal goes back to the menu instead.
/// </summary>
public class ArrivalTutorial : MonoBehaviour
{
    public const string SceneName = "Arrival";
    private const string DoneKey = "TutorialDone";

    /// <summary>Whether this player has finished the tutorial before (so New Game can skip it).</summary>
    public static bool Done => PlayerPrefs.GetInt(DoneKey, 0) == 1;

    /// <summary>Set by the main menu's Tutorial button: a replay, which ends back at the menu.</summary>
    public static bool ReplayFromMenu;

    // States added to the player's animator for this cutscene (UAL clips, retargeted).
    private const string FallState = "Tut_Fall", ImpactState = "Tut_Impact", GetUpState = "Tut_GetUp";
    private const string DazedState = "Tut_Dazed", ShakeHeadState = "Tut_ShakeHead", LookAroundState = "Tut_LookAround";

    private static readonly Color Gold = new Color(1f, 0.78f, 0.35f, 1f);

    [Header("Places")]
    [SerializeField, Tooltip("Where he lands, facing the way he'll look first.")] private Transform landing;
    [SerializeField] private GauntletInteractable chest;
    [SerializeField, Tooltip("Blocks the only exit until the gauntlet is on.")] private GameObject exitBarrier;
    [SerializeField, Tooltip("The fight starts once he's this close to here.")] private Transform chamberCentre;
    [SerializeField] private float chamberRadius = 6f;
    [SerializeField] private Transform gruntSpawn;
    [SerializeField] private Transform portalPoint;

    [Header("Prefabs")]
    [SerializeField] private GameObject gruntPrefab;
    [SerializeField] private GameObject eyePrefab;
    [SerializeField] private GameObject portalPrefab;

    [Header("Tuning")]
    [SerializeField] private float fallHeight = 30f;
    [SerializeField, Tooltip("The ambush shot. It should take most of his health, and leave him alive.")]
    private float ambushDamage = 60f;
    [SerializeField, Tooltip("The tutorial's enemies hit this hard (1 = as usual), so a first-timer isn't killed here.")]
    private float enemyDamage = 0.5f;

    private Transform _player;
    private CharacterController _controller;
    private PlayerMovement _movement;
    private PlayerManager _state;
    private PlayerHealth _health;
    private Animator _animator;

    private void Awake()
    {
        // He arrives with nothing: the gauntlet comes from the chest.
        InventoryManager.StartEmptyHanded = true;
        if (PersistentEquipment.Instance != null) PersistentEquipment.Instance.ResetForNewRun();
    }

    private void OnDestroy()
    {
        InventoryManager.StartEmptyHanded = false;
        Time.timeScale = 1f;
    }

    private IEnumerator Start()
    {
        yield return null; // everything else's Start first (the HUD, the inventory)
        if (InventoryManager.Instance != null) InventoryManager.Instance.RemoveAllGauntlets(); // whatever the HUD put on him

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        _player = player.transform;
        _controller = player.GetComponent<CharacterController>();
        _movement = player.GetComponent<PlayerMovement>();
        _state = player.GetComponent<PlayerManager>();
        _health = player.GetComponent<PlayerHealth>();
        _animator = player.GetComponentInChildren<Animator>();

        yield return Arrive();
        yield return ToTheChest();
        yield return Equip();
        yield return WalkTo(chamberCentre, chamberRadius);
        GameObject grunt = Spawn(gruntPrefab, gruntSpawn.position, Quaternion.LookRotation(_player.position - gruntSpawn.position));
        yield return Fight(grunt);
        yield return new WaitForSeconds(1.2f);
        GameObject eye = null;
        yield return Ambush(e => eye = e);
        yield return WaitDead(eye);
        yield return LevelUp();
        yield return ThePortal();
    }

    #region 1. The fall
    private IEnumerator Arrive()
    {
        CutsceneCamera cut = CutsceneCamera.Begin();
        _controller.enabled = false;

        // The player's pivot sits above his feet (the controller's middle), so "on the floor" is this much higher.
        float feet = _controller.height * 0.5f - _controller.center.y;
        Vector3 floor = landing.position;
        Vector3 ground = floor + Vector3.up * feet; // where his pivot is when he stands there
        System.Func<float, Vector3> body = h => new Vector3(_player.position.x, floor.y + h, _player.position.z); // h above his feet
        Vector3 forward = landing.forward;
        Vector3 right = landing.right;
        _player.rotation = landing.rotation;
        _player.position = ground + Vector3.up * fallHeight;
        Play(FallState, 0f);

        // Low on the floor, looking up the shaft as he comes down it.
        cut.Position = floor + forward * 6f + right * 2.5f + Vector3.up * 0.8f;
        const float fallSeconds = 1.4f;
        for (float t = 0f; t < fallSeconds; t += Time.deltaTime)
        {
            float u = t / fallSeconds;
            _player.position = ground + Vector3.up * (fallHeight * (1f - u * u));
            cut.Look = _player.position;
            yield return null;
        }

        // The landing.
        _player.position = ground;
        Play(ImpactState, 0.05f);
        _floorY = floor.y;
        _standY = ground.y;
        _holdAboveFloor = true; // lying and getting up, the pose would sink him into the floor
        EnemyFx.Impact(floor, 3.2f, new Color(0.85f, 0.4f, 0.25f, 1f));
        EnemyFx.GroundCrack(floor, 2.6f);
        FMODUnity.RuntimeManager.PlayOneShot("event:/Combat/Impact_Heavy", floor);
        StartCoroutine(Shake(cut, 0.35f, 0.7f));
        cut.Look = floor + Vector3.up * 0.4f;
        yield return new WaitForSeconds(1.3f);

        // Up, slowly, the camera drifting in.
        Play(GetUpState, 0.2f);
        Vector3 near = floor + forward * 3.6f + right * 1.4f + Vector3.up * 1.4f;
        yield return cut.Shot(cut.Position, near, () => floor + Vector3.up * 0.4f, () => body(1.4f), 1.6f);

        // Dazed, then the head shake, in close.
        Play(DazedState, 0.25f);
        StartCoroutine(Sway(cut, 1.2f));
        yield return new WaitForSeconds(1.2f);
        Play(ShakeHeadState, 0.2f);
        Vector3 face = floor + forward * 2.2f + Vector3.up * 1.7f;
        yield return cut.Shot(cut.Position, face, () => body(1.4f), () => body(1.65f), 0.6f);
        yield return new WaitForSeconds(1.2f);

        // Looking round the pit, from over his shoulder...
        Play(LookAroundState, 0.25f);
        Vector3 shoulder = floor - forward * 2.6f + right * 0.9f + Vector3.up * 1.9f;
        yield return cut.Shot(cut.Position, shoulder, () => body(1.65f), () => body(1.2f) + forward * 6f, 1.2f);
        yield return new WaitForSeconds(1.4f);

        // ...until he sees the chest.
        Vector3 chestAt = chest.transform.position;
        GroundFx ring = EnemyFx.Ring(FloorAnchor(chest.transform.position), 2f, Gold);
        Vector3 toChest = Flat(chestAt - floor).normalized;
        Vector3 overChest = floor - toChest * 2.2f + Vector3.Cross(Vector3.up, toChest) * -1f + Vector3.up * 2f;
        yield return cut.Shot(cut.Position, overChest, () => body(1.2f) + forward * 6f, () => chestAt + Vector3.up * 0.8f, 1.1f);
        yield return new WaitForSeconds(1.1f);

        _holdAboveFloor = false;
        // Control back, facing the chest.
        Play("Locomotion", 0.3f);
        _player.rotation = Quaternion.LookRotation(toChest);
        _controller.enabled = true;
        cut.End();
        _ringOnChest = ring;
    }

    private GroundFx _ringOnChest;
    #endregion

    #region 2. The chest
    private IEnumerator ToTheChest()
    {
        KeyHints.Show(Row("Move", "Tut_Move"), Row("Roll", "Tut_Sprint"), Row("Roll", "Tut_Roll"));
        bool moved = false, sprinted = false, rolled = false;
        float hideHintsAt = float.MaxValue;
        Vector3 start = _player.position;
        float fast = 0f;
        Guide(chest.transform.position);

        while (!InReach(chest.transform.position, chest.InteractionRadius))
        {
            if (!moved && Flat(_player.position - start).magnitude > 2f) { moved = true; KeyHints.Tick(0); }

            fast = Flat(_movement.Velocity).magnitude > _movement.walkSpeed + 1f ? fast + Time.deltaTime : 0f;
            if (!sprinted && fast > 0.35f) { sprinted = true; KeyHints.Tick(1); }
            if (!rolled && _state.GetCurrentState() == PlayerState.Dodging) { rolled = true; KeyHints.Tick(2); }

            // All three done: the panel goes (a moment after, so the last tick is seen).
            if (moved && sprinted && rolled && hideHintsAt == float.MaxValue) hideHintsAt = Time.time + 0.8f;
            if (Time.time >= hideHintsAt) { KeyHints.Hide(); hideHintsAt = float.PositiveInfinity; }
            yield return null;
        }

        Guide(null);
        KeyHints.Show(Row("Interact", "Tut_OpenChest"));
        while (chest.gameObject.activeInHierarchy && !ChoiceOpen()) yield return null;
        KeyHints.Hide();
    }

    // The gauntlet chosen: the chest closes itself and the character screen opens with it on his fist.
    private IEnumerator Equip()
    {
        while (chest.gameObject.activeInHierarchy)
        {
            // Backed out of the chest without taking it: point at it again.
            if (!ChoiceOpen() && InReach(chest.transform.position, chest.InteractionRadius)) KeyHints.Show(Row("Interact", "Tut_OpenChest"));
            while (!ChoiceOpen() && chest.gameObject.activeInHierarchy) yield return null;
            KeyHints.Hide();
            while (ChoiceOpen()) yield return null;
        }
        if (_ringOnChest != null) _ringOnChest.End();

        KeyHints.Show(Row("OpenInventory", "Tut_Equipped"));
        yield return null;
        while (InventoryOpen()) yield return null;
        KeyHints.Hide();

        // The way on opens.
        if (exitBarrier != null)
        {
            Vector3 at = exitBarrier.transform.position;
            FMODUnity.RuntimeManager.PlayOneShot("event:/Environment/Glass_Shatter", at);
            EnemyFx.Impact(at, 3f, Gold);
            exitBarrier.SetActive(false);
        }
        _ringAhead = EnemyFx.Ring(FloorAnchor(chamberCentre.position), 2.4f, Gold);
        Guide(chamberCentre.position);
    }

    private GroundFx _ringAhead;
    #endregion

    #region 3-5. The fights and the portal
    private IEnumerator WalkTo(Transform place, float radius)
    {
        while (!InReach(place.position, radius)) yield return null;
        if (_ringAhead != null) _ringAhead.End();
        Guide(null);
    }

    private IEnumerator Fight(GameObject grunt)
    {
        KeyHints.Show(Row("LightAttack", "Tut_Light"), Row("HeavyAttack", "Tut_Heavy"));
        InputAction light = InputSystem.actions.FindAction("LightAttack");
        InputAction heavy = InputSystem.actions.FindAction("HeavyAttack");
        bool lit = false, heaved = false;

        while (!IsDead(grunt))
        {
            if (!lit && light.WasPressedThisFrame()) { lit = true; KeyHints.Tick(0); }
            if (!heaved && heavy.WasPressedThisFrame()) { heaved = true; KeyHints.Tick(1); }
            yield return null;
        }
        KeyHints.Hide();
    }

    // Shot in the back by an eyeball he never saw coming. Time stops until he drinks a potion.
    private IEnumerator Ambush(System.Action<GameObject> spawned)
    {
        while (_state.GetCurrentState() != PlayerState.Idle && _state.GetCurrentState() != PlayerState.Walking) yield return null;

        CutsceneCamera cut = CutsceneCamera.Begin();
        _controller.enabled = false;
        Play("Locomotion", 0.2f);

        Vector3 back = -Flat(_player.forward).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, back);
        Vector3 eyeAt = AmbushSpot(back);
        GameObject eye = Spawn(eyePrefab, eyeAt, Quaternion.LookRotation(-back));
        spawned(eye);

        // In front of him, so we see what he doesn't: it rising behind him.
        // Low and wide, both of them in frame. (His pivot is about a metre above his feet.)
        Transform eyeT = eye.transform;
        cut.Position = _player.position - back * 4.8f + side * 1.8f + Vector3.up * 0.2f;
        cut.Look = Vector3.Lerp(_player.position, eyeT.position, 0.55f) + Vector3.up * 0.9f;
        yield return null; // the eyeball's Start, so it knows where he is
        cut.Look = Vector3.Lerp(_player.position, eyeT.position, 0.55f) + Vector3.up * 0.9f;

        // It should hurt, never kill: if the grunt already hurt him, he quietly gets some back first.
        if (_health.CurrentHealth < ambushDamage + 20f) _health.Heal(ambushDamage + 20f - _health.CurrentHealth);
        // The hit has to land: the potion lesson needs him hurt (a potion can't be drunk at full health). So no
        // debug invincibility and no leftover i-frames from the grunt's last blow, for this one shot.
        bool debugInvincible = PlayerHealth.DebugInvincible;
        PlayerHealth.DebugInvincible = false;
        _health.EndHitInvincibility();

        float healthBefore = _health.CurrentHealth;
        float shot = ambushDamage / Mathf.Max(0.01f, enemyDamage); // the tutorial's damage scale applies to it too; undo that
        eye.GetComponent<EyeEnemy>().FireNow(shot);
        _controller.enabled = true; // so the fireball finds him

        for (float t = 0f; t < 3f && _health.CurrentHealth >= healthBefore; t += Time.deltaTime) yield return null;
        if (_health.CurrentHealth >= healthBefore) // it missed: it doesn't
        {
            _health.EndHitInvincibility();
            _health.TakeDamage(shot, 0, eye);
        }
        PlayerHealth.DebugInvincible = debugInvincible;
        Play("LargeStumble", 0.05f);
        StartCoroutine(Shake(cut, 0.25f, 0.5f));
        yield return new WaitForSeconds(1.1f);

        // Frozen, until he drinks. Not before the controls are back (the cutscene releases them a moment after).
        cut.End();
        Play("Locomotion", 0.2f);
        _state.SetPlayerState(PlayerState.Idle);
        for (float t = 0f; t < 1f && GameplayInputGate.Instance != null && GameplayInputGate.Instance.IsSuspended; t += Time.unscaledDeltaTime)
            yield return null;

        if (_health.CurrentHealth < _health.MaxHealth && _health.CurrentPotions > 0)
        {
            Time.timeScale = 0f;
            KeyHints.Show(Row("Heal", "Tut_Potion"));
            InputAction heal = InputSystem.actions.FindAction("Heal");
            PlayerCombat combat = _player.GetComponent<PlayerCombat>();
            if (heal != null && !heal.enabled) heal.Enable(); // whatever else holds the controls, R works here
            int potions = _health.CurrentPotions;
            while (_health.CurrentPotions >= potions)
            {
                // The Heal key drinks as usual; if anything kept that from happening, drink here, so R always works.
                if (heal != null && heal.WasPressedThisFrame())
                {
                    _state.SetPlayerState(PlayerState.Idle);
                    combat.UsePotion();
                }
                yield return null;
            }
            KeyHints.Tick(0);
            Time.timeScale = 1f;
            yield return new WaitForSeconds(0.8f);
            KeyHints.Hide();
        }
    }

    // Behind him, in the open: straight back if there's room, else a little to either side or closer, the first spot
    // with a clear line to him (so the eyeball isn't in a wall, and the shot isn't stopped by one).
    private Vector3 AmbushSpot(Vector3 back)
    {
        Vector3 chest = _player.position + Vector3.up * 0.5f;
        foreach (float distance in new[] { 7f, 5.5f, 4f })
        foreach (float angle in new[] { 0f, 30f, -30f, 60f, -60f })
        {
            Vector3 spot = EncounterRoller.OnNavMesh(_player.position + Quaternion.Euler(0f, angle, 0f) * back * distance);
            if (Flat(spot - _player.position).magnitude < 3f) continue;
            if (!Physics.Linecast(chest, spot + Vector3.up * 1.5f, ~0, QueryTriggerInteraction.Ignore)) return spot;
        }
        return EncounterRoller.OnNavMesh(_player.position + back * 4f);
    }

    // The fight won: he levels up (the XP bar filled the rest of the way if the kills didn't), and learns where the
    // reward is. The portal waits until he's claimed it and closed the screen.
    private IEnumerator LevelUp()
    {
        ProgressBarCircle bar = ProgressBarCircle.Instance;
        if (bar == null) yield break;
        if (!bar.HasPendingLevelUps) bar.AddExperience(bar.expToNextLevel - bar.currentExp);
        yield return new WaitForSeconds(1f); // the level-up flourish on the bar first

        KeyHints.Show(Row("RewardsMenu", "Tut_LevelReward"));
        while (!RewardOpen() && bar.HasPendingLevelUps) yield return null;
        KeyHints.Hide();

        while (RewardOpen() || InventoryOpen()) yield return null;
        yield return new WaitForSeconds(0.5f);
    }

    private static bool RewardOpen() => RewardMenuManager.Instance != null && RewardMenuManager.Instance.IsRewardModeActive();

    private IEnumerator WaitDead(GameObject enemy)
    {
        while (!IsDead(enemy)) yield return null;
        yield return new WaitForSeconds(1f);
    }

    private IEnumerator ThePortal()
    {
        // The effect is a ring centred on its pivot: stood up off the floor, and big enough to walk into.
        GameObject portal = Instantiate(portalPrefab, portalPoint.position + Vector3.up * 2.8f, portalPoint.rotation);
        portal.transform.localScale *= 2f;
        FMODUnity.RuntimeManager.PlayOneShot("event:/Environment/Teleport", portalPoint.position);
        GroundFx ring = EnemyFx.Ring(FloorAnchor(portalPoint.position), 2.2f, Gold);
        Guide(portalPoint.position);
        KeyHints.Show(new KeyHints.Row { Action = null, Label = Text("Tut_Portal") });

        while (!InReach(portalPoint.position, 1.8f)) yield return null;
        Guide(null);
        KeyHints.Hide();
        ring.End();
        yield return new WaitForSeconds(0.35f); // the hint gone, so it isn't in the frame that gets twisted
        PortalTwist.Play(Finish, new Color(1f, 0.22f, 0.15f, 1f)); // the red portal's light
    }

    private void Finish()
    {
        PlayerPrefs.SetInt(DoneKey, 1);
        PlayerPrefs.Save();
        InventoryManager.StartEmptyHanded = false;

        if (ReplayFromMenu)
        {
            ReplayFromMenu = false;
            SceneManager.LoadScene("MainMenu");
            return;
        }

        // The tutorial's level and gems were for practice: the run proper starts fresh, at level 1 with the plain
        // gauntlets (an empty Backpack is what makes the Dungeon hand those out).
        if (PersistentEquipment.Instance != null) PersistentEquipment.Instance.ResetForNewRun();
        LoadingScreen.LoadLevel("Dungeon Level");
    }
    #endregion

    #region Keeping him above the floor
    // The lying and getting-up clips put his body lower than his feet; while they play, he's lifted just enough
    // that no part of him is under the floor. Done after the animation has posed him, every frame.
    private static readonly HumanBodyBones[] Limbs =
    {
        HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Head,
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
        HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
        HumanBodyBones.LeftHand, HumanBodyBones.RightHand,
    };
    private const float BodyThickness = 0.1f; // bones run through the middle of the limbs

    private bool _holdAboveFloor;
    private float _floorY, _standY;

    private void LateUpdate()
    {
        if (_holdAboveFloor && _animator != null)
        {
            float lowest = float.MaxValue;
            foreach (HumanBodyBones limb in Limbs)
            {
                Transform bone = _animator.GetBoneTransform(limb);
                if (bone != null) lowest = Mathf.Min(lowest, bone.position.y);
            }
            if (lowest < float.MaxValue)
            {
                Vector3 at = _player.position;
                at.y = Mathf.Max(_standY, _floorY + BodyThickness - (lowest - at.y));
                _player.position = at;
            }
        }

        UpdateArrow();
    }
    #endregion

    #region The arrow on the floor
    // When he has somewhere to be and isn't getting closer to it, an arrow on the floor in front of him points the way.
    private const float ArrowAfter = 2.5f;   // seconds without getting closer
    private const float ArrowNotWithin = 4f; // metres: close enough, no arrow

    private Vector3? _guideTo;
    private float _closest, _stalled, _arrowAlpha;
    private Transform _arrow;
    private Material _arrowMaterial;

    private void Guide(Vector3? to)
    {
        _guideTo = to;
        _closest = float.MaxValue;
        _stalled = 0f;
    }

    private void UpdateArrow()
    {
        if (_player == null) return;

        bool show = false;
        Vector3 way = Vector3.zero;
        if (_guideTo.HasValue && Time.timeScale > 0f && _movement.enabled)
        {
            way = Flat(_guideTo.Value - _player.position);
            float distance = way.magnitude;
            way = way.normalized;
            if (distance < _closest - 0.3f) { _closest = distance; _stalled = 0f; }
            else _stalled += Time.deltaTime;
            show = distance > ArrowNotWithin && _stalled > ArrowAfter;
        }

        _arrowAlpha = Mathf.MoveTowards(_arrowAlpha, show ? 1f : 0f, Time.unscaledDeltaTime * 3f);
        if (_arrowAlpha <= 0f) { if (_arrow != null) _arrow.gameObject.SetActive(false); return; }

        if (_arrow == null) _arrow = MakeArrow();
        _arrow.gameObject.SetActive(true);
        if (show) _arrow.rotation = Quaternion.LookRotation(Vector3.down, way); // flat, its tip along the way

        // Ahead of his feet, sliding forward and back, on the floor.
        Vector3 feet = _player.position + Vector3.down * (_controller.height * 0.5f - _controller.center.y);
        Vector3 at = feet + _arrow.up * (2.2f + Mathf.PingPong(Time.time * 0.8f, 0.6f));
        at.y = (UnityEngine.AI.NavMesh.SamplePosition(at, out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas) ? hit.position.y : feet.y) + 0.06f;
        _arrow.position = at;

        Color colour = Gold;
        colour.a = _arrowAlpha * (0.65f + 0.35f * Mathf.Sin(Time.time * 5f));
        _arrowMaterial.color = colour;
    }

    // An arrow drawn into a small texture, on a quad: unlit, so it shows in the dark of the pit.
    private Transform MakeArrow()
    {
        const int w = 32, h = 48;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float u = (x + 0.5f) / w - 0.5f, v = (y + 0.5f) / h;
            bool head = v > 0.5f && Mathf.Abs(u) < (1f - v) / 0.5f * 0.5f;
            bool shaft = v <= 0.5f && v > 0.04f && Mathf.Abs(u) < 0.15f;
            pixels[y * w + x] = head || shaft ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
        }
        texture.SetPixels32(pixels);
        texture.Apply();

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Guide Arrow";
        Destroy(quad.GetComponent<Collider>());
        quad.transform.localScale = new Vector3(1.3f, 1.95f, 1f);
        _arrowMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = texture, renderQueue = 3100 };
        quad.GetComponent<Renderer>().sharedMaterial = _arrowMaterial;
        quad.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return quad.transform;
    }

    // A point on the floor under (or beside) somewhere, for a ring to sit on: a chest's own pivot is up in its lid.
    private Transform FloorAnchor(Vector3 above)
    {
        Vector3 at = above;
        if (UnityEngine.AI.NavMesh.SamplePosition(above, out UnityEngine.AI.NavMeshHit hit, 4f, UnityEngine.AI.NavMesh.AllAreas)) at.y = hit.position.y;
        var anchor = new GameObject("Marker").transform;
        anchor.position = at;
        return anchor;
    }
    #endregion

    #region Helpers
    private GameObject Spawn(GameObject prefab, Vector3 at, Quaternion facing)
    {
        at = EncounterRoller.OnNavMesh(at);
        GameObject enemy = Instantiate(prefab, at, facing);
        if (enemy.TryGetComponent(out BaseEnemy stats)) stats.ScaleForLevel(1f, enemyDamage);
        FMODUnity.RuntimeManager.PlayOneShot("event:/Environment/Teleport", at);
        EnemyFx.Impact(at, 1.8f, EnemyFx.Hell);
        return enemy;
    }

    private static bool IsDead(GameObject enemy) =>
        enemy == null || (enemy.TryGetComponent(out BaseEnemy stats) && stats.IsDead());

    private bool InReach(Vector3 place, float radius) => Flat(_player.position - place).magnitude <= radius;

    private static bool ChoiceOpen() => GauntletChoiceUI.Instance != null && GauntletChoiceUI.Instance.gameObject.activeInHierarchy;

    private static bool InventoryOpen()
    {
        InventoryManager inventory = InventoryManager.Instance;
        return inventory != null && inventory.gameObject.activeInHierarchy && !GauntletScreenFx.IsClosing(inventory.gameObject);
    }

    private void Play(string state, float blend) => _animator.CrossFadeInFixedTime(state, blend, 0);

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    private static IEnumerator Shake(CutsceneCamera cut, float strength, float seconds)
    {
        for (float t = 0f; t < seconds && cut != null; t += Time.unscaledDeltaTime)
        {
            cut.Shake = strength * (1f - t / seconds);
            yield return null;
        }
        if (cut != null) cut.Shake = 0f;
    }

    // The world swims a little while he's dazed.
    private static IEnumerator Sway(CutsceneCamera cut, float seconds)
    {
        Vector3 look = cut.Look;
        for (float t = 0f; t < seconds && cut != null; t += Time.deltaTime)
        {
            cut.Look = look + new Vector3(Mathf.Sin(t * 2.3f) * 0.35f, Mathf.Sin(t * 1.7f) * 0.2f, 0f);
            yield return null;
        }
    }

    private static KeyHints.Row Row(string action, string key) => new KeyHints.Row { Action = action, Label = Text(key) };

    private static string Text(string key) => new LocalizedString("UIStrings", key).GetLocalizedString();
    #endregion
}
