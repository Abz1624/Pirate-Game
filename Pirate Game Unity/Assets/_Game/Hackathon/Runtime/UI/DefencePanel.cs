using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon (12 Oct): "Design defences" for the player's island. The island is a 15-minute level: one row per minute
    /// with its budget bar, the enemies in it (+ / - per kind, "Add enemy" picks from the catalog grouped by role), the
    /// guardian's arrival minute, the presets (Swarm, Gunline, Elite guard) and Save (PUT /api/islands/{id}/defence).
    /// Over-budget windows show red as you edit; the server's per-window errors show under the header.
    /// </summary>
    public sealed class DefencePanel : MonoBehaviour
    {
        RectTransform _list;
        TextMeshProUGUI _title, _status;
        Button _save, _close, _test;
        bool _dirty;
        DefenceCatalog _catalog;
        IslandDefenceView _view;
        DefenceDesign _design;
        string _islandId, _islandName;
        int _openPicker = -1;
        bool _busy;
        readonly Dictionary<string, int> _costs = new Dictionary<string, int>();
        readonly Dictionary<string, DefenceKind> _kinds = new Dictionary<string, DefenceKind>();
        int[] _budgets = new int[DefenceRules.Windows];
        string _problems;

        public bool IsOpen => gameObject.activeSelf;
        public DefenceDesign Design => _design;
        public string StatusText => _status != null ? _status.text : null;
        public bool Busy => _busy;
        public string Problems => _problems;

        public static DefencePanel Create(Transform canvas)
        {
            var rt = HackUi.Panel(canvas, "HackathonDefence", new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.96f), 229);
            var panel = rt.gameObject.AddComponent<DefencePanel>();
            panel.Build(rt);
            return panel;
        }

        void Build(RectTransform rt)
        {
            var header = HackUi.TopStrip(rt, "Header", 14f, 80f);
            _title = HackUi.TextAt(header, "Title", "Design defences", 42, TextAlignmentOptions.MidlineLeft, HackUi.Gold, Vector2.zero, new Vector2(0.44f, 1f), true);
            _test = HackUi.Button(header, "Test defences", TestDefences, new Vector2(0.45f, 0.06f), new Vector2(0.67f, 0.94f));
            _save = HackUi.Button(header, "Save", Save, new Vector2(0.68f, 0.06f), new Vector2(0.83f, 0.94f));
            _close = HackUi.Button(header, "Close", Close, new Vector2(0.84f, 0.06f), new Vector2(1f, 0.94f));
            _status = HackUi.StatusBar(rt, 100f, 56f);
            var body = HackUi.Area(rt, "Body", Vector2.zero, Vector2.one, new Vector4(24f, 20f, 24f, 168f));
            _list = HackUi.ScrollList(body, "Scroll", Vector4.zero);
            _list.GetComponent<VerticalLayoutGroup>().spacing = 6f;
        }

        public async void Open(string islandId, string islandName)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _islandId = islandId;
            _islandName = islandName;
            _title.text = $"Defences of {islandName}";
            _design = null;
            _openPicker = -1;
            _problems = null;
            Clear();
            Status("Loading the design...", HackUi.Dim);
            _busy = true;
            try
            {
                if (_catalog == null)
                {
                    var (catalog, cerr) = await IslandApi.DefenceCatalog();
                    if (this == null) return;
                    if (cerr != null) { Status(cerr, HackUi.Bad); return; }
                    _catalog = catalog;
                }
                var (view, error) = await IslandApi.Defence(islandId);
                if (this == null) return;
                if (error != null) { Status(error, HackUi.Bad); return; }
                Apply(view);
                Status(view.isCustom ? "Your saved design. Edit a minute, then Save." : "Default design. Pick a preset or edit a minute, then Save.", HackUi.Dim);
            }
            finally { _busy = false; }
        }

        void Apply(IslandDefenceView view)
        {
            _view = view;
            _costs.Clear();
            _kinds.Clear();
            var kinds = view.kinds != null && view.kinds.Length > 0 ? view.kinds : _catalog?.kinds;
            if (kinds != null) foreach (var k in kinds) if (k != null) { _costs[k.kind] = k.cost; _kinds[k.kind] = k; }
            _budgets = view.budgets != null && view.budgets.Length >= DefenceRules.Windows ? view.budgets : _budgets;
            _design = DefenceRules.Normalize(view.design, _catalog?.defaultGuardian ?? "barnacle_king");
            _dirty = false;
            Render();
        }

        /// <summary>Called after Close (the Islands screen reopens).</summary>
        public Action Closed;
        /// <summary>"Test defences": starts a practice raid on (islandId, islandName) with the saved design.</summary>
        public Action<string, string> Practice;
        public bool Dirty => _dirty;

        /// <summary>Saves unsaved changes first (a practice raid uses the saved design), then starts the practice raid.</summary>
        public async void TestDefences()
        {
            if (_design == null || _busy) return;
            if (_dirty && !await SaveNow()) return;
            if (this == null) return;
            var id = _islandId;
            var name = _islandName;
            Close();
            Practice?.Invoke(id, name);
        }

        public void Close()
        {
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        void Status(string text, Color color)
        {
            _status.text = text;
            _status.color = color;
        }

        void Clear()
        {
            for (var i = _list.childCount - 1; i >= 0; i--)
            {
                var c = _list.GetChild(i).gameObject;
                c.SetActive(false);
                Destroy(c);
            }
        }

        public int Budget(int minute) => minute >= 1 && minute <= _budgets.Length ? _budgets[minute - 1] : 0;
        public int Cost(int minute) => _design != null ? DefenceRules.Cost(_design.windows[minute - 1], _costs) : 0;

        public int OverBudgetWindows()
        {
            var n = 0;
            if (_design == null) return 0;
            for (var m = 1; m <= DefenceRules.Windows; m++) if (Cost(m) > Budget(m)) n++;
            return n;
        }

        void Render()
        {
            var keep = _list.anchoredPosition;
            Clear();
            if (_design == null) return;

            // Presets.
            if (_catalog?.presets != null && _catalog.presets.Length > 0)
            {
                HackUi.Section(_list, "Presets", "replace the whole design");
                var buttons = new List<(string, Action)>();
                foreach (var p in _catalog.presets)
                {
                    var preset = p;
                    buttons.Add((p.name, () => ApplyPreset(preset)));
                }
                IslandUi.ButtonRowOf(_list, buttons.ToArray());
            }

            // Guardian.
            var g = _design.guardian;
            var guardianName = BossName(g.kind);
            var grow = HackUi.Row(_list, IslandUi.ButtonRow, IslandUi.MineTint);
            var gcell = HackUi.Cell(grow, $"<b>Guardian: {guardianName}</b>\n<size=85%>arrives at minute {g.minute} ({IslandRules.Clock((g.minute - 1) * 60)})</size>", 0f, 25, TextAlignmentOptions.MidlineLeft, HackUi.TextColor);
            gcell.textWrappingMode = TextWrappingModes.Normal;
            IslandUi.RowButton(grow, "Earlier", () => GuardianMinute(-1), 150f).interactable = g.minute > 2;
            IslandUi.RowButton(grow, "Later", () => GuardianMinute(1), 150f).interactable = g.minute < DefenceRules.Windows;

            if (!string.IsNullOrEmpty(_problems)) IslandUi.TextRow(_list, _problems, 24, HackUi.Bad, 44f, new Color(0.5f, 0.1f, 0.08f, 0.35f));

            for (var m = 1; m <= DefenceRules.Windows; m++) Window(m);

            Canvas.ForceUpdateCanvases();
            _list.anchoredPosition = keep;
        }

        void Window(int minute)
        {
            var w = _design.windows[minute - 1];
            var cost = Cost(minute);
            var budget = Budget(minute);
            var over = cost > budget;
            var guardianHere = _design.guardian.minute == minute;
            var head = HackUi.Row(_list, IslandUi.ButtonRow, over ? new Color(0.6f, 0.12f, 0.1f, 0.45f) : new Color(1f, 1f, 1f, 0.08f));
            var title = $"<b>Minute {minute}</b>  <size=80%><color=#9aa4ad>{IslandRules.Clock((minute - 1) * 60)}</color></size>" +
                        (guardianHere ? "  <color=#ffd166>+ guardian</color>" : string.Empty);
            HackUi.Cell(head, title, 0f, 26, TextAlignmentOptions.MidlineLeft, HackUi.TextColor);
            var m = minute;
            IslandUi.RowButton(head, _openPicker == minute ? "Done" : "Add enemy", () => { _openPicker = _openPicker == m ? -1 : m; Render(); }, 230f);
            var bar = IslandUi.Bar(_list, budget > 0 ? (float)cost / budget : 0f,
                over ? $"OVER BUDGET  {cost} / {budget} points" : $"{cost} / {budget} points", over ? new Color(0.9f, 0.25f, 0.2f, 1f) : new Color(0.3f, 0.65f, 0.4f, 1f));
            bar.label.color = HackUi.TextColor;

            if (w.enemies == null || w.enemies.Length == 0)
                IslandUi.TextRow(_list, "<color=#9aa4ad>No ships this minute.</color>", 23, null, 40f);
            else
                foreach (var e in w.enemies)
                {
                    var kind = e.kind;
                    var row = HackUi.Row(_list, IslandUi.ButtonRow, IslandUi.RowTint);
                    var c = _costs.TryGetValue(kind, out var each) ? each : 0;
                    HackUi.Cell(row, $"{KindName(kind)}  <b>x {e.count}</b>  <size=80%><color=#9aa4ad>{c * e.count} pts</color></size>", 0f, 25, TextAlignmentOptions.MidlineLeft, HackUi.TextColor);
                    IslandUi.RowButton(row, "-", () => Change(m, kind, -1), 96f);
                    IslandUi.RowButton(row, "+", () => Change(m, kind, 1), 96f);
                }

            if (_openPicker == minute) Picker(minute);
        }

        void Picker(int minute)
        {
            var groups = new List<string>();
            var byRole = new Dictionary<string, List<DefenceKind>>();
            foreach (var k in _kinds.Values)
            {
                var role = k.role ?? string.Empty;
                if (!byRole.TryGetValue(role, out var list)) { byRole[role] = list = new List<DefenceKind>(); groups.Add(role); }
                list.Add(k);
            }
            var left = Budget(minute) - Cost(minute);
            foreach (var role in groups)
            {
                IslandUi.TextRow(_list, $"<color=#ffd166>{DefenceRules.RoleName(role)}</color>", 23, null, 36f, new Color(0f, 0f, 0f, 0.25f));
                foreach (var k in byRole[role])
                {
                    var kind = k.kind;
                    var row = HackUi.Row(_list, IslandUi.ButtonRow, new Color(0f, 0f, 0f, 0.25f));
                    var fits = k.cost <= left;
                    HackUi.Cell(row, $"{k.displayName}  <size=80%><color={(fits ? "#9aa4ad" : "#ff8c73")}>{k.cost} pts each</color></size>", 0f, 24, TextAlignmentOptions.MidlineLeft, HackUi.TextColor);
                    IslandUi.RowButton(row, "+1", () => Change(minute, kind, 1), 110f);
                    IslandUi.RowButton(row, "+5", () => Change(minute, kind, 5), 110f);
                }
            }
        }

        string KindName(string kind) => _kinds.TryGetValue(kind ?? string.Empty, out var k) && !string.IsNullOrEmpty(k.displayName) ? k.displayName : kind;

        string BossName(string kind)
        {
            var bosses = _view?.bosses ?? _catalog?.bosses;
            if (bosses != null) foreach (var b in bosses) if (b != null && b.kind == kind) return b.displayName;
            return "Barnacle King";
        }

        public void Change(int minute, string kind, int delta)
        {
            if (_design == null || minute < 1 || minute > DefenceRules.Windows) return;
            DefenceRules.Add(_design.windows[minute - 1], kind, delta);
            Edited();
        }

        public void GuardianMinute(int delta)
        {
            if (_design == null) return;
            _design.guardian.minute = Mathf.Clamp(_design.guardian.minute + delta, 2, DefenceRules.Windows);
            _design.guardian.arrivesAtSecond = (_design.guardian.minute - 1) * 60;
            Edited();
        }

        public void ApplyPreset(DefencePreset preset)
        {
            if (preset?.design == null) return;
            _design = DefenceRules.Normalize(preset.design, _catalog?.defaultGuardian ?? "barnacle_king");
            _openPicker = -1;
            Edited();
            Status($"Preset {preset.name}: {preset.description} Save to keep it.", HackUi.Gold);
        }

        public bool ApplyPreset(string id)
        {
            if (_catalog?.presets == null) return false;
            foreach (var p in _catalog.presets) if (p != null && p.id == id) { ApplyPreset(p); return true; }
            return false;
        }

        void Edited()
        {
            _problems = null;
            _dirty = true;
            var over = OverBudgetWindows();
            Status(over > 0 ? $"{over} minute{(over == 1 ? " is" : "s are")} over budget. The server will refuse the design until they fit." : "Unsaved changes. Save to keep them.",
                over > 0 ? HackUi.Bad : HackUi.Gold);
            Render();
        }

        public async void Save() => await SaveNow();

        async System.Threading.Tasks.Task<bool> SaveNow()
        {
            if (_design == null || _busy) return false;
            _busy = true;
            _save.interactable = false;
            Status("Saving the design...", HackUi.Gold);
            try
            {
                var (view, error, problems) = await IslandApi.SaveDefence(_islandId, DefenceRules.ForSave(_design));
                if (this == null) return false;
                if (error != null)
                {
                    if (problems != null && problems.Length > 0)
                    {
                        var sb = new StringBuilder("<b>Not saved:</b>");
                        foreach (var p in problems) sb.Append("\n").Append(DefenceRules.Problem(p));
                        _problems = sb.ToString();
                    }
                    Status(error + (problems != null && problems.Length > 0 ? $" {problems.Length} problem{(problems.Length == 1 ? string.Empty : "s")} listed below." : string.Empty), HackUi.Bad);
                    Render();
                    return false;
                }
                _problems = null;
                Apply(view);
                Status($"Saved. Raiders of {_islandName} now face this design. Press Test defences to try it.", HackUi.Good);
                return true;
            }
            finally
            {
                _busy = false;
                if (this != null) _save.interactable = true;
            }
        }
    }
}
