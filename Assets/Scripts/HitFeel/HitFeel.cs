using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum HitStrength
{
    Light,
    Heavy,
    Kill
}

/// <summary>
/// The small things that make a hit feel good: a short enemy pause, a white flash, a camera nudge and a
/// bit of gamepad rumble when the player lands a hit; a camera nudge when the player is hit; and a short
/// slow-mo on the last kill of a fight. One entry point so every attack type feels the same.
/// </summary>
public static class HitFeel
{
    // Melee calls BaseEnemy.TakeDamage itself and then plays its own, more precise feedback.
    // This tells BaseEnemy not to add a second one for the same hit.
    public static bool InMelee;

    private static HitFeelRunner _runner;

    private static HitFeelRunner Runner
    {
        get
        {
            if (_runner == null)
            {
                var go = new GameObject("[HitFeel]");
                Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<HitFeelRunner>();
            }
            return _runner;
        }
    }

    /// <summary>The player landed a hit on an enemy.</summary>
    public static void EnemyHit(BaseEnemy enemy, HitStrength strength)
    {
        if (enemy == null) return;
        if (!Runner.TryBeginHit(enemy)) return; // one hit, one reaction

        float pause = strength == HitStrength.Light ? 0.05f : strength == HitStrength.Heavy ? 0.1f : 0.12f;
        float nudge = strength == HitStrength.Light ? 0.035f : strength == HitStrength.Heavy ? 0.09f : 0.12f;

        // Pause on the runner, not on the enemy: a killing blow stops the enemy's own coroutines.
        Runner.Pause(enemy, pause);

        HitFlash flash = enemy.GetComponent<HitFlash>();
        if (flash == null) flash = enemy.gameObject.AddComponent<HitFlash>();
        flash.Flash(0.07f);

        if (PlayerCamera.Instance != null && PlayerCamera.Instance.playerTarget != null)
        {
            Vector3 dir = enemy.transform.position - PlayerCamera.Instance.playerTarget.position;
            PlayerCamera.Instance.Kick(dir, nudge);
        }

        Rumble(strength == HitStrength.Light ? 0.15f : 0.35f, strength == HitStrength.Light ? 0.1f : 0.45f, pause);
    }

    /// <summary>The player took a hit; a medium camera nudge away from the attacker.</summary>
    public static void PlayerHit(float damage, GameObject attacker)
    {
        if (PlayerCamera.Instance == null) return;

        Vector3 dir = Random.insideUnitSphere;
        if (attacker != null && PlayerCamera.Instance.playerTarget != null)
        {
            dir = PlayerCamera.Instance.playerTarget.position - attacker.transform.position;
        }

        PlayerCamera.Instance.Kick(dir, Mathf.Lerp(0.07f, 0.16f, Mathf.Clamp01(damage / 30f)));
    }

    private static void Rumble(float low, float high, float seconds)
    {
        Gamepad pad = Gamepad.current;
        if (pad == null || !InputHelper.IsGamepadLastUsed()) return;

        Runner.Rumble(pad, low, high, seconds);
    }
}

public class HitFeelRunner : MonoBehaviour
{
    private readonly Dictionary<int, float> _lastHit = new Dictionary<int, float>();
    private Coroutine _slowMo;

    private void OnEnable()
    {
        BaseEnemy.OnAnyEnemyDied += HandleEnemyDied;
    }

    private void OnDisable()
    {
        BaseEnemy.OnAnyEnemyDied -= HandleEnemyDied;
    }

    /// <summary>False if this enemy already reacted to a hit in the last few frames.</summary>
    public bool TryBeginHit(BaseEnemy enemy)
    {
        int id = enemy.GetInstanceID();
        if (_lastHit.TryGetValue(id, out float t) && Time.unscaledTime - t < 0.04f) return false;
        _lastHit[id] = Time.unscaledTime;
        return true;
    }

    public void Pause(BaseEnemy enemy, float seconds)
    {
        StartCoroutine(PauseRoutine(enemy, seconds));
    }

    private IEnumerator PauseRoutine(BaseEnemy enemy, float seconds)
    {
        enemy.FreezeForHitstop();
        yield return new WaitForSecondsRealtime(seconds); // real time, so the last-kill slow-mo can't stretch it
        if (enemy != null) enemy.UnfreezeFromHitstop();
    }

    public void Rumble(Gamepad pad, float low, float high, float seconds)
    {
        StartCoroutine(RumbleRoutine(pad, low, high, seconds));
    }

    private IEnumerator RumbleRoutine(Gamepad pad, float low, float high, float seconds)
    {
        pad.SetMotorSpeeds(low, high);
        yield return new WaitForSecondsRealtime(seconds);
        if (pad != null) pad.SetMotorSpeeds(0f, 0f);
    }

    // Last enemy down: a brief slow-mo so the finishing blow lands.
    private void HandleEnemyDied(BaseEnemy dead)
    {
        foreach (BaseEnemy other in FindObjectsByType<BaseEnemy>(FindObjectsSortMode.None))
        {
            if (other != dead && !other.IsDead()) return;
        }

        if (_slowMo != null) return;
        _slowMo = StartCoroutine(SlowMoRoutine());
    }

    private IEnumerator SlowMoRoutine()
    {
        // Only from normal speed, so it never fights a menu or another time effect.
        if (!Mathf.Approximately(Time.timeScale, 1f)) { _slowMo = null; yield break; }

        const float slow = 0.3f, hold = 0.35f, ease = 0.2f;
        Time.timeScale = slow;

        float t = 0f;
        while (t < hold)
        {
            if (!Mathf.Approximately(Time.timeScale, slow)) { _slowMo = null; yield break; } // someone else took over
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        t = 0f;
        float last = slow;
        while (t < ease)
        {
            if (!Mathf.Approximately(Time.timeScale, last)) { _slowMo = null; yield break; }
            t += Time.unscaledDeltaTime;
            last = Mathf.Lerp(slow, 1f, t / ease);
            Time.timeScale = last;
            yield return null;
        }

        if (Mathf.Approximately(Time.timeScale, last)) Time.timeScale = 1f;
        _slowMo = null;
    }
}
