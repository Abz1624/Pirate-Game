using RoguePirates.V4.Config;
using RoguePirates.V4.Rewards;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Console
{
    /// <summary>
    /// Rum and Results (M12): each Rum row's credits and Rum so far this run, the settlement status and the treasury,
    /// and a switch for the extra "1 Rum per chest" row. Row values (Rum per credit, kills per minute, caps) are edited
    /// live in the Config section (rewards.*).
    /// </summary>
    public sealed class RumSection : IConsoleSection
    {
        public string Title => "Rum & Results";
        public string SearchText => "rum rewards results marks settlement treasury unlock chest minute overtime elite boss landmark";

        const float Width = 440f;
        GUIStyle _wrap;

        public void Draw(TuningConsole console)
        {
            var game = console.Game;
            if (game?.Context == null || !game.Context.TryGet<IRumView>(out var rum)) { GUILayout.Label("No V4 reward module in this scene."); return; }
            if (_wrap == null) _wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };
            var rows = rum.Settled ? rum.SettledRows : rum.Live;
            var config0 = game.Config?.Single<RewardConfig>();
            var total = 0;
            foreach (var r in rows)
            {
                var per = config0?.Row(r.RowId)?.rum ?? 0;
                GUILayout.Label($"{r.Label}: {r.Credits} × {per} → {r.Rum} {rum.CurrencyName}", _wrap, GUILayout.Width(Width));
                total += r.Rum;
            }
            GUILayout.Label(rum.Settled
                ? $"Settled: {rum.SettledTotal} {rum.CurrencyName} ({(rum.SettlementApplied ? "paid" : "not paid: already settled for this run id")})"
                : $"So far: {total} {rum.CurrencyName} (paid at death; the minute in progress does not count yet)", _wrap, GUILayout.Width(Width));
            GUILayout.Label($"Treasury: {rum.Treasury} {rum.CurrencyName}", _wrap, GUILayout.Width(Width));

            var config = game.Config?.Single<RewardConfig>();
            var chests = config?.Row("chests");
            if (chests == null) return;
            var index = config.rows.IndexOf(chests);
            var on = GUILayout.Toggle(chests.enabled, $"Row: {chests.rum} {rum.CurrencyName} per chest opened", GUILayout.Width(Width));
            if (on != chests.enabled)
            {
                var t = HighSeas.Core.RunManager.Instance != null ? HighSeas.Core.RunManager.Instance.ElapsedSeconds : 0f;
                if (!game.Config.TryApplyEdit(config.configId, $"rows[{index}].enabled", on ? "true" : "false", t, out var error)) Debug.LogWarning("Rum: " + error);
            }
        }
    }
}
