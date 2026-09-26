using System.Collections.Generic;
using System.Linq;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.UI;
using HighSeas.Weapons;

namespace RoguePirates.V4.Legacy
{
    /// <summary>
    /// Loadout changes for the console and Module Lab (M5). Until M9 the loadout lives in the legacy level system and the
    /// switched-off WeaponController record, so both are written the way V3's playtest tools wrote them; the V4 Weapon
    /// Manager picks the change up on its next tick.
    /// </summary>
    [DeletedInPhase(9, "V4 progression owns the loadout")]
    public static class LegacyWeaponLab
    {
        static WeaponController Record(V4Game game) => game?.Shell?.Bridge?.Weapons;

        public static IReadOnlyList<string> Loadout(V4Game game) =>
            Record(game)?.Weapons.Select(w => w.def.id).ToList() ?? new List<string>();

        public static int SlotCap(V4Game game) => System.Math.Max(Record(game)?.Weapons.Count ?? 0, 6);

        public static void SetLoadout(V4Game game, IReadOnlyList<string> weaponIds)
        {
            var record = Record(game);
            if (record == null) return;
            record.EnsureWeaponSlotCapacity(weaponIds.Count);
            record.SetWeaponLoadout(weaponIds);
            // V3's SetWeaponLoadout cleared the controller's kegs, strikes, skiffs and pending broadsides; V4 keeps them.
            if (game.Context != null && game.Context.TryGet<RoguePirates.V4.Weapons.IWeaponManager>(out var weapons)) weapons.ClearInFlight();
            LevelSystem.Instance?.SetPlaytestWeaponLoadout(weaponIds);
        }

        public static void Add(V4Game game, string weaponId)
        {
            var ids = Loadout(game).ToList();
            if (ids.Contains(weaponId)) return;
            ids.Add(weaponId);
            SetLoadout(game, ids);
        }

        public static void Remove(V4Game game, int index)
        {
            var ids = Loadout(game).ToList();
            if (index < 0 || index >= ids.Count) return;
            ids.RemoveAt(index);
            SetLoadout(game, ids);
        }

        public static void Solo(V4Game game, string weaponId) => SetLoadout(game, new[] { weaponId });

        /// <summary>One level up with an Uncommon package (V3 UpgradeWeapon, what a weapon offer does to the record).</summary>
        public static void RankUp(V4Game game, int index)
        {
            var record = Record(game);
            if (record == null || index < 0 || index >= record.Weapons.Count) return;
            record.UpgradeWeapon(index, Rarity.Uncommon);
        }

        /// <summary>Evolves a weapon (V3 WeaponEvolutionRuntime, as the level system does when the evolution is taken).</summary>
        public static void Evolve(string weaponId, string evolutionId) => WeaponEvolutionRuntime.Apply(weaponId, evolutionId);

        public static bool IsEvolved(string weaponId) => WeaponEvolutionRuntime.IsEvolved(weaponId);
    }
}
