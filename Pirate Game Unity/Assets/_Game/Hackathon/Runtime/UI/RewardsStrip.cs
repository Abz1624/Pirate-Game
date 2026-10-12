using System;
using System.Text;
using RoguePirates.V4.Online;
using RoguePirates.V4.Presentation.Online;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon (12 Oct): the weekly SOL rewards on V4's leaderboard panel (the rows show each place's wallet and reward
    /// from the board JSON, a small V4 change): a "Rewards" strip above the panel's footer with the prize table (GET
    /// /api/rewards/weekly) and last week's winners with devnet explorer buttons for their payouts. When last week has no
    /// winners yet, the current week's (paid early for the demo) are shown.
    /// </summary>
    public sealed class RewardsStrip : MonoBehaviour
    {
        const float Height = 112f;
        LeaderboardPanel _panel;
        TextMeshProUGUI _table, _winners;
        readonly Button[] _links = new Button[3];
        readonly string[] _urls = new string[3];
        int _requests;

        public string TableText => _table != null ? _table.text : null;
        public string WinnersText => _winners != null ? _winners.text : null;

        public static RewardsStrip Attach(LeaderboardPanel panel)
        {
            if (panel == null) return null;
            var existing = panel.GetComponent<RewardsStrip>();
            if (existing != null) return existing;
            var strip = panel.gameObject.AddComponent<RewardsStrip>();
            strip._panel = panel;
            strip.Build((RectTransform)panel.transform);
            return strip;
        }

        void Build(RectTransform rt)
        {
            // The list ends above the strip (it sat 104 above the bottom, over the footer).
            var body = rt.Find("Body") as RectTransform;
            if (body != null) body.offsetMin = new Vector2(body.offsetMin.x, body.offsetMin.y + Height + 8f);

            var area = HackUi.Area(rt, "HackathonRewards", Vector2.zero, new Vector2(1f, 0f));
            area.offsetMin = new Vector2(24f, 104f);
            area.offsetMax = new Vector2(-24f, 104f + Height);
            area.gameObject.AddComponent<Image>().color = new Color(0.45f, 0.75f, 0.45f, 0.12f);
            _table = HackUi.TextAt(area, "Table", "Rewards: Top 3 each week win devnet SOL: 0.5 / 0.3 / 0.2", 25, TextAlignmentOptions.MidlineLeft,
                HackUi.Gold, new Vector2(0f, 0.52f), Vector2.one);
            _table.margin = new Vector4(16f, 0f, 16f, 0f);
            _winners = HackUi.TextAt(area, "Winners", string.Empty, 20, TextAlignmentOptions.MidlineLeft, HackUi.TextColor, Vector2.zero, new Vector2(0.58f, 0.52f));
            _winners.margin = new Vector4(16f, 0f, 8f, 0f);
            _winners.textWrappingMode = TextWrappingModes.Normal;
            for (var i = 0; i < _links.Length; i++)
            {
                var index = i;
                var x0 = 0.585f + i * 0.14f;
                _links[i] = HackUi.Button(area, "#" + (i + 1) + " payout", () => { if (_urls[index] != null) Application.OpenURL(_urls[index]); },
                    new Vector2(x0, 0.06f), new Vector2(x0 + 0.13f, 0.5f));
                var t = _links[i].GetComponentInChildren<TextMeshProUGUI>(true);
                if (t != null) t.fontSize = 20;
                _links[i].gameObject.SetActive(false);
            }
        }

        void OnEnable()
        {
            if (_table != null) Refresh(); // the panel opened
        }

        public async void Refresh()
        {
            var request = ++_requests;
            try
            {
                var (data, error) = await HackathonApi.WeeklyRewards();
                if (this == null || request != _requests) return;
                if (error != null) { _winners.text = "Rewards unavailable: " + error; return; }
                var label = "Last week";
                if (data.winners == null || data.winners.Length == 0)
                {
                    // Demo: the current week may have been paid early by the owner.
                    var monday = Monday(DateTime.UtcNow).ToString("yyyy-MM-dd");
                    var (current, _) = await HackathonApi.WeeklyRewards(monday);
                    if (this == null || request != _requests) return;
                    if (current?.winners != null && current.winners.Length > 0) { data = current; label = "This week (paid early for the demo)"; }
                }
                Show(data, label);
            }
            catch (Exception e)
            {
                Debug.LogWarning("RewardsStrip: " + e.Message);
                if (this != null) _winners.text = "Rewards unavailable.";
            }
        }

        void Show(WeeklyRewardsResponse data, string label)
        {
            var table = new StringBuilder("Rewards: Top 3 each week win devnet SOL: ");
            if (data.table != null && data.table.Length > 0)
                for (var i = 0; i < data.table.Length; i++) table.Append(i > 0 ? " / " : string.Empty).Append(data.table[i].sol.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            else table.Append("0.5 / 0.3 / 0.2");
            table.Append("  <size=75%><color=#9aa4ad>(wallet players, paid at the weekly reset)</color></size>");
            _table.text = table.ToString();

            for (var i = 0; i < _links.Length; i++) { _urls[i] = null; _links[i].gameObject.SetActive(false); }
            if (data.winners == null || data.winners.Length == 0)
            {
                _winners.text = $"{label}: no winners yet. Top the board with a wallet to win.";
                return;
            }
            var sb = new StringBuilder(label + ": ");
            for (var i = 0; i < data.winners.Length && i < 3; i++)
            {
                var w = data.winners[i];
                if (w == null) continue;
                sb.Append(i > 0 ? "   " : string.Empty)
                  .Append("<color=#ffd166>#").Append(w.rank).Append("</color> ").Append(w.name)
                  .Append(" <size=80%><color=#9aa4ad>").Append(string.IsNullOrEmpty(w.walletShort) ? Web3Rules.ShortAddress(w.wallet) : w.walletShort).Append("</color></size> ")
                  .Append(w.sol.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(" SOL");
                var sig = w.payout != null ? w.payout.signature : null;
                var slot = Mathf.Clamp(w.rank - 1, 0, _links.Length - 1);
                _urls[slot] = HackathonRules.ExplorerTx(sig);
                _links[slot].gameObject.SetActive(_urls[slot] != null);
            }
            _winners.text = sb.ToString();
        }

        static DateTime Monday(DateTime utc)
        {
            var d = utc.Date;
            var diff = ((int)d.DayOfWeek + 6) % 7;
            return d.AddDays(-diff);
        }
    }
}
