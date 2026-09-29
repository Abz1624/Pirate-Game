using System.Collections.Generic;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.POI;
using RoguePirates.V4.Foundation.World;
using RoguePirates.V4.Stage;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Stage
{
    /// <summary>
    /// Chest and landmark markers (M14.12; port of POIManager.CreateMarker and POIInteractable's rings, was
    /// LegacyPoiMarkers): the prefab or placeholder per POI type, chests floating and glowing on the water, the Signal
    /// Buoy's pulsing charge disc and the Beacon's telegraph ring. Every marker and ring is made when the run's layout is
    /// bound (one per POI, plus the Rare Treasure Chest look at each Powder Cache), then shown, hidden or retired from
    /// <see cref="IPoiMarkerView"/> each frame, so nothing is created while the run plays. Markers only draw: interaction
    /// range is measured to the POI's own position (R-95).
    /// </summary>
    public sealed class PoiMarkerPresenter : MonoBehaviour
    {
        sealed class Marker
        {
            public GameObject Main, Alternate; // Alternate: the Powder Cache's Rare Treasure Chest look
            public Transform ChargeRing, TelegraphRing;
            public Renderer ChargeRenderer;
        }

        IPoiMarkerView _view;
        DemoJuiceSettings _juice;
        int _boundVersion = -1;
        readonly List<Marker> _markers = new List<Marker>();

        public int MarkerCount => _markers.Count;

        public void Bind(IPoiMarkerView view)
        {
            _view = view;
            _juice = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            Rebuild();
        }

        /// <summary>The marker object drawn for a POI (null when none is showing).</summary>
        public GameObject Shown(int index)
        {
            if (index < 0 || index >= _markers.Count) return null;
            var m = _markers[index];
            if (m.Alternate != null && m.Alternate.activeSelf) return m.Alternate;
            return m.Main != null && m.Main.activeSelf ? m.Main : null;
        }

        public bool ChargeRingShown(int index) => index >= 0 && index < _markers.Count && _markers[index].ChargeRing != null && _markers[index].ChargeRing.gameObject.activeSelf;
        public bool TelegraphRingShown(int index) => index >= 0 && index < _markers.Count && _markers[index].TelegraphRing != null && _markers[index].TelegraphRing.gameObject.activeSelf;

        void Rebuild()
        {
            foreach (var m in _markers)
            {
                if (m.Main != null) Destroy(m.Main);
                if (m.Alternate != null) Destroy(m.Alternate);
            }
            _markers.Clear();
            if (_view == null) return;
            _boundVersion = _view.LayoutVersion;
            for (var i = 0; i < _view.PoiCount; i++)
            {
                var type = _view.PoiType(i);
                var position = _view.PoiPosition(i);
                var m = new Marker { Main = CreateMarker(type, position) };
                if (type == POIType.MerchantSkiff) m.Alternate = CreateMarker(POIType.SalvageChest, position);
                if (type == POIType.TrialBuoy) m.ChargeRing = CreateChargeRing(m.Main.transform, out m.ChargeRenderer);
                if (type == POIType.BossBeacon) m.TelegraphRing = CreateTelegraphRing(m.Main.transform);
                m.Main.SetActive(false);
                if (m.Alternate != null) m.Alternate.SetActive(false);
                _markers.Add(m);
            }
        }

        void LateUpdate()
        {
            if (_view == null) return;
            if (_view.LayoutVersion != _boundVersion) Rebuild();
            for (var i = 0; i < _markers.Count; i++) Sync(i, _markers[i]);
        }

        void Sync(int i, Marker m)
        {
            var spawned = _view.IsMarkerSpawned(i) && !_view.IsConsumed(i);
            var alternate = spawned && m.Alternate != null && _view.VisualType(i) != _view.PoiType(i);
            var shown = alternate ? m.Alternate : m.Main;
            var other = alternate ? m.Main : m.Alternate;
            if (other != null && other.activeSelf) other.SetActive(false);
            if (shown == null) return;
            if (shown.activeSelf != spawned) shown.SetActive(spawned);
            if (!spawned) return;

            // V3 SetMarkerHidden hid the marker's children (the rings with them).
            var hidden = _view.IsHidden(i);
            for (var c = 0; c < shown.transform.childCount; c++)
            {
                var child = shown.transform.GetChild(c);
                var ring = child == m.ChargeRing || child == m.TelegraphRing;
                if (!ring && child.gameObject.activeSelf == hidden) child.gameObject.SetActive(!hidden);
            }

            if (m.ChargeRing != null)
            {
                var show = !hidden && _view.ChargeRingShown(i);
                if (m.ChargeRing.gameObject.activeSelf != show) m.ChargeRing.gameObject.SetActive(show);
                if (show)
                {
                    // POIInteractable.UpdateChargeRing: twice the circle radius, pulsing, brighter as it charges.
                    var size = _view.ChargeRingRadius * 2f;
                    var pulse = 1f + Mathf.Sin(Time.time * 6f) * 0.04f;
                    m.ChargeRing.localScale = new Vector3(size * pulse, 0.04f, size * pulse);
                    if (m.ChargeRenderer != null)
                        m.ChargeRenderer.material.color = new Color(0.2f, 0.75f, 1f, Mathf.Lerp(0.2f, 0.75f, _view.SignalBuoyCharge01(i)));
                }
            }

            if (m.TelegraphRing != null)
            {
                var show = !hidden && _view.TelegraphRingShown(i);
                if (m.TelegraphRing.gameObject.activeSelf != show) m.TelegraphRing.gameObject.SetActive(show);
                if (show)
                {
                    // POIInteractable.PulseTelegraph.
                    var pulse = 1f + Mathf.Sin(Time.time * 24f) * 0.08f;
                    var baseScale = _juice != null ? _juice.bossTelegraphScale : 2.4f;
                    m.TelegraphRing.localScale = new Vector3(baseScale * pulse, 0.05f, baseScale * pulse);
                }
            }
        }

        Transform CreateChargeRing(Transform parent, out Renderer renderer)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "SignalBuoyChargeRing";
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = Vector3.up * 0.05f;
            renderer = ring.GetComponent<Renderer>();
            renderer.material.color = new Color(0.2f, 0.75f, 1f, 0.45f);
            StripColliders(ring);
            ring.SetActive(false);
            return ring.transform;
        }

        Transform CreateTelegraphRing(Transform parent)
        {
            var scale = _juice != null ? _juice.bossTelegraphScale : 2.4f;
            GameObject ring;
            if (_juice != null && _juice.bossTelegraphRingPrefab != null)
                ring = Instantiate(_juice.bossTelegraphRingPrefab, parent.position, Quaternion.identity, parent);
            else
            {
                ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.transform.SetParent(parent, false);
                ring.transform.localPosition = Vector3.up * 0.05f;
                ring.GetComponent<Renderer>().material.color = new Color(1f, 0.2f, 0.9f, 0.65f);
                StripColliders(ring);
            }
            ring.name = "BossTelegraph";
            ring.transform.localScale = new Vector3(scale, 0.05f, scale);
            ring.SetActive(false);
            return ring.transform;
        }

        // ---- POIManager.CreateMarker, as V3 ----------------------------------------------------------------------

        GameObject CreateMarker(POIType type, Vector3 position)
        {
            var root = transform;
            var spawnPos = position + Vector3.up * 0.15f;
            var prefab = GetPrefab(type);
            var placeholders = _view.UsePlaceholderVisuals;
            if (prefab != null && (!placeholders || type == POIType.SalvageChest))
                return CreatePrefabMarker(type, prefab, spawnPos, root);

            if (placeholders)
            {
                var marker = PlaceholderFactory.CreatePoiMarker(type, spawnPos, root);
                if (type == POIType.SalvageChest) SalvageChestWaterFloat.Attach(marker, _juice);
                return marker;
            }

            if (prefab != null) return CreatePrefabMarker(type, prefab, spawnPos, root);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = type.ToString();
            go.transform.SetParent(root, false);
            go.transform.position = spawnPos + Vector3.up * 0.35f;
            go.transform.localScale = FallbackScale(type);
            go.GetComponent<Renderer>().material.color = type switch
            {
                POIType.SalvageChest => new Color(0.8f, 0.6f, 0.2f),
                POIType.LighthouseOfFortune => Color.white,
                POIType.TrialBuoy => Color.red,
                POIType.BossBeacon => Color.magenta,
                _ => Color.gray
            };
            StripColliders(go);
            if (type == POIType.SalvageChest) SalvageChestWaterFloat.Attach(go, _juice);
            return go;
        }

        GameObject CreatePrefabMarker(POIType type, GameObject prefab, Vector3 spawnPos, Transform root)
        {
            var go = Instantiate(prefab, spawnPos, Quaternion.identity, root);
            go.name = type.ToString();
            go.transform.localScale *= Scale(type);
            if (type == POIType.SalvageChest)
            {
                SalvageChestWaterFloat.Attach(go, _juice);
                SalvageChestTreasureGlow.Attach(go, _juice);
            }
            StripColliders(go);
            return go;
        }

        static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        }

        GameObject GetPrefab(POIType type)
        {
            if (_juice == null) return null;
            return type switch
            {
                POIType.SalvageChest => _juice.salvageChestPrefab,
                POIType.LighthouseOfFortune => _juice.lighthousePrefab,
                POIType.TrialBuoy => _juice.trialBuoyPrefab,
                POIType.BossBeacon => _juice.bossBeaconPrefab,
                _ => null
            };
        }

        float Scale(POIType type)
        {
            if (_juice == null) return 1f;
            return type switch
            {
                POIType.SalvageChest => _juice.salvageChestScale,
                POIType.LighthouseOfFortune => _juice.lighthouseScale,
                POIType.TrialBuoy => _juice.trialBuoyScale,
                POIType.BossBeacon => _juice.bossBeaconScale,
                _ => 1f
            };
        }

        static Vector3 FallbackScale(POIType type) => type switch
        {
            POIType.SalvageChest => new Vector3(1.6f, 0.8f, 1.6f),
            POIType.LighthouseOfFortune => new Vector3(1.2f, 2f, 1.2f),
            POIType.TrialBuoy => new Vector3(1f, 1.2f, 1f),
            POIType.BossBeacon => new Vector3(2.2f, 0.2f, 2.2f),
            _ => new Vector3(2f, 1f, 2f)
        };
    }
}
