using System;
using System.Reflection;
using HighSeas.Core;
using HighSeas.Pickups;
using HighSeas.V3;

namespace RoguePirates.V4.Legacy
{
    /// <summary>
    /// R-103 fix around V3's floor powerup tick: V3's Magnetic Tide (<c>PickupWorld.AttractLooseRewards</c>) flags the XP
    /// and gold barrels as homing, and a homing barrel that reaches the ship is released without paying; health barrels
    /// are never pulled. Kills drop barrels, so the powerup gave nothing. When a Magnetic Tide is collected this frame,
    /// every XP, gold and health barrel on the map is opened where it lies (V3's own barrel burst), and its crystals,
    /// coins and health fly to the ship and pay as usual. Detected from V3's powerup slots: a Magnetic Tide slot that
    /// went empty before its lifetime ran out was collected.
    /// </summary>
    [DeletedInPhase(9, "V4 PickupModule and PowerupModule")]
    public sealed class LegacyMagneticTide
    {
        static readonly FieldInfo AgentsField = typeof(V3FloorPowerupWorld).GetField("_agents", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly Type AgentType = AgentsField?.FieldType.GetElementType();
        static readonly FieldInfo ActiveField = AgentType?.GetField("active");
        static readonly FieldInfo KindField = AgentType?.GetField("kind");
        static readonly FieldInfo LifetimeField = AgentType?.GetField("lifetime");
        static readonly FieldInfo PoolField = typeof(PickupWorld).GetField("_pool", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly MethodInfo CollectMethod = typeof(PickupWorld).GetMethod("Collect", BindingFlags.Instance | BindingFlags.NonPublic);

        readonly Func<bool> _enabled;
        float[] _tideLifetime = Array.Empty<float>();

        public LegacyMagneticTide(Func<bool> enabled) => _enabled = enabled;

        /// <summary>The reflection seams exist (tested).</summary>
        public static bool SeamsFound => AgentsField != null && ActiveField != null && KindField != null && LifetimeField != null && PoolField != null && CollectMethod != null;

        /// <summary>Before V3's powerup tick: remember which slots hold a Magnetic Tide and how long each has left.</summary>
        public void BeforeTick(V3FloorPowerupWorld powerups)
        {
            if (powerups == null || !SeamsFound || !_enabled()) return;
            var agents = (Array)AgentsField.GetValue(powerups);
            if (_tideLifetime.Length != agents.Length) _tideLifetime = new float[agents.Length];
            for (var i = 0; i < agents.Length; i++)
            {
                var agent = agents.GetValue(i);
                var tide = (bool)ActiveField.GetValue(agent) && (V3FloorPowerupKind)KindField.GetValue(agent) == V3FloorPowerupKind.MagneticTide;
                _tideLifetime[i] = tide ? (float)LifetimeField.GetValue(agent) : 0f;
            }
        }

        /// <summary>After V3's powerup tick: if a Magnetic Tide was collected, open every barrel.</summary>
        public void AfterTick(V3FloorPowerupWorld powerups, PickupWorld pickups, float dt)
        {
            if (powerups == null || pickups == null || !SeamsFound || !_enabled()) return;
            var agents = (Array)AgentsField.GetValue(powerups);
            var collected = false;
            for (var i = 0; i < agents.Length && i < _tideLifetime.Length; i++)
            {
                if (_tideLifetime[i] <= dt) continue; // no tide here, or it expired this frame
                if (!(bool)ActiveField.GetValue(agents.GetValue(i))) collected = true;
            }
            if (collected) OpenAllBarrels(pickups);
        }

        /// <summary>
        /// Opens every XP, gold and health barrel (V3's Collect: a burst of flying pickups that home to the ship; it also
        /// clears the homing flag V3's Magnetic Tide set).
        /// </summary>
        public static int OpenAllBarrels(PickupWorld pickups)
        {
            var pool = (PickupAgent[])PoolField.GetValue(pickups);
            var opened = 0;
            for (var i = 0; i < pool.Length; i++)
            {
                if (!pool[i].active) continue;
                if (pool[i].type != PickupType.XpBarrel && pool[i].type != PickupType.GoldBarrel && pool[i].type != PickupType.Powerup) continue;
                CollectMethod.Invoke(pickups, new object[] { i });
                opened++;
            }
            return opened;
        }
    }
}
