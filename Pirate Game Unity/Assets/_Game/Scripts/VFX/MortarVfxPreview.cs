using HighSeas.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HighSeas.VFX
{
    /// <summary>
    /// Play-mode preview of the full mortar VFX chain (muzzle → shell arc → telegraph → impact + splash)
    /// using the same WeaponJuiceController code the game uses. Drop in any scene, assign Ship + Target, press Play.
    /// Space / left click = fire. Auto Fire repeats.
    /// </summary>
    public sealed class MortarVfxPreview : MonoBehaviour
    {
        [SerializeField] Transform ship;
        [SerializeField] Transform target;
        [SerializeField] float blastRadius = 4f;
        [SerializeField] float flightSeconds = 1.1f;
        [SerializeField] bool autoFire = true;
        [SerializeField] float autoFireInterval = 2.5f;
        [Tooltip("Randomly offset the landing point around Target each shot.")]
        [SerializeField] float targetScatter = 3f;

        WeaponJuiceController _juice;
        JuiceVfxBroker _broker;
        float _nextAutoFire;
        readonly System.Collections.Generic.List<PendingImpact> _pending = new();

        struct PendingImpact
        {
            public Vector3 position;
            public float time;
        }

        void Start()
        {
            var settings = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            if (settings != null) flightSeconds = settings.mortarFlightSeconds;
            _broker = JuiceVfxBroker.Instance;
            if (_broker == null)
            {
                _broker = new GameObject("JuiceVfxBroker (Preview)").AddComponent<JuiceVfxBroker>();
                _broker.Configure(settings, _broker.transform);
            }

            _juice = FindFirstObjectByType<WeaponJuiceController>();
            if (_juice == null)
            {
                _juice = new GameObject("WeaponJuiceController (Preview)").AddComponent<WeaponJuiceController>();
                _juice.Configure(settings, _juice.transform);
            }

            _nextAutoFire = Time.time + 0.5f;
        }

        void Update()
        {
            var fire = false;
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) fire = true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) fire = true;
            if (autoFire && Time.time >= _nextAutoFire) fire = true;
            if (fire) Fire();

            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < _pending[i].time) continue;
                _juice.OnMortarImpact(_pending[i].position, blastRadius, null);
                _broker?.PlayWaterSplash(_pending[i].position);
                _pending.RemoveAt(i);
            }
        }

        [ContextMenu("Fire")]
        public void Fire()
        {
            if (!Application.isPlaying || ship == null || target == null || _juice == null) return;
            _nextAutoFire = Time.time + autoFireInterval;

            var offset = Random.insideUnitCircle * targetScatter;
            var landing = target.position + new Vector3(offset.x, 0f, offset.y);
            landing.y = 0.2f;

            _juice.OnMortarTelegraph(landing, blastRadius);
            _juice.OnMortarLaunch(MortarShellFlight.FindLaunchPoint(ship), landing, flightSeconds, blastRadius);
            _pending.Add(new PendingImpact { position = landing, time = Time.time + flightSeconds });
        }

        void OnDrawGizmos()
        {
            if (ship == null || target == null) return;
            var from = MortarShellFlight.FindLaunchPoint(ship);
            var to = target.position;
            Gizmos.color = new Color(1f, 0.55f, 0.2f, 1f);
            Gizmos.DrawWireSphere(from, 0.4f);
            var flat = to - from;
            flat.y = 0f;
            var apex = Mathf.Clamp(flat.magnitude * 0.55f, 6f, 20f);
            var prev = from;
            for (var i = 1; i <= 24; i++)
            {
                var u = i / 24f;
                var p = Vector3.Lerp(from, to, u);
                p.y += 4f * apex * u * (1f - u);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
            Gizmos.DrawWireSphere(to, blastRadius);
        }
    }
}
