using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Platform;
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
                case V3RunSessionKind.SplitScreenCoOp:
                    ConfigureSplitScreenCoOp(humanPlayer, database, startingWeaponId, spacing);
                    break;
            }

            var label = kind switch
            {
                V3RunSessionKind.PvP => "PvP duel (WASD vs arrows)",
                V3RunSessionKind.SplitScreenCoOp => "Split-screen co-op",
                _ => "Co-op (WASD + arrows + numpad 8456)"
            };
            V3ToastHud.Instance?.Show($"{label} ready.\nPress F8 for playtest keys.", 5f);
        }

        static void ConfigurePvP(
            PlayerShipController humanPlayer,
            GameDatabase database,
            WeaponController humanWeapons,
            float spacing)
        {
            V3PvPRunDirector.ResetState();
            DisableAmbientPve();

            var duelLoadout = V3PvPWeaponLoadout.GetDuelLoadout(database);
            var duelLevel = V3PvPWeaponLoadout.DuelWeaponLevel;

            V3LocalShipInput.Bind(humanPlayer, ShipKeyboardBinding.Wasd);
            V3MultiShipWeaponLoadouts.ApplyLoadout(humanPlayer, database, duelLoadout, duelLevel);
            LevelSystem.Instance?.SetPlaytestWeapon(V3PvPWeaponLoadout.DuelWeaponId, duelLevel);

            var forward = humanPlayer.MovementForward;
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var humanPos = V3MultiShipSpawnUtil.ResolveSafeSpawn(
                humanPlayer.Position - right * (spacing * 0.5f));
            humanPlayer.SetWorldPosition(humanPos);
            var spawn = V3MultiShipSpawnUtil.ResolveSafeSpawn(humanPos + right * spacing);

            var rival = PlayerShipFactory.SpawnFromTemplate(
                humanPlayer,
                "RivalShip_Player2",
                spawn,
                Quaternion.LookRotation(-forward, Vector3.up),
                V3ShipTeam.RivalBot,
                localHuman: true,
                database,
                duelLoadout.Length > 0 ? duelLoadout[0] : "deck_cannons",
                humanPlayer,
                duelLoadout,
                ShipKeyboardBinding.ArrowKeys,
                useBotBrain: false);

            V3MultiShipWeaponLoadouts.ApplyLoadout(rival, database, duelLoadout, duelLevel);

            V3PvPRunDirector.Ensure(rival);
            V3ShipNameLabels.TagShips(humanPlayer, rival);

            V3ToastHud.Instance?.Show(
                $"PvP — Player 1: WASD · Player 2: Arrow keys\nBroadside Lv{duelLevel} each.",
                6f);
        }

        static void ConfigureCoOp(
            PlayerShipController humanPlayer,
            GameDatabase database,
            string startingWeaponId,
            float spacing)
        {
            V3LocalShipInput.Bind(humanPlayer, ShipKeyboardBinding.Wasd);

            var forward = humanPlayer.MovementForward;
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var back = -forward;

            var wingA = V3MultiShipSpawnUtil.ResolveSafeSpawn(
                humanPlayer.Position + right * spacing * 0.85f + back * (spacing * 0.35f));
            var wingB = V3MultiShipSpawnUtil.ResolveSafeSpawn(
                humanPlayer.Position - right * spacing * 0.85f + back * (spacing * 0.35f));

            var allyA = PlayerShipFactory.SpawnFromTemplate(
                humanPlayer, "AllyShip_Player2", wingA, humanPlayer.transform.rotation,
                V3ShipTeam.AllyBot, true, database, startingWeaponId, humanPlayer,
                keyboardBinding: ShipKeyboardBinding.ArrowKeys, useBotBrain: false);
            var allyB = PlayerShipFactory.SpawnFromTemplate(
                humanPlayer, "AllyShip_Player3", wingB, humanPlayer.transform.rotation,
                V3ShipTeam.AllyBot, true, database, startingWeaponId, humanPlayer,
                keyboardBinding: ShipKeyboardBinding.Numpad8456, useBotBrain: false);

            V3ShipNameLabels.TagShips(humanPlayer, allyA, allyB);
        }

        static void ConfigureSplitScreenCoOp(
            PlayerShipController humanPlayer,
            GameDatabase database,
            string startingWeaponId,
            float spacing)
        {
            V3LocalShipInput.Bind(humanPlayer, ShipKeyboardBinding.Wasd);

            var forward = humanPlayer.MovementForward;
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var spawn = V3MultiShipSpawnUtil.ResolveSafeSpawn(
                humanPlayer.Position + right * spacing * 0.75f);

            var playerTwo = PlayerShipFactory.SpawnFromTemplate(
                humanPlayer,
                "CoOp_Player2",
                spawn,
                humanPlayer.transform.rotation,
                V3ShipTeam.AllyBot,
                localHuman: true,
                database,
                startingWeaponId,
                humanPlayer,
                keyboardBinding: ShipKeyboardBinding.ArrowKeys,
                useBotBrain: false);

            V3SplitScreenRig.Enable(humanPlayer, playerTwo);
            V3ShipNameLabels.TagShips(humanPlayer, playerTwo);

            V3ToastHud.Instance?.Show(
                "Split-screen co-op\nLeft: WASD · Right: Arrow keys",
                5f);
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
