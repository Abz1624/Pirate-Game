using System;
using System.Collections.Generic;
using HighSeas.Combat;
using RoguePirates.V4.Config;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// Lockstep tests compare the V4 enemy world (which reads the config's combat blocks, M14) with V3's EnemyWorld (which
    /// reads V3's static registry). Real V4 runs used to write the config into that registry (LegacyCombatProfileOverride),
    /// so for a like-for-like comparison this writes the config's registered blocks into the registry and puts every
    /// touched profile back on Dispose.
    /// </summary>
    public sealed class V3RegistryFromConfig : IDisposable
    {
        readonly List<(EnemyCombatProfile live, EnemyCombatProfile snapshot)> _touched = new List<(EnemyCombatProfile, EnemyCombatProfile)>();

        public V3RegistryFromConfig(ConfigSetRuntime config)
        {
            if (config == null) return;
            foreach (var a in config.All<EnemyArchetypeConfig>()) Apply(a.enemyId, a.combat);
            foreach (var v in config.All<EnemyVariantConfig>()) Apply(v.variantId, v.combat);
        }

        void Apply(string id, ArchetypeCombat block)
        {
            if (block == null || !block.registered || !EnemyCombatProfileRegistry.TryGet(id, out var profile)) return;
            var snapshot = new EnemyCombatProfile();
            FieldMirror.CopyFields(profile, snapshot);
            _touched.Add((profile, snapshot));
            FieldMirror.CopyFields(block, profile);
        }

        public void Dispose()
        {
            foreach (var (live, snapshot) in _touched) FieldMirror.CopyFields(snapshot, live);
            _touched.Clear();
        }
    }
}
