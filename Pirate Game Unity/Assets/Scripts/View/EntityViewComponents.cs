using System.Collections.Generic;
using UnityEngine;

namespace PirateGame.View
{
    public class SnapshotInterpolator : MonoBehaviour
    {
        [SerializeField] float interpolationDelay = 0.1f;

        Net.MatchSnapshot previous;
        Net.MatchSnapshot current;
        float blend;

        public void PushSnapshot(Net.MatchSnapshot snap)
        {
            previous = current ?? snap;
            current = snap;
            blend = 0f;
        }

        public Vector3 InterpolatePosition(short x, short z, short prevX, short prevZ)
        {
            blend = Mathf.Clamp01(blend + Time.deltaTime / interpolationDelay);
            var px = Mathf.Lerp(prevX, x, blend);
            var pz = Mathf.Lerp(prevZ, z, blend);
            return new Vector3(px, 0f, pz);
        }
    }

    public class EntityViewPool : MonoBehaviour
    {
        [SerializeField] GameObject shipPrefab;
        [SerializeField] GameObject enemyPrefab;
        [SerializeField] GameObject enemyProjectilePrefab;

        readonly Dictionary<ushort, GameObject> ships = new();
        readonly Dictionary<ushort, GameObject> enemies = new();
        readonly Dictionary<ushort, GameObject> enemyProjectiles = new();

        public void SyncFromSnapshot(Net.MatchSnapshot snap, byte localSlot, SnapshotInterpolator interp)
        {
            foreach (var p in snap.players)
            {
                if (!ships.TryGetValue(p.slot, out var go))
                {
                    go = Instantiate(shipPrefab);
                    ships[p.slot] = go;
                }
                go.transform.position = new Vector3(p.x, 0f, p.z);
                go.transform.rotation = Quaternion.Euler(0f, p.heading / 1024f * 360f, 0f);
            }

            var seenEnemies = new HashSet<ushort>();
            foreach (var e in snap.enemies)
            {
                seenEnemies.Add(e.id);
                if (!enemies.TryGetValue(e.id, out var go))
                {
                    go = Instantiate(enemyPrefab);
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

            foreach (var proj in snap.enemyProjectiles)
            {
                if (!enemyProjectiles.TryGetValue(proj.id, out var go))
                {
                    go = Instantiate(enemyProjectilePrefab);
                    enemyProjectiles[proj.id] = go;
                }
                go.transform.position = new Vector3(proj.x, 0f, proj.z);
            }
        }
    }

    public class GameEventPump : MonoBehaviour
    {
        public event System.Action<Net.GameEventState> OnEvent;

        public void ProcessEvents(IEnumerable<Net.GameEventState> events)
        {
            foreach (var e in events)
            {
                OnEvent?.Invoke(e);
                switch (e.kind)
                {
                    case Net.GameEventKind.Fire:
                        SpawnOwnProjectileVfx(e);
                        break;
                    case Net.GameEventKind.Hit:
                        PlayHitVfx(e);
                        break;
                    case Net.GameEventKind.Death:
                        PlayDeathVfx(e);
                        break;
                    case Net.GameEventKind.LevelUp:
                        PlayLevelUpVfx(e);
                        break;
                    case Net.GameEventKind.Evolution:
                        PlayEvolutionVfx(e);
                        break;
                    case Net.GameEventKind.Revive:
                        PlayReviveVfx(e);
                        break;
                }
            }
        }

        void SpawnOwnProjectileVfx(Net.GameEventState e) { }
        void PlayHitVfx(Net.GameEventState e) { }
        void PlayDeathVfx(Net.GameEventState e) { }
        void PlayLevelUpVfx(Net.GameEventState e) { }
        void PlayEvolutionVfx(Net.GameEventState e) { }
        void PlayReviveVfx(Net.GameEventState e) { }
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
