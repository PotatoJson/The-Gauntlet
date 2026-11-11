using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Pergius hides. As the fight starts, and again at 40% health, he vanishes into one of several banks of fog round
// the edge of his arena. From the fog come waves of his demons, and lasers at the player out of every bank. Finding
// him (walking into the bank he's in: a pair of red eyes that blinks, his gold lasers and his growl give it away) drags him out, dazed for a
// few seconds. Walking into the wrong bank blows it away in their face and stuns them. Left alone, he steps out on his own.
public partial class HellLord
{
    private const int FogBanks = 5;
    private const int PuffsPerBank = 40;
    private const float BankRadius = 3.5f;
    private const float EdgeShare = 0.72f; // how far out towards the edge of the floor the banks sit
    private const float EyeBlinkEvery = 2.2f, EyeBlinkSeconds = 0.15f, GrowlEvery = 3.5f;

    [Header("Hiding in the fog")]
    [SerializeField] private Material fogMaterial;
    [SerializeField] private Color fogColour = new Color(0.42f, 0.38f, 0.46f, 0.7f);
    [SerializeField, Range(0f, 1f), Tooltip("He hides again when his health falls this low")]
    private float hideAgainAt = 0.4f;
    [SerializeField, Tooltip("How long he stays hidden if nobody finds him")]
    private float hideSeconds = 35f;
    [SerializeField, Tooltip("How long he's dazed when he's found")]
    private float foundStunSeconds = 3f;
    [SerializeField, Tooltip("Walking into the wrong bank of fog stuns the player this long")]
    private float wrongBankStunSeconds = 2f;
    [SerializeField] private float waveInterval = 7f;
    [SerializeField] private int waves = 3;
    [SerializeField] private float laserInterval = 2.2f;
    [SerializeField] private float laserDamage = 18f;
    [SerializeField, Tooltip("How long the thin line shows before the laser fires down it")]
    private float laserWarnSeconds = 0.9f;

    private bool _hiding, _hidOnce, _hidTwice, _stunningPlayer;
    private readonly List<GameObject> _summons = new List<GameObject>();
    private readonly List<GameObject> _hideThings = new List<GameObject>();
    private EnemySpawner _spawner;

    private class Bank
    {
        public Vector3 Centre;
        public ParticleSystem Fog;
        public bool Gone;
    }

    // From the fight loop: hide at the start of the fight and once more at 40% health.
    private bool TryHide()
    {
        if (_hiding || IsDead()) return false;

        bool start = !_hidOnce && isEngaged;
        bool again = _hidOnce && !_hidTwice && currentHealth / maxHealth <= hideAgainAt;
        if (!start && !again) return false;

        if (start) _hidOnce = true;
        else _hidTwice = true;
        StopCasting();
        EndBeamAttack();
        StartCoroutine(Hide());
        return true;
    }

    private IEnumerator Hide()
    {
        _hiding = true;
        isAttacking = true;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        if (_spawner == null) _spawner = FindFirstObjectByType<EnemySpawner>();

        // Gone in a burst of fog.
        FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Pergius_Vanish", transform.position);
        EnemyFx.Impact(transform.position, 4f, EnemyFx.Hell);
        StoryDialogue.Say("Story_A2_Hide_01", Speaker.Pergius);
        yield return new WaitForSeconds(0.4f);
        SetHidden(true);

        // Banks of fog round the edge of the floor, and him in one of them.
        Vector3 centre = ArenaCentre();
        var banks = new List<Bank>();
        float turn = Random.value * 360f;
        for (int i = 0; i < FogBanks; i++)
        {
            Vector3 along = Quaternion.Euler(0f, turn + i * 360f / FogBanks, 0f) * Vector3.forward;
            Vector3 at = centre + along * WardRing.EdgeDistance(centre, along) * EdgeShare;
            if (NavMesh.SamplePosition(at, out NavMeshHit onFloor, 4f, NavMesh.AllAreas)) at = onFloor.position;
            banks.Add(new Bank { Centre = at, Fog = FogBank(at) });
        }
        Bank lair = banks[Random.Range(0, banks.Count)];
        navAgent.Warp(lair.Centre);
        transform.position = lair.Centre;
        GameObject eyes = Eyes(lair.Centre);
        _hideThings.Add(eyes);

        float waveClock = 1.5f, laserClock = laserInterval, blinkClock = EyeBlinkEvery, growlClock = 1f;
        int wavesSent = 0;
        bool found = false;
        for (float hidden = 0f; hidden < hideSeconds && !found && !IsDead(); hidden += Time.deltaTime)
        {
            waveClock -= Time.deltaTime;
            if (waveClock <= 0f && wavesSent < waves)
            {
                waveClock = waveInterval;
                wavesSent++;
                SendWave(banks, lair, wavesSent);
            }

            laserClock -= Time.deltaTime;
            if (laserClock <= 0f)
            {
                laserClock = laserInterval;
                List<Bank> standing = banks.FindAll(bank => !bank.Gone);
                Bank firing = standing[Random.Range(0, standing.Count)];
                StartCoroutine(Laser(firing.Centre + Vector3.up * 3f, firing == lair)); // his own are gold

            }

            // The tells: his eyes blink in the fog, and he growls from it now and then.
            blinkClock -= Time.deltaTime;
            if (eyes != null) eyes.SetActive(blinkClock > 0f || blinkClock < -EyeBlinkSeconds);
            if (blinkClock < -EyeBlinkSeconds) blinkClock = EyeBlinkEvery * Random.Range(0.8f, 1.2f);
            growlClock -= Time.deltaTime;
            if (growlClock <= 0f)
            {
                growlClock = GrowlEvery * Random.Range(0.8f, 1.25f);
                FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Pergius_Growl", lair.Centre + Vector3.up * 3f);
            }

            // The player walks into a bank: his, and he's dragged out; another, and it bursts in their face and stuns them.
            foreach (Bank bank in banks)
            {
                if (bank.Gone || player == null) continue;
                Vector3 gap = player.position - bank.Centre;
                if (Mathf.Abs(gap.y) > 3f || new Vector2(gap.x, gap.z).magnitude > BankRadius) continue;

                if (bank == lair) found = true;
                else
                {
                    Disperse(bank);
                    StartCoroutine(StunPlayer(wrongBankStunSeconds)); // the wrong bank: it bursts in their face
                }
            }
            yield return null;
        }

        foreach (Bank bank in banks) if (!bank.Gone) Disperse(bank, bank == lair);
        foreach (GameObject thing in _hideThings) if (thing != null) Destroy(thing);
        _hideThings.Clear();

        // Out of the fog.
        SetHidden(false);
        EnemyFx.Impact(transform.position, 3.5f, EnemyFx.Hell);
        FMODUnity.RuntimeManager.PlayOneShot("event:/Combat/Impact_Heavy", transform.position);
        _hiding = false;
        isAttacking = false;
        if (found) yield return Dazed();
        else if (navAgent.isOnNavMesh) navAgent.isStopped = false;
    }

    // Dragged out of hiding: down on one knee for a few seconds, stars in his eyes.
    private IEnumerator Dazed()
    {
        StoryDialogue.Say("Story_A2_Found_01", Speaker.Pergius);
        ApplyStun(foundStunSeconds);
        animator.CrossFadeInFixedTime("Die", 0.2f); // the start of his fall, held
        animator.speed = 0.5f;
        Renderer body = GetComponentInChildren<Renderer>();
        Transform head = new GameObject("PergiusHead").transform;
        head.SetParent(transform, false);
        head.position = body != null ? new Vector3(transform.position.x, body.bounds.max.y, transform.position.z) : transform.position + Vector3.up * 5f;
        Destroy(head.gameObject, foundStunSeconds + 0.5f);
        DizzyStars.Show(head, foundStunSeconds, 1.6f);
        for (float t = 0f; t < foundStunSeconds; t += 0.5f)
        {
            if (t >= 1.1f) animator.speed = 0f;
            yield return new WaitForSeconds(0.5f);
        }
        animator.speed = 1f;
        animator.CrossFadeInFixedTime("HumanM@Idle01", 0.4f);
    }

    private void SetHidden(bool hidden)
    {
        foreach (Renderer part in GetComponentsInChildren<Renderer>(true)) part.enabled = !hidden;
        Collider body = GetComponent<Collider>();
        if (body != null) body.enabled = !hidden;
    }

    private Vector3 ArenaCentre()
    {
        if (_spawner == null) _spawner = FindFirstObjectByType<EnemySpawner>();
        ChamberData chamber = _spawner != null ? _spawner.chambers.Find(c => c.activeEnemies.Contains(gameObject)) : null;
        if (chamber != null && chamber.chamberTriggers.Count > 0 && chamber.chamberTriggers[0] != null)
            return EnemyFx.FloorUnder(chamber.chamberTriggers[0].bounds.center + Vector3.up * 2f);
        return transform.position;
    }

    private ParticleSystem FogBank(Vector3 at)
    {
        GameObject host = new GameObject("FogBank");
        host.transform.position = at;
        _hideThings.Add(host);
        ParticleSystem fog = host.AddComponent<ParticleSystem>();
        fog.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = fog.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = PuffsPerBank;
        ParticleSystem.EmissionModule emission = fog.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = fog.shape;
        shape.enabled = false;
        ParticleSystemRenderer view = fog.GetComponent<ParticleSystemRenderer>();
        view.sharedMaterial = fogMaterial;
        view.sortMode = ParticleSystemSortMode.Distance;

        for (int i = 0; i < PuffsPerBank; i++)
        {
            Vector2 spread = Random.insideUnitCircle * BankRadius;
            fog.Emit(new ParticleSystem.EmitParams
            {
                position = at + new Vector3(spread.x, Random.Range(0.5f, 6f), spread.y),
                startSize = Random.Range(4f, 7f),
                startLifetime = 120f,
                startColor = fogColour,
                rotation = Random.Range(0f, 360f),
                velocity = Random.insideUnitSphere * 0.2f
            }, 1);
        }
        EnemyFx.Impact(at, BankRadius, new Color(0.6f, 0.55f, 0.7f, 1f));
        return fog;
    }

    // Two faint red eyes in the fog where he is: the only way to tell, and only from close by.
    private GameObject Eyes(Vector3 at)
    {
        GameObject eyes = new GameObject("PergiusEyes");
        eyes.transform.position = at + Vector3.up * 4.6f;
        Light glow = eyes.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.15f, 0.1f);
        glow.range = 7f;
        glow.intensity = 3.5f;
        // Drawn after the fog, so the fog doesn't swallow them (walls in front still do).
        Material red = new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 0.25f, 0.15f, 1f), renderQueue = 4000 };
        foreach (float side in new[] { -0.28f, 0.28f })
        {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(eye.GetComponent<Collider>());
            eye.transform.SetParent(eyes.transform, false);
            eye.transform.localPosition = new Vector3(side, 0f, 0f);
            eye.transform.localScale = new Vector3(0.24f, 0.1f, 0.1f);
            eye.GetComponent<Renderer>().sharedMaterial = red;
        }
        return eyes;
    }

    private void Disperse(Bank bank, bool quiet = false)
    {
        bank.Gone = true;
        if (bank.Fog != null)
        {
            bank.Fog.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Destroy(bank.Fog.gameObject, 0.1f);
        }
        EnemyFx.Impact(bank.Centre, BankRadius + 1f, new Color(0.6f, 0.55f, 0.7f, 1f));
        if (!quiet) FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Pergius_Vanish", bank.Centre);
    }

    // Demons out of the fog, each from a bank of its own, harder each wave: two grunts and a caster; two grunts and
    // two eyeballs; two elites.
    private void SendWave(List<Bank> banks, Bank lair, int number)
    {
        if (_spawner == null) return;
        GameObject[] wave = number switch
        {
            1 => new[] { _spawner.gruntPrefab, _spawner.gruntPrefab, _spawner.casterPrefab },
            2 => new[] { _spawner.gruntPrefab, _spawner.gruntPrefab, _spawner.peeperPrefab, _spawner.peeperPrefab },
            _ => new[] { _spawner.elitePrefab, _spawner.elitePrefab },
        };
        List<Bank> standing = banks.FindAll(bank => !bank.Gone);
        for (int i = 0; i < wave.Length && standing.Count > 0; i++)
        {
            Summon(wave[i], standing[(i + Random.Range(0, standing.Count)) % standing.Count].Centre);
        }
        FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/HellLord_Cast", lair.Centre);
    }

    private void Summon(GameObject prefab, Vector3 at)
    {
        if (prefab == null || _spawner == null) return;
        Vector3 toPlayer = player != null ? player.position - at : Vector3.forward;
        toPlayer.y = 0f;
        GameObject demon = _spawner.Summon(prefab, at, Quaternion.LookRotation(toPlayer.sqrMagnitude > 0.01f ? toPlayer : Vector3.forward), gameObject);
        _summons.Add(demon);
        EnemyFx.Impact(at, 2f, EnemyFx.Hell);
    }

    // A thin line from the fog to the player, then the laser down it.
    private IEnumerator Laser(Vector3 from, bool his)
    {
        if (player == null) yield break;

        GameObject host = new GameObject("FogLaser");
        _hideThings.Add(host);
        LineRenderer line = host.AddComponent<LineRenderer>();
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.positionCount = 2;
        line.numCapVertices = 2;
        // His own lasers burn gold; the empty banks' burn red. Watching where the gold comes from finds him.
        Color warn = his ? new Color(1f, 0.85f, 0.25f, 0.6f) : new Color(1f, 0.2f, 0.15f, 0.55f);
        Color fire = his ? new Color(1f, 0.95f, 0.55f, 1f) : new Color(1f, 0.55f, 0.3f, 1f);

        FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Pergius_Laser", from); // its whine rises through the warning, then fires
        Vector3 to = player.position + Vector3.up;
        for (float t = 0f; t < laserWarnSeconds; t += Time.deltaTime)
        {
            if (line == null) yield break; // he came out of the fog and took his lasers with him
            if (t < laserWarnSeconds * 0.65f && player != null) to = player.position + Vector3.up; // it follows, then holds
            Vector3 end = from + (to - from).normalized * 40f;
            line.SetPosition(0, from);
            line.SetPosition(1, end);
            line.startWidth = line.endWidth = 0.05f + 0.05f * t / laserWarnSeconds;
            line.startColor = line.endColor = warn;
            yield return null;
        }

        if (line == null) yield break;
        Vector3 along = (to - from).normalized;
        line.startColor = line.endColor = fire;
        line.startWidth = line.endWidth = 0.6f;
        if (player != null && playerHealth != null)
        {
            Vector3 chest = player.position + Vector3.up;
            Vector3 nearest = from + along * Mathf.Max(0f, Vector3.Dot(chest - from, along));
            if (Vector3.Distance(chest, nearest) < 1f) playerHealth.TakeDamage(laserDamage, 0, gameObject);
        }
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            if (line == null) yield break;
            line.startWidth = line.endWidth = Mathf.Lerp(0.6f, 0f, t / 0.25f);
            yield return null;
        }
        if (host != null) Destroy(host);
    }

    // Whatever he hid with goes when he does.
    // Dazed by a bank of fog bursting in their face: no moving or attacking for a moment, stars over their head.
    private IEnumerator StunPlayer(float seconds)
    {
        if (_stunningPlayer || player == null) yield break;
        _stunningPlayer = true;
        if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.SuspendWhileRunning(this);
        if (playerManager != null) playerManager.SetPlayerState(PlayerState.Staggered);
        Animator body = player.GetComponentInChildren<Animator>();
        if (body != null) body.CrossFadeInFixedTime("LargeStumble", 0.1f);
        DizzyStars.Show(body != null ? body.GetBoneTransform(HumanBodyBones.Head) : player, seconds, 0.6f);
        EnemyFx.Impact(player.position, 2f, new Color(0.6f, 0.55f, 0.7f, 1f));
        yield return new WaitForSeconds(seconds);
        ReleasePlayerStun();
    }

    private void ReleasePlayerStun()
    {
        if (!_stunningPlayer) return;
        _stunningPlayer = false;
        if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.RestoreWhenReleased(this);
        if (playerManager != null) playerManager.SetPlayerState(PlayerState.Idle);
    }

    private void EndHiding()
    {
        ReleasePlayerStun();
        foreach (GameObject thing in _hideThings) if (thing != null) Destroy(thing);
        _hideThings.Clear();
        foreach (GameObject demon in _summons)
        {
            if (demon != null && demon.TryGetComponent(out BaseEnemy enemy) && !enemy.IsDead()) enemy.TakeDamage(999999f);
        }
        _summons.Clear();
        _hiding = false;
    }
}
