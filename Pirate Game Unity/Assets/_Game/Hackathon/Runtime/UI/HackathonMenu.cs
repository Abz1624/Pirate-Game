using RoguePirates.V4.Presentation.Menu;
using RoguePirates.V4.Presentation.Online;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RoguePirates.Hackathon.UI
{
    /// <summary>
    /// Hackathon (12 Oct): attaches the hackathon's menu pieces to V4's online menu when the menu scene loads (V4 code
    /// unchanged): the 1v1 wager and AI captain additions (<see cref="WagerMenu"/>), the "DAILY RACE" button (a copy of
    /// the art menu's PvP button, one step above it) and its panel (<see cref="DailyRacePanel"/>), and the weekly rewards
    /// strip on the leaderboard (<see cref="RewardsStrip"/>).
    /// </summary>
    public sealed class HackathonMenu : MonoBehaviour
    {
        public WagerMenu Wagers { get; private set; }
        public DailyRacePanel Race { get; private set; }
        public RewardsStrip Rewards { get; private set; }
        public Button RaceButton { get; private set; }
        public IslandsPanel Islands { get; private set; }
        public MarketPanel Market { get; private set; }
        public Button IslandsButton { get; private set; }
        public Button MarketButton { get; private set; }

        static string _shownRaid;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryAttach();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryAttach();

        public static HackathonMenu TryAttach()
        {
            var online = FindFirstObjectByType<OnlineMenuPresenter>();
            if (online == null) return null;
            // Back at the menu: no raid schedule is running or waiting (13 Oct, review).
            RoguePirates.V4.Spawning.RaidSchedule.Clear();
            var existing = online.GetComponent<HackathonMenu>();
            if (existing != null) return existing;
            var canvas = online.transform.parent;
            var menu = online.gameObject.AddComponent<HackathonMenu>();
            menu.Wagers = WagerMenu.Attach(online, canvas);
            menu.Race = DailyRacePanel.Create(canvas);
            menu.Rewards = RewardsStrip.Attach(online.Leaderboard);
            menu.RaceButton = BuildRaceButton(canvas, online.PvpButton, menu.Race);
            // U-3: Islands and Market (copies of the art button under Daily Race; the unused V4 "Raid" placeholder goes,
            // raids live on the Islands screen).
            menu.Islands = IslandsPanel.Create(canvas);
            menu.Market = MarketPanel.Create(canvas);
            menu.Islands.Market = menu.Market;
            menu.Market.Islands = menu.Islands;
            var main = FindDeep(canvas, "MainMenuPanel");
            var raid = main != null ? FindDeep(main, "Raid") : null;
            if (raid != null && raid.GetComponent<Button>() != null) raid.gameObject.SetActive(false);
            menu.IslandsButton = ArtButton(online.PvpButton, "HackathonIslands", "ISLANDS", menu.Islands.Open);
            menu.MarketButton = ArtButton(online.PvpButton, "HackathonMarket", "MARKET", menu.Market.Open);
            if (main != null) TightenColumn(main);
            // Back from a raid voyage: show its result on the Islands screen.
            var last = RaidHud.LastRaid != null ? RaidHud.LastRaid.raidId : RaidHud.LastResult;
            if (last != null && last != _shownRaid)
            {
                _shownRaid = last;
                menu.Islands.Open();
            }
            return menu;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _shownRaid = null;

        /// <summary>Six side buttons in the height five had: a tighter gap, the column keeps its centre.</summary>
        static void TightenColumn(Transform main)
        {
            var menu = FindDeep(main, "RightMenu") as RectTransform;
            var layout = menu != null ? menu.GetComponent<VerticalLayoutGroup>() : null;
            if (layout != null) layout.spacing = Mathf.Min(layout.spacing, 12f);
            ArtMenuView.FitSideButtons(main);
            if (menu == null || layout == null) return;
            var height = 0f;
            var count = 0;
            foreach (Transform child in menu)
            {
                if (!child.gameObject.activeSelf) continue;
                var le = child.GetComponent<LayoutElement>();
                height += le != null && le.preferredHeight > 0f ? le.preferredHeight : 65f;
                count++;
            }
            if (count > 0) height += layout.spacing * (count - 1) + layout.padding.vertical;
            menu.sizeDelta = new Vector2(menu.sizeDelta.x, height);
            LayoutRebuilder.MarkLayoutForRebuild(menu);
        }

        /// <summary>A copy of the art menu's PvP button just above it in the side column, labelled <paramref name="label"/>.</summary>
        static Button ArtButton(Button pvp, string name, string label, UnityEngine.Events.UnityAction click)
        {
            if (pvp == null || pvp.transform.parent == null) return null;
            var copy = Instantiate(pvp.gameObject, pvp.transform.parent, false);
            copy.name = name;
            copy.transform.SetSiblingIndex(pvp.transform.GetSiblingIndex());
            var button = copy.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.interactable = true;
            foreach (var t in copy.GetComponentsInChildren<TextMeshProUGUI>(true)) t.text = label;
            foreach (var t in copy.GetComponentsInChildren<Text>(true)) t.text = label;
            button.onClick.AddListener(click);
            return button;
        }

        /// <summary>A copy of the PvP button one button step above it ("DAILY RACE").</summary>
        static Button BuildRaceButton(Transform canvas, Button pvp, DailyRacePanel panel)
        {
            Button button;
            var main = FindDeep(canvas, "MainMenuPanel");
            if (pvp != null && pvp.transform.parent != null)
            {
                var copy = Instantiate(pvp.gameObject, pvp.transform.parent, false);
                copy.name = "HackathonDailyRace";
                var source = (RectTransform)pvp.transform;
                var crt = (RectTransform)copy.transform;
                var leaderboard = main != null ? FindDeep(main, "Leaderboard") as RectTransform : null;
                var step = leaderboard != null ? source.anchoredPosition.y - leaderboard.anchoredPosition.y : source.rect.height + 16f;
                if (Mathf.Abs(step) < 1f) step = source.rect.height + 16f;
                crt.anchoredPosition = source.anchoredPosition + new Vector2(0f, step);
                copy.transform.SetSiblingIndex(pvp.transform.GetSiblingIndex());
                button = copy.GetComponent<Button>();
                button.onClick = new Button.ButtonClickedEvent();
                button.interactable = true;
                foreach (var t in copy.GetComponentsInChildren<TextMeshProUGUI>(true)) t.text = "DAILY RACE";
                foreach (var t in copy.GetComponentsInChildren<Text>(true)) t.text = "DAILY RACE";
                if (main != null) ArtMenuView.FitSideButtons(main);
            }
            else
            {
                // No art menu: a gold kit button (13 Oct, the art team's UI kit).
                var rt = MarketKit.Node(main != null ? main : canvas, "HackathonDailyRace");
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.sizeDelta = new Vector2(300f, 86f);
                rt.anchoredPosition = new Vector2(-48f, 230f);
                button = MarketKit.KitButton(rt, "Daily Race", "Icon_Flag", null, "Btn_Gold", 30f, 36f);
            }
            button.onClick.AddListener(panel.Open);
            return button;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
