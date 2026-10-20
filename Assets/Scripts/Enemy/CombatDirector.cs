using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Coordinates a group of melee enemies so they act like a pack instead of a mob (think Sifu):
/// only a few of them hold an "attack token" and are allowed to close in and swing, the rest circle the
/// player at a distance and wait their turn. Tokens rotate, flankers behind the player get priority, and the
/// group reads the player: it presses when the player drinks a potion, charges a heavy or is out of stamina,
/// and doesn't throw swings into a dodge roll.
///
/// Nothing needs placing in a scene: the first enemy creates the director. Drop a CombatDirector in a scene
/// yourself to tune it there instead.
/// </summary>
public class CombatDirector : MonoBehaviour
{
    private static CombatDirector _instance;

    public static CombatDirector Instance
    {
        get
        {
            if (_instance == null) _instance = FindFirstObjectByType<CombatDirector>();
            if (_instance == null) _instance = new GameObject("CombatDirector").AddComponent<CombatDirector>();
            return _instance;
        }
    }

    /// <summary>The director if there is one. Never creates one, so it is safe while a scene is unloading.</summary>
    public static CombatDirector Current => _instance;

    [Header("Attackers at once")]
    [Tooltip("At least this many enemies may attack together (when that many are fighting).")]
    [SerializeField] private int minAttackers = 2;
    [Tooltip("...and never more than this many. The limit wanders between the two so the pressure comes in waves.")]
    [SerializeField] private int maxAttackers = 4;
    [SerializeField] private Vector2 limitRerollSeconds = new Vector2(8f, 12f);

    [Header("Rotating the token")]
    [Tooltip("Seconds a token holder keeps its token after finishing an attack.")]
    [SerializeField] private float holdAfterAttack = 1.8f;
    [Tooltip("Seconds a holder may take to start an attack (walking in included) before the token moves on.")]
    [SerializeField] private float holdWithoutAttack = 3.5f;
    [Tooltip("A fresh token holder waits a random moment in this range before swinging, so a pack never attacks on the same frame.")]
    [SerializeField] private Vector2 attackStagger = new Vector2(0.15f, 0.7f);

    [Header("Circling")]
    [SerializeField] private float ringRadius = 6f;
    [SerializeField] private float orbitDegreesPerSecond = 14f;
    [Tooltip("Enemies waiting their turn keep at least this many degrees apart on the ring.")]
    [SerializeField] private float minDegreesApart = 40f;

    [Header("Reading the player")]
    [Tooltip("Extra attackers while the player is drinking a potion, charging a heavy attack or out of stamina.")]
    [SerializeField] private int extraAttackersWhenPunishing = 1;
    [SerializeField] private float lowStaminaFraction = 0.25f;

    private class Member
    {
        public BaseEnemy enemy;
        public float waitingSince;
        public float grantTime;
        public float lastActive;
        public float stagger;
        public bool attackedSinceGrant;
        public float ringOffset;
        public float angle;
        public bool angleSet;
    }

    private readonly Dictionary<BaseEnemy, Member> _members = new Dictionary<BaseEnemy, Member>();
    private readonly List<BaseEnemy> _scratch = new List<BaseEnemy>();
    private readonly List<Member> _waiting = new List<Member>();

    private int _limit = 3;
    private float _limitTimer;
    private float _orbitDirection = 1f;
    private float _orbitFlipTimer = 8f;
    private float _tickTimer;

    private Transform _player;
    private PlayerManager _playerManager;
    private PlayerStamina _playerStamina;
    private PlayerCombat _playerCombat;
    private bool _playerDodging, _playerPunishable;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        ApplySceneProfile(SceneManager.GetActiveScene().name);
        RerollLimit();
    }

    // Later levels push harder; everything else uses the inspector values.
    private void ApplySceneProfile(string sceneName)
    {
        if (sceneName != "Hell Level") return;

        minAttackers = Mathf.Max(minAttackers, 3);
        maxAttackers = Mathf.Max(maxAttackers, 5);
        holdWithoutAttack = Mathf.Min(holdWithoutAttack, 2.5f);
        holdAfterAttack = Mathf.Min(holdAfterAttack, 1.2f);
        attackStagger *= 0.6f;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    #region Registration
    public void Register(BaseEnemy enemy)
    {
        if (enemy == null || _members.ContainsKey(enemy)) return;

        _members[enemy] = new Member
        {
            enemy = enemy,
            waitingSince = Time.time,
            ringOffset = Random.Range(-0.8f, 1.2f),
            stagger = Random.Range(attackStagger.x, attackStagger.y)
        };
    }

    public void Unregister(BaseEnemy enemy)
    {
        if (enemy == null) return;
        enemy.HasAttackToken = false;
        _members.Remove(enemy);
    }

    /// <summary>Wakes every enemy near this one, so a pack arrives together instead of one by one.</summary>
    public void AlertAround(BaseEnemy source, float radius)
    {
        float sqr = radius * radius;
        foreach (var pair in _members)
        {
            BaseEnemy other = pair.Key;
            if (other == null || other == source) continue;
            if ((other.transform.position - source.transform.position).sqrMagnitude <= sqr) other.Alert();
        }
    }
    #endregion

    #region Permission
    /// <summary>
    /// Asked by an enemy right before it starts an attack. Hands out a token when one is free; the holder
    /// still has to wait out its stagger and may not swing while the player is rolling.
    /// </summary>
    public bool MayAttack(BaseEnemy enemy)
    {
        if (!_members.TryGetValue(enemy, out Member member)) return true;

        if (!enemy.HasAttackToken)
        {
            if (CountHolders() >= EffectiveLimit()) return false;
            Grant(member);
        }

        if (_playerDodging && DistanceToPlayer(enemy) < 4.5f) return false; // it would only whiff into the i-frames
        if (_playerPunishable) return true;                                  // press the advantage, no waiting
        return Time.time >= member.grantTime + member.stagger;
    }

    private void Grant(Member member)
    {
        member.enemy.HasAttackToken = true;
        member.grantTime = Time.time;
        member.lastActive = Time.time;
        member.attackedSinceGrant = false;
        member.stagger = Random.Range(attackStagger.x, attackStagger.y);
    }

    private void Release(Member member)
    {
        member.enemy.HasAttackToken = false;
        member.waitingSince = Time.time;
        member.angleSet = false; // pick up the ring from wherever it is standing now
    }

    /// <summary>The enemy finished an attack; the token it holds starts its short countdown.</summary>
    public void NotifyAttackEnded(BaseEnemy enemy)
    {
        if (_members.TryGetValue(enemy, out Member member)) member.lastActive = Time.time;
    }

    private int CountHolders()
    {
        int count = 0;
        foreach (var pair in _members) if (pair.Key != null && pair.Key.HasAttackToken) count++;
        return count;
    }

    private int EffectiveLimit() => _limit + (_playerPunishable ? extraAttackersWhenPunishing : 0);
    #endregion

    #region Ring positions
    /// <summary>Where a waiting enemy should stand: its spot on the ring around the player.</summary>
    public Vector3 GetWaitingPosition(BaseEnemy enemy)
    {
        Transform player = FindPlayer();
        if (player == null || !_members.TryGetValue(enemy, out Member member)) return enemy.transform.position;

        if (!member.angleSet)
        {
            Vector3 away = enemy.transform.position - player.position;
            member.angle = Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg;
            member.angleSet = true;
        }

        Vector3 dir = Quaternion.Euler(0f, member.angle, 0f) * Vector3.forward;
        return player.position + dir * (ringRadius + member.ringOffset);
    }
    #endregion

    private void Update()
    {
        _tickTimer -= Time.deltaTime;
        if (_tickTimer > 0f) return;
        _tickTimer = 0.15f;

        FindPlayer();
        ReadPlayer();
        TickLimit(0.15f);
        Prune();
        ManageTokens();
        SpreadWaitingEnemies(0.15f);
    }

    private void TickLimit(float dt)
    {
        _limitTimer -= dt;
        if (_limitTimer <= 0f) RerollLimit();

        _orbitFlipTimer -= dt;
        if (_orbitFlipTimer <= 0f)
        {
            _orbitDirection = -_orbitDirection;
            _orbitFlipTimer = Random.Range(6f, 11f);
        }
    }

    private void RerollLimit()
    {
        _limit = Random.Range(minAttackers, Mathf.Max(minAttackers, maxAttackers) + 1);
        _limitTimer = Random.Range(limitRerollSeconds.x, limitRerollSeconds.y);
    }

    private Transform FindPlayer()
    {
        if (_player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null)
            {
                _player = found.transform;
                _playerManager = found.GetComponent<PlayerManager>();
                _playerStamina = found.GetComponent<PlayerStamina>();
                _playerCombat = found.GetComponent<PlayerCombat>();
            }
        }
        return _player;
    }

    private float DistanceToPlayer(BaseEnemy enemy)
    {
        return _player != null ? Vector3.Distance(enemy.transform.position, _player.position) : float.MaxValue;
    }

    // What the player is doing right now decides how hard the pack leans in.
    private void ReadPlayer()
    {
        _playerDodging = false;
        _playerPunishable = false;
        if (_playerManager == null) return;

        PlayerState state = _playerManager.GetCurrentState();
        _playerDodging = state == PlayerState.Dodging;

        bool healing = state == PlayerState.Healing;
        bool chargingHeavy = _playerCombat != null && _playerCombat.IsChargingHeavy;
        bool winded = _playerStamina != null && _playerStamina.MaxStamina > 0f
                      && _playerStamina.CurrentStamina / _playerStamina.MaxStamina <= lowStaminaFraction;

        _playerPunishable = healing || chargingHeavy || winded;
    }

    private void Prune()
    {
        _scratch.Clear();
        foreach (var pair in _members)
        {
            if (pair.Key == null) _scratch.Add(pair.Key);
        }
        foreach (BaseEnemy gone in _scratch) _members.Remove(gone);
    }

    private bool IsCandidate(BaseEnemy enemy)
    {
        return enemy != null && enemy.GroupAIActive && !enemy.IsDead() && enemy.IsEngagedWithPlayer;
    }

    private void ManageTokens()
    {
        float now = Time.time;
        int holders = 0;
        _waiting.Clear();

        foreach (var pair in _members)
        {
            Member member = pair.Value;
            BaseEnemy enemy = pair.Key;
            if (enemy == null) continue;

            if (!IsCandidate(enemy))
            {
                if (enemy.HasAttackToken) Release(member);
                continue;
            }

            if (enemy.HasAttackToken)
            {
                if (enemy.IsBlockingOrStunned()) { Release(member); continue; } // knocked about: pass it on

                if (enemy.IsAttackingOrCharging)
                {
                    member.lastActive = now;
                    member.attackedSinceGrant = true;
                    holders++;
                    continue;
                }

                float patience = member.attackedSinceGrant ? holdAfterAttack : holdWithoutAttack;
                if (now - member.lastActive > patience) Release(member);
                else holders++;
            }

            if (!enemy.HasAttackToken) _waiting.Add(member);
        }

        // Fill free tokens, most deserving first: waited longest, closest, and flankers behind the player.
        int limit = Mathf.Min(EffectiveLimit(), holders + _waiting.Count);
        while (holders < limit && _waiting.Count > 0)
        {
            int best = 0;
            float bestScore = float.MinValue;
            for (int i = 0; i < _waiting.Count; i++)
            {
                float score = Score(_waiting[i], now);
                if (score > bestScore) { bestScore = score; best = i; }
            }

            Grant(_waiting[best]);
            _waiting.RemoveAt(best);
            holders++;
        }
    }

    private float Score(Member member, float now)
    {
        BaseEnemy enemy = member.enemy;
        float score = (now - member.waitingSince) * 0.6f;                        // fairness
        score -= DistanceToPlayer(enemy) * 0.12f;                                // the near ones are ready sooner
        score += Random.value * 0.4f;                                            // a little unpredictability

        if (_player != null)
        {
            Vector3 toEnemy = (enemy.transform.position - _player.position).normalized;
            if (Vector3.Dot(_player.forward, toEnemy) < -0.25f) score += 1.6f;   // behind the player: flank
        }
        return score;
    }

    // Slides each waiting enemy's ring angle round the player and keeps neighbours from bunching up.
    private void SpreadWaitingEnemies(float dt)
    {
        _waiting.Clear();
        foreach (var pair in _members)
        {
            if (pair.Key == null || pair.Key.HasAttackToken || !IsCandidate(pair.Key)) continue;

            Member member = pair.Value;
            if (!member.angleSet)
            {
                Vector3 away = pair.Key.transform.position - (_player != null ? _player.position : Vector3.zero);
                member.angle = Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg;
                member.angleSet = true;
            }

            member.angle += _orbitDirection * orbitDegreesPerSecond * dt;
            _waiting.Add(member);
        }

        if (_waiting.Count < 2) return;

        _waiting.Sort((a, b) => Mathf.Repeat(a.angle, 360f).CompareTo(Mathf.Repeat(b.angle, 360f)));

        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < _waiting.Count; i++)
            {
                Member a = _waiting[i];
                Member b = _waiting[(i + 1) % _waiting.Count];
                float gap = Mathf.Repeat(b.angle - a.angle, 360f);

                if (gap < minDegreesApart)
                {
                    float push = (minDegreesApart - gap) * 0.5f;
                    a.angle -= push;
                    b.angle += push;
                }
            }
        }
    }
}
