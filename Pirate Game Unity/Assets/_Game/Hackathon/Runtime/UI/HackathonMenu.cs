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
            var existing = online.GetComponent<HackathonMenu>();
            if (existing != null) return existing;
            var canvas = online.transform.parent;
            var menu = online.gameObject.AddComponent<HackathonMenu>();
            menu.Wagers = WagerMenu.Attach(online, canvas);
            menu.Race = DailyRacePanel.Create(canvas);
            menu.Rewards = RewardsStrip.Attach(online.Leaderboard);
            menu.RaceButton = BuildRaceButton(canvas, online.PvpButton, menu.Race);
            return menu;
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
                button = HackUi.Button(main != null ? main : canvas, "Daily Race", null);
                var rt = (RectTransform)button.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.sizeDelta = new Vector2(300f, HackUi.RowHeight);
                rt.anchoredPosition = new Vector2(-48f, 230f);
                button.onClick = new Button.ButtonClickedEvent();
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
