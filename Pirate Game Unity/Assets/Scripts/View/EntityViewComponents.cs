using System.Collections.Generic;
using UnityEngine;

namespace PirateGame.View
{
    public class SnapshotInterpolator : MonoBehaviour
    {
        [SerializeField] Net.SnapshotBuffer buffer;

        public void PushSnapshot(Net.MatchSnapshot snap) => buffer?.Push(snap);

        public Vector3 InterpolatePosition(short x, short z, short prevX, short prevZ, float t)
        {
            var px = Mathf.Lerp(prevX, x, t);
            var pz = Mathf.Lerp(prevZ, z, t);
            return new Vector3(px, 0f, pz);
        }

        public float InterpolateHeading(ushort heading, ushort prevHeading, float t)
        {
            var a = prevHeading / 1024f * 360f;
            var b = heading / 1024f * 360f;
            return Mathf.LerpAngle(a, b, t);
        }

        public bool TryGetBlend(out Net.MatchSnapshot from, out Net.MatchSnapshot to, out float t)
        {
            from = null;
            to = null;
            t = 0f;
            return buffer != null && buffer.TryGetInterpolated(out from, out to, out t);
        }
    }

    public class EntityViewPool : MonoBehaviour
    {
        [SerializeField] GameObject shipPrefab;
        [SerializeField] GameObject enemyPrefab;
        [SerializeField] GameObject krakenPrefab;
        [SerializeField] GameObject enemyProjectilePrefab;
        [SerializeField] GameObject orbPrefab;
        [SerializeField] Transform stormRing;
        [SerializeField] float arenaRadius = 80f;

        const byte KrakenArchetype = 99;

        readonly Dictionary<ushort, GameObject> ships = new();
        readonly Dictionary<ushort, GameObject> enemies = new();
        readonly Dictionary<ushort, GameObject> enemyProjectiles = new();
        readonly Dictionary<ushort, GameObject> orbs = new();

        public void SyncFromSnapshot(
            Net.MatchSnapshot snap,
            byte localSlot,
            SnapshotInterpolator interp,
            float blendT = 1f,
            Net.MatchSnapshot prevSnap = null)
        {
            if (snap == null) return;
            prevSnap ??= snap;

            UpdateStorm(snap.stormRadiusPct);

            foreach (var p in snap.players)
            {
                if (p.slot == localSlot) continue;
                if (!ships.TryGetValue(p.slot, out var go))
                {
                    go = Instantiate(shipPrefab);
                    ships[p.slot] = go;
                }

                var prev = FindPlayer(prevSnap, p.slot);
                var pos = interp != null
                    ? interp.InterpolatePosition(p.x, p.z, prev.x, prev.z, blendT)
                    : new Vector3(p.x, 0f, p.z);
                var heading = interp != null
                    ? interp.InterpolateHeading(p.heading, prev.heading, blendT)
                    : p.heading / 1024f * 360f;

                go.transform.position = pos;
                go.transform.rotation = Quaternion.Euler(0f, heading, 0f);
                go.SetActive(!p.downed);
            }

            var seenEnemies = new HashSet<ushort>();
            foreach (var e in snap.enemies)
            {
                seenEnemies.Add(e.id);
                if (!enemies.TryGetValue(e.id, out var go))
                {
                    var prefab = e.archetype == KrakenArchetype && krakenPrefab != null
                        ? krakenPrefab
                        : enemyPrefab;
                    go = Instantiate(prefab);
                    enemies[e.id] = go;
                }
                go.transform.position = new Vector3(e.x, 0f, e.z);
            }

            foreach (var id in new List<ushort>(enemies.Keys))
            {
                if (!seenEnemies.Contains(id))
                {
                    Destroy(enemies[id]);
                    enemies.Remove(id);
                }
            }

            var seenProjectiles = new HashSet<ushort>();
            foreach (var proj in snap.enemyProjectiles)
            {
                seenProjectiles.Add(proj.id);
                if (!enemyProjectiles.TryGetValue(proj.id, out var go))
                {
                    go = Instantiate(enemyProjectilePrefab);
                    enemyProjectiles[proj.id] = go;
                }
                go.transform.position = new Vector3(proj.x, 0f, proj.z);
            }

            foreach (var id in new List<ushort>(enemyProjectiles.Keys))
            {
                if (!seenProjectiles.Contains(id))
                {
                    Destroy(enemyProjectiles[id]);
                    enemyProjectiles.Remove(id);
                }
            }

            if (orbPrefab != null)
            {
                var seenOrbs = new HashSet<ushort>();
                foreach (var orb in snap.orbs)
                {
                    seenOrbs.Add(orb.id);
                    if (!orbs.TryGetValue(orb.id, out var go))
                    {
                        go = Instantiate(orbPrefab);
                        orbs[orb.id] = go;
                    }
                    go.transform.position = new Vector3(orb.x, 0f, orb.z);
                }

                foreach (var id in new List<ushort>(orbs.Keys))
                {
                    if (!seenOrbs.Contains(id))
                    {
                        Destroy(orbs[id]);
                        orbs.Remove(id);
                    }
                }
            }
        }

        static Net.SnapshotPlayerState FindPlayer(Net.MatchSnapshot snap, byte slot)
        {
            if (snap?.players == null) return default;
            foreach (var p in snap.players)
                if (p.slot == slot) return p;
            return default;
        }

        void UpdateStorm(byte radiusPct)
        {
            if (stormRing == null) return;
            var scale = Mathf.Max(0.25f, radiusPct / 100f) * arenaRadius * 2f;
            stormRing.localScale = new Vector3(scale, stormRing.localScale.y, scale);
        }
    }

    public class GameEventPump : MonoBehaviour
    {
        public event System.Action<Net.GameEventState> OnEvent;
        uint lastProcessedTick;

        public void ProcessEvents(IEnumerable<Net.GameEventState> events)
        {
            if (events == null) return;
            foreach (var e in events)
            {
                if (e.tick <= lastProcessedTick) continue;
                lastProcessedTick = e.tick;
                OnEvent?.Invoke(e);
            }
        }

        public void ResetPump() => lastProcessedTick = 0;
    }

    public class HomingProjectileVfx : MonoBehaviour
    {
        [SerializeField] float speed = 24f;
        Vector3 target;
        bool hasTarget;

        public void Launch(Vector3 start, Vector3 hitTarget)
        {
            transform.position = start;
            target = hitTarget;
            hasTarget = true;
        }

        void Update()
        {
            if (!hasTarget) return;
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            if (Vector3.Distance(transform.position, target) < 0.2f)
                Destroy(gameObject);
        }
    }
}
