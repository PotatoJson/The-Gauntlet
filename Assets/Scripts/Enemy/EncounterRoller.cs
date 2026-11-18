using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Rolls a fresh fight for a chamber each run. The spawn points placed in the scene still decide how many enemies the
/// room has, where they stand and how hard it is: the roll puts one enemy on every point again, shuffled, but picks a
/// new mix whose total cost stays close to what was placed, and splits big rooms into waves.
///
/// The mix leans on what the room was built with (each enemy type placed there is that much more likely) and can also
/// bring in any other type used elsewhere in the same level, so a level keeps its own cast of enemies.
/// </summary>
public static class EncounterRoller
{
    public struct Spawn
    {
        public GameObject Prefab;
        public Vector3 Position;
        public Quaternion Rotation;
    }

    private struct Kind
    {
        public GameObject Prefab;
        public int Cost;
        public int Placed;     // spawn points of this type in the room
        public bool InLevel;   // used by any room in this level
    }

    /// <summary>Every spawn point in the chamber, whatever enemy type it was placed for.</summary>
    public static List<Transform> SpawnPoints(ChamberData chamber)
    {
        var points = new List<Transform>();
        foreach (Transform container in Containers(chamber))
        {
            if (container == null) continue;
            if (container.childCount == 0) points.Add(container);
            else foreach (Transform point in container) points.Add(point);
        }
        return points;
    }

    /// <summary>The waves for this chamber: the first spawns at once, each next one when the last is nearly beaten.</summary>
    public static List<List<Spawn>> Roll(EnemySpawner spawner, ChamberData chamber, float budgetVariance, int enemiesPerWave, int maxWaves)
    {
        Kind[] kinds = Kinds(spawner, chamber);
        List<Transform> points = SpawnPoints(chamber);
        var waves = new List<List<Spawn>>();
        if (points.Count == 0) return waves;

        // As many enemies as the room was built with, and their total cost (the budget) close to what was placed.
        float budget = 0f;
        foreach (Kind kind in kinds) budget += kind.Placed * kind.Cost;
        budget = Mathf.Round(budget * Random.Range(1f - budgetVariance, 1f + budgetVariance));

        int minCost = int.MaxValue, maxCost = 0;
        foreach (Kind kind in kinds)
        {
            if (kind.Prefab == null || !kind.InLevel) continue;
            minCost = Mathf.Min(minCost, kind.Cost);
            maxCost = Mathf.Max(maxCost, kind.Cost);
        }
        if (maxCost == 0) return waves;

        var picked = new List<GameObject>();
        for (int left = points.Count; left > 0; left--)
        {
            // Whatever this one costs, the ones after it must still be able to land on the budget.
            float most = budget - (left - 1) * minCost;
            float least = budget - (left - 1) * maxCost;
            int choice = Pick(kinds, least, most);
            picked.Add(kinds[choice].Prefab);
            budget -= kinds[choice].Cost;
        }

        Shuffle(points);
        Shuffle(picked);

        // Big rooms come in waves of roughly equal size, never more than maxWaves of them.
        int waveCount = picked.Count > enemiesPerWave + 2
            ? Mathf.Min(maxWaves, Mathf.CeilToInt(picked.Count / (float)enemiesPerWave))
            : 1;
        for (int w = 0; w < waveCount; w++) waves.Add(new List<Spawn>());

        for (int i = 0; i < picked.Count; i++)
        {
            Transform point = points[i];
            waves[i * waveCount / picked.Count].Add(new Spawn { Prefab = picked[i], Position = OnNavMesh(point.position), Rotation = point.rotation });
        }
        return waves;
    }

    /// <summary>The nearest walkable point, so a moved or shared spawn point never leaves an enemy in a wall.</summary>
    public static Vector3 OnNavMesh(Vector3 at) =>
        NavMesh.SamplePosition(at, out NavMeshHit hit, 4f, NavMesh.AllAreas) ? hit.position : at;

    // A type costing between least and most, weighted by how many of it the room was built with, plus one for any
    // type this level uses at all. If none fits (the budget was rolled low), the one whose cost comes closest.
    private static int Pick(Kind[] kinds, float least, float most)
    {
        float total = 0f;
        foreach (Kind kind in kinds) total += Weight(kind, least, most);

        if (total > 0f)
        {
            float roll = Random.value * total;
            for (int i = 0; i < kinds.Length; i++)
            {
                roll -= Weight(kinds[i], least, most);
                if (roll < 0f) return i;
            }
        }

        int closest = -1;
        float gap = float.MaxValue;
        for (int i = 0; i < kinds.Length; i++)
        {
            if (kinds[i].Prefab == null || !kinds[i].InLevel) continue;
            float miss = Mathf.Max(least - kinds[i].Cost, kinds[i].Cost - most, 0f);
            if (miss < gap) { gap = miss; closest = i; }
        }
        return closest;
    }

    private static float Weight(Kind kind, float least, float most)
    {
        if (kind.Prefab == null || !kind.InLevel || kind.Cost < least || kind.Cost > most) return 0f;
        return kind.Placed + 1f;
    }

    private static Kind[] Kinds(EnemySpawner spawner, ChamberData chamber)
    {
        // Cost is roughly how much of a fight each one is next to a grunt.
        var kinds = new[]
        {
            new Kind { Prefab = spawner.gruntPrefab, Cost = 1, Placed = Count(chamber.gruntSpawnPoints) },
            new Kind { Prefab = spawner.elitePrefab, Cost = 3, Placed = Count(chamber.eliteSpawnPoints) },
            new Kind { Prefab = spawner.peeperPrefab, Cost = 1, Placed = Count(chamber.peeperSpawnPoints) },
            new Kind { Prefab = spawner.casterPrefab, Cost = 2, Placed = Count(chamber.casterSpawnPoints) },
            new Kind { Prefab = spawner.shieldPrefab, Cost = 2, Placed = Count(chamber.shieldSpawnPoints) },
            new Kind { Prefab = spawner.spinningPrefab, Cost = 2, Placed = Count(chamber.spinningSpawnPoints) },
            new Kind { Prefab = spawner.biterPrefab, Cost = 1, Placed = Count(chamber.biterSpawnPoints) },
        };

        foreach (ChamberData room in spawner.chambers)
        {
            Transform[] containers = Containers(room);
            for (int i = 0; i < kinds.Length; i++) kinds[i].InLevel |= Count(containers[i]) > 0;
        }
        return kinds;
    }

    // In the same order as the kinds above.
    private static Transform[] Containers(ChamberData c) => new[]
    {
        c.gruntSpawnPoints, c.eliteSpawnPoints, c.peeperSpawnPoints, c.casterSpawnPoints,
        c.shieldSpawnPoints, c.spinningSpawnPoints, c.biterSpawnPoints,
    };

    private static int Count(Transform container) => container == null ? 0 : Mathf.Max(1, container.childCount);

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
