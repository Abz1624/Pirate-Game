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

            var systemsWeapon = FindHumanWeaponController(humanPlayer);
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

            var rng = RunManager.Instance != null ? RunManager.Instance.Rng : null;
            var humanLoadout = V3PvPWeaponLoadout.RollRandomWeapons(database, rng);
            var rivalLoadout = V3PvPWeaponLoadout.RollRandomWeapons(database, rng);

            if (humanWeapons != null && humanLoadout.Length > 0)
            {
                humanWeapons.ApplyCombatExperiment(CombatExperimentContext.Active);
                humanWeapons.Initialize(database, humanLoadout[0]);
                humanWeapons.SetWeaponLoadout(humanLoadout);
            }

            LevelSystem.Instance?.SetPlaytestWeaponLoadout(humanLoadout);

            var forward = humanPlayer.MovementForward;
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var spawn = humanPlayer.Position + right * spacing;
            spawn.y = 0f;
            humanPlayer.SetWorldPosition(humanPlayer.Position - right * (spacing * 0.5f));

            var rival = PlayerShipFactory.SpawnFromTemplate(
                humanPlayer,
                "RivalShip_Bot",
                spawn,
                Quaternion.LookRotation(-forward, Vector3.up),
                V3ShipTeam.RivalBot,
                localHuman: false,
                database,
                rivalLoadout.Length > 0 ? rivalLoadout[0] : "deck_cannons",
                humanPlayer,
                rivalLoadout);

            V3PvPRunDirector.Ensure(rival);

            V3ToastHud.Instance?.Show(
                "PvP duel — defeat the rival captain.\nYou and the rival each have 6 random weapons.",
                5f);
        }

        static WeaponController FindHumanWeaponController(PlayerShipController humanPlayer)
        {
            var all = Object.FindObjectsByType<WeaponController>(FindObjectsSortMode.None);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                if (all[i].GetComponent<PlayerShipController>() != null)
                    continue;
                return all[i];
            }

            return humanPlayer != null ? humanPlayer.GetComponent<WeaponController>() : null;
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

            var wingA = humanPlayer.Position + right * spacing * 0.85f + back * (spacing * 0.35f);
            var wingB = humanPlayer.Position - right * spacing * 0.85f + back * (spacing * 0.35f);
            wingA.y = wingB.y = 0f;

            PlayerShipFactory.SpawnFromTemplate(
                humanPlayer, "AllyShip_A", wingA, humanPlayer.transform.rotation,
                V3ShipTeam.AllyBot, false, database, startingWeaponId, humanPlayer);
            PlayerShipFactory.SpawnFromTemplate(
                humanPlayer, "AllyShip_B", wingB, humanPlayer.transform.rotation,
                V3ShipTeam.AllyBot, false, database, startingWeaponId, humanPlayer);
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
