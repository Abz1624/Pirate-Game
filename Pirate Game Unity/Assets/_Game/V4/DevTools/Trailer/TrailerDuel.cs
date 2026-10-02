using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Trailer
{
    /// <summary>
    /// A scripted broadside exchange for the trailer's Arena and Hunt shots: muzzle flash, a cannonball arc and an
    /// explosion or splash, alternating between the player and the stand-in rivals. Purely visual.
    /// </summary>
    public sealed class TrailerDuel : MonoBehaviour
    {
        struct Shot { public GameObject Ball; public Vector3 From, To; public float T, Duration; public bool Hit; }

        List<GameObject> _rivals;
        float _interval;
        float _next;
        int _turn;
        GameObject _muzzle, _ball, _explosion, _splash;
        readonly List<Shot> _shots = new List<Shot>();

        public void Begin(List<GameObject> rivals, float interval, GameObject muzzle, GameObject ball, GameObject explosion, GameObject splash)
        {
            _rivals = rivals;
            _interval = Mathf.Max(0.15f, interval);
            _muzzle = muzzle; _ball = ball; _explosion = explosion; _splash = splash;
            _next = 0f;
        }

        void Spawn(GameObject prefab, Vector3 at, Quaternion rot, float life)
        {
            if (prefab == null) return;
            var go = Instantiate(prefab, at, rot);
            Destroy(go, life);
        }

        void Fire(Vector3 from, Vector3 to)
        {
            var dir = to - from; dir.y = 0f;
            var rot = dir.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(dir) : Quaternion.identity;
            Spawn(_muzzle, from + Vector3.up * 1.2f, rot, 2f);
            var ball = _ball != null ? Instantiate(_ball, from, rot) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            if (_ball == null) { ball.transform.localScale = Vector3.one * 0.5f; Destroy(ball.GetComponent<Collider>()); }
            var hit = Random.value < 0.7f;
            var miss = hit ? Vector3.zero : new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
            _shots.Add(new Shot { Ball = ball, From = from + Vector3.up * 1.2f, To = to + miss + Vector3.up * 0.8f, T = 0f, Duration = Mathf.Clamp((to - from).magnitude / 26f, 0.35f, 1.1f), Hit = hit });
        }

        void Update()
        {
            var dt = Time.deltaTime;
            _next -= dt;
            if (_next <= 0f && _rivals != null && _rivals.Count > 0)
            {
                _next = _interval;
                var rival = _rivals[_turn % _rivals.Count];
                if (rival != null)
                {
                    if (_turn % 2 == 0) Fire(rival.transform.position, transform.position);
                    else Fire(transform.position, rival.transform.position);
                }
                _turn++;
            }
            for (var i = _shots.Count - 1; i >= 0; i--)
            {
                var s = _shots[i];
                s.T += dt;
                var k = Mathf.Clamp01(s.T / s.Duration);
                var pos = Vector3.Lerp(s.From, s.To, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 3f);
                if (s.Ball != null) s.Ball.transform.position = pos;
                if (k >= 1f)
                {
                    if (s.Ball != null) Destroy(s.Ball);
                    Spawn(s.Hit ? _explosion : _splash, s.To, Quaternion.identity, 3f);
                    _shots.RemoveAt(i);
                    continue;
                }
                _shots[i] = s;
            }
        }

        void OnDestroy()
        {
            foreach (var s in _shots) if (s.Ball != null) Destroy(s.Ball);
        }
    }
}
