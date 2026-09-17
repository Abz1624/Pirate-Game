using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.POI;
using HighSeas.Player;
using HighSeas.UI;
using HighSeas.V3;
using HighSeas.Weapons;
using UnityEngine;

namespace HighSeas.V3.MultiShip
{
    public static class V3MultiShipRunSetup
    {
        public static void ApplyAfterRunStart(
            V3RunSessionKind kind,
            PlayerShipController humanPlayer,
            GameDatabase database,
            string startingWeaponId,
            float spacing)
        {
            if (kind == V3RunSessionKind.SinglePlayer || humanPlayer == null || database == null)
                return;

            var systemsWeapon = V3MultiShipWeaponLoadouts.FindHumanWeaponController(humanPlayer);
            if (systemsWeapon != null)
                systemsWeapon.BindOwner(humanPlayer);

            var combatant = humanPlayer.GetComponent<V3ShipCombatant>();
            if (combatant == null)
                combatant = humanPlayer.gameObject.AddComponent<V3ShipCombatant>();
            combatant.Configure(V3ShipTeam.Human, true);

            switch (kind)
            {
                case V3RunSessionKind.PvP:
                    ConfigurePvP(humanPlayer, database, systemsWeapon, spacing);
                    break;
                case V3RunSessionKind.CoOp:
                    ConfigureCoOp(humanPlayer, database, startingWeaponId, spacing);
                    break;
            }

            var label = kind == V3RunSessionKind.PvP ? "PvP duel" : "Co-op simulation";
            V3ToastHud.Instance?.Show($"{label} ready.\nYou + bots — press F8 for playtest keys.", 5f);
        }

        static void ConfigurePvP(
            PlayerShipController humanPlayer,
            GameDatabase database,
            WeaponController humanWeapons,
            float spacing)
        {
            V3PvPRunDirector.ResetState();
            DisableAmbientPve();

            var fullLoadout = V3PvPWeaponLoadout.GetAllPlaytestWeapons(database);

            if (humanWeapons != null && fullLoadout.Length > 0)
            {
                humanWeapons.ApplyCombatExperiment(CombatExperimentContext.Active);
                humanWeapons.EnsureWeaponSlotCapacity(fullLoadout.Length);
                humanWeapons.Initialize(database, fullLoadout[0]);
                humanWeapons.SetWeaponLoadout(fullLoadout);
            }

            LevelSystem.Instance?.SetPlaytestWeaponLoadout(fullLoadout);

            var forward = humanPlayer.MovementForward;
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var humanPos = V3MultiShipSpawnUtil.ResolveSafeSpawn(
                humanPlayer.Position - right * (spacing * 0.5f));
            humanPlayer.SetWorldPosition(humanPos);
            var spawn = V3MultiShipSpawnUtil.ResolveSafeSpawn(humanPos + right * spacing);

            var rival = PlayerShipFactory.SpawnFromTemplate(
                humanPlayer,
                "RivalShip_Bot",
                spawn,
                Quaternion.LookRotation(-forward, Vector3.up),
                V3ShipTeam.RivalBot,
                localHuman: false,
                database,
                fullLoadout.Length > 0 ? fullLoadout[0] : "deck_cannons",
                humanPlayer,
                fullLoadout);

            V3PvPRunDirector.Ensure(rival);
            V3ShipNameLabels.TagShips(humanPlayer, rival);

            V3ToastHud.Instance?.Show(
                $"PvP duel — defeat the rival captain.\nFull arsenal equipped ({fullLoadout.Length} weapons each).",
                5f);
        }

        static void ConfigureCoOp(
            PlayerShipController humanPlayer,
            GameDatabase database,
            string startingWeaponId,
            float spacing)
        {
            var forward = humanPlayer.MovementForward;
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var back = -forward;

            var wingA = V3MultiShipSpawnUtil.ResolveSafeSpawn(
                humanPlayer.Position + right * spacing * 0.85f + back * (spacing * 0.35f));
            var wingB = V3MultiShipSpawnUtil.ResolveSafeSpawn(
                humanPlayer.Position - right * spacing * 0.85f + back * (spacing * 0.35f));

            var allyA = PlayerShipFactory.SpawnFromTemplate(
                humanPlayer, "AllyShip_A", wingA, humanPlayer.transform.rotation,
                V3ShipTeam.AllyBot, false, database, startingWeaponId, humanPlayer);
            var allyB = PlayerShipFactory.SpawnFromTemplate(
                humanPlayer, "AllyShip_B", wingB, humanPlayer.transform.rotation,
                V3ShipTeam.AllyBot, false, database, startingWeaponId, humanPlayer);

            V3ShipNameLabels.TagShips(humanPlayer, allyA, allyB);
        }

        static void DisableAmbientPve()
        {
            var director = Object.FindFirstObjectByType<SpawnDirector>();
            if (director != null)
                director.enabled = false;

            var beats = Object.FindFirstObjectByType<V3PressureBeatDirector>();
            if (beats != null)
                beats.enabled = false;

            var overtime = Object.FindFirstObjectByType<V3OvertimeDirector>();
            if (overtime != null)
                overtime.enabled = false;

            var poi = Object.FindFirstObjectByType<POIManager>();
            if (poi != null)
                poi.enabled = false;

            var enemyWorld = Object.FindFirstObjectByType<EnemyWorld>();
            enemyWorld?.DespawnAllActive();
        }
    }
}
