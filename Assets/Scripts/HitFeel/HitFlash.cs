using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Briefly paints an enemy solid white when it is hit. Added to the enemy at runtime by HitFeel, so no
/// prefab needs editing. It swaps materials rather than editing properties, which works with whatever
/// shader the enemy uses (the dissolve shader graph included).
/// </summary>
public class HitFlash : MonoBehaviour
{
    private static Material _white;

    private Renderer[] _renderers;
    private Material[][] _originals;
    private Coroutine _routine;
    private bool _flashing;

    private static Material _ice;
    private Material[][] _thawed;
    private bool _frozen;

    public static HitFlash For(BaseEnemy enemy)
    {
        HitFlash flash = enemy.GetComponent<HitFlash>();
        return flash != null ? flash : enemy.gameObject.AddComponent<HitFlash>();
    }

    /// <summary>Paints the enemy a flat icy blue while it is frozen solid.</summary>
    public void SetFrozen(bool frozen)
    {
        if (frozen == _frozen) return;

        if (_white == null) _white = Resources.Load<Material>("HitFlashWhite");
        if (_white == null) return;

        if (_renderers == null) CacheRenderers();

        if (frozen)
        {
            if (_ice == null)
            {
                _ice = new Material(_white);
                Color blue = new Color(0.55f, 0.82f, 1f, 1f);
                _ice.SetColor("_BaseColor", blue);
                _ice.SetColor("_Color", blue);
            }

            if (_routine != null) { StopCoroutine(_routine); Restore(); } // cut a running white flash short

            _frozen = true;
            _thawed = new Material[_renderers.Length][];
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;

                _thawed[i] = r.sharedMaterials;
                var ice = new Material[_thawed[i].Length];
                for (int m = 0; m < ice.Length; m++) ice[m] = _ice;
                r.sharedMaterials = ice;
            }
        }
        else
        {
            _frozen = false;
            if (_thawed == null) return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null && _thawed[i] != null) _renderers[i].sharedMaterials = _thawed[i];
            }
            _thawed = null;
        }
    }

    public void Flash(float seconds)
    {
        if (_frozen) return; // frozen solid: the ice colour stays

        if (_white == null) _white = Resources.Load<Material>("HitFlashWhite");
        if (_white == null) return;

        if (_renderers == null) CacheRenderers();
        if (_renderers.Length == 0) return;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(FlashRoutine(seconds));
    }

    private void CacheRenderers()
    {
        var list = new List<Renderer>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            list.Add(r);
        }

        _renderers = list.ToArray();
        _originals = new Material[_renderers.Length][];
    }

    private IEnumerator FlashRoutine(float seconds)
    {
        Apply();
        yield return new WaitForSecondsRealtime(seconds); // real time: the flash must outlast the hit-pause
        Restore();
    }

    private void Apply()
    {
        if (_flashing) return;
        _flashing = true;

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            if (r == null) continue;

            _originals[i] = r.sharedMaterials;
            var white = new Material[_originals[i].Length];
            for (int m = 0; m < white.Length; m++) white[m] = _white;
            r.sharedMaterials = white;
        }
    }

    private void Restore()
    {
        _routine = null;
        if (!_flashing) return;
        _flashing = false;

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null && _originals[i] != null) _renderers[i].sharedMaterials = _originals[i];
        }
    }

    private void OnDisable()
    {
        Restore();
        SetFrozen(false);
    }
}
