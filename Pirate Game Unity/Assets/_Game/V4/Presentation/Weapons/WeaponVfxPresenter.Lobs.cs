using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Weapons
{
    /// <summary>
    /// Hackathon art pass (12 Oct): a predicted-area shell of a weapon with a lob model in the juice settings
    /// (<c>weaponArtModels[].lobModel</c>: the art team's ink pot for the Squid Ink Pots) is drawn as that model thrown
    /// from the ship's rail in an arc, landing on the shell's ring exactly when the shell lands (the ring's own seconds),
    /// then going back to its pool. The shell is the weapon's that aimed this frame (as the Cursed Doubloons' coins are).
    /// Presentation only: it reads the effect calls the simulation already makes; no random numbers.
    /// </summary>
    public sealed partial class WeaponVfxPresenter
    {
        const string KindLob = "lob";

        struct Lob
        {
            public View View;
            public Vector3 From, To;
            public float T, Flight, Arc;
        }

        readonly List<Lob> _lobs = new List<Lob>();
        /// <summary>Lobbed models in flight (tests).</summary>
        public int LobCount => _lobs.Count;
        /// <summary>A lobbed model's position (tests).</summary>
        public Vector3 LobPosition(int index) => _lobs[index].View.Go != null ? _lobs[index].View.Go.transform.position : Vector3.zero;

        Foundation.Data.ArtWeaponModel AimedLobArt()
        {
            if (_settings == null || _aimedFrame != Time.frameCount) return null;
            var art = _settings.WeaponArt(_aimedWeapon);
            return art != null && art.lobModel != null ? art : null;
        }

        void PrewarmLob(string weaponId)
        {
            var art = _settings != null ? _settings.WeaponArt(weaponId) : null;
            if (art == null || art.lobModel == null || _pools == null) return;
            EnsurePool(KindLob, art.lobModel, Mathf.Max(1, art.lobPrewarm));
        }

        /// <summary>Throws the aimed weapon's lob model at a shell's landing point (false: no lob model).</summary>
        bool TryLob(Vector3 target, float seconds)
        {
            var art = AimedLobArt();
            if (art == null) return false;
            var from = _ship != null && _ship.Exists ? _ship.Position + Vector3.up * 1.6f : target;
            var go = Spawn(KindLob, art.lobModel, from, Quaternion.identity, art.lobScale, out var pooled);
            if (go == null) return false;
            var prep = Prepare(go);
            if (!prep.Done) { StripPhysics(go); prep.Done = true; }
            foreach (var trail in go.GetComponentsInChildren<TrailRenderer>(true)) trail.Clear();
            _lobs.Add(new Lob { View = new View { Go = go, Pooled = pooled }, From = from, To = target, Flight = Mathf.Max(0.15f, seconds), Arc = art.lobArcHeight });
            return true;
        }

        void TickLobs(float dt)
        {
            for (var i = _lobs.Count - 1; i >= 0; i--)
            {
                var lob = _lobs[i];
                lob.T += dt;
                if (lob.T >= lob.Flight || lob.View.Go == null)
                {
                    ReleaseView(lob.View);
                    _lobs.RemoveAt(i);
                    continue;
                }
                var k = lob.T / lob.Flight;
                var position = Vector3.Lerp(lob.From, lob.To, k) + Vector3.up * (lob.Arc * 4f * k * (1f - k));
                lob.View.Go.transform.position = position;
                _lobs[i] = lob;
            }
        }

        void ClearLobs()
        {
            foreach (var lob in _lobs) ReleaseView(lob.View);
            _lobs.Clear();
        }
    }
}
