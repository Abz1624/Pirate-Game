using System.Collections.Generic;
using RoguePirates.V4.Foundation.Progression;
using RoguePirates.V4.Composition;
using RoguePirates.V4.Config;
using RoguePirates.V4.Presentation.Menu;
using RoguePirates.V4.Rewards;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RoguePirates.V4
{
    /// <summary>
    /// V4 main menu (M14.13; was V3's carousel with V3's locks): the Captain cards from the Captain configs in menu order,
    /// locked by <see cref="CaptainLocks"/> (the adopted locks; the session's v3-parity preset restores V3's, through
    /// <see cref="MenuServices"/>). A locked Captain whose
    /// only missing piece is Rum turns Set Sail into an unlock for that Rum. Set Sail saves the Captain (the shared
    /// selection the run reads) and loads the V4 run scene.
    /// </summary>
    public sealed class V4MainMenuController : MonoBehaviour
    {
        [SerializeField] Canvas menuCanvas;
        [Tooltip("The Captain, unlock and reward configs the menu reads (the run's config set).")]
        [SerializeField] ConfigSet configSet;

        ICaptainMenuView _view;
        MenuServices _services;
        ConfigSetRuntime _config;
        CaptainLocks _locks;
        UnlockService _unlocks;
        readonly List<CaptainConfig> _captains = new List<CaptainConfig>();
        readonly List<CaptainMenuEntry> _entries = new List<CaptainMenuEntry>();
        int _focused;

        /// <summary>The generated carousel (null when the art team's menu is shown).</summary>
        public CaptainSelectView View => _view as CaptainSelectView;
        public ArtMenuView ArtView => _view as ArtMenuView;
        public CaptainLocks Locks => _locks;

        void Awake()
        {
            EnsureMenuCamera();
            EnsureEventSystem();
            if (menuCanvas == null) BuildCanvas();
            // Demo: the art team's menu when its settings are present, else the generated carousel.
            _view = ArtMenuView.TryCreate(menuCanvas.transform,
                () => RoguePirates.V4.Foundation.Core.AppRoot.Instance != null && RoguePirates.V4.Foundation.Core.AppRoot.Instance.Save != null
                    ? RoguePirates.V4.Foundation.Core.AppRoot.Instance.Save.Meta.marks : 0,
                "RUM");
            if (_view == null) _view = CaptainSelectView.Ensure(menuCanvas.transform);
            // V5 M7: the settings screen, the safe area and the UI scale.
            _settings = SettingsPanel.Create(menuCanvas.transform);
            if (_view is ArtMenuView art) art.HubRequested += OnHub;
            _online = RoguePirates.V4.Presentation.Online.OnlineMenuPresenter.Create(menuCanvas.transform); // Playtest (3 Oct, owner)
            RoguePirates.V4.Foundation.UI.SafeAreaFitter.Wrap(menuCanvas);
            RoguePirates.V4.Foundation.UI.UiScale.ApplyAll();
        }

        SettingsPanel _settings;

        MenuHubPresenter _hub;

        /// <summary>V5 M8: the hub over the menu (also used by tests and the menu check).</summary>
        public MenuHubPresenter Hub => _hub;

        void OnHub(string screen)
        {
            if (screen == "Settings") { _settings.Open(); return; }
            if (_hub == null) return;
            if (screen == "Play") _hub.ShowModes();
            else if (screen == "Ledger") _hub.ShowShop();
            else if (screen == "Profile") _hub.ShowProfile();
            else if (screen == "Leaderboard") _online?.OpenLeaderboard(); // Playtest (3 Oct, owner)
        }

        // Playtest (3 Oct, owner): guest profiles, the weekly leaderboard and the PvP placeholder (Presentation/Online).
        RoguePirates.V4.Presentation.Online.OnlineMenuPresenter _online;
        public RoguePirates.V4.Presentation.Online.OnlineMenuPresenter Online => _online;

        void Start()
        {
            // M14.13 review fix: the menu's services come from the runtime (the session's preset applied, V3 state through the ports).
            _services = MenuServices.Create(configSet);
            if (_services == null) { Debug.LogError("V4MainMenuController: no config set"); return; }
            _config = _services.Config;
            _locks = _services.Locks;
            _unlocks = _services.Unlocks;
            _captains.Clear();
            _captains.AddRange(_locks.MenuOrder());
            BuildEntries();
            (_view as ArtMenuView)?.SetCurrency(Currency);
            // V5 M8: the shop, collection, profile and mode select, over the local save.
            var meta = _services.Meta;
            meta.BalanceChanged += _ =>
            {
                // A buy in the shop (a Captain too): the select shows it unlocked, as a buy there does.
                (_view as ArtMenuView)?.SetCurrency(Currency);
                BuildEntries();
                _view.Refresh(_entries);
                OnFocus(_focused);
            };
            _hub = MenuHubPresenter.Create(_settings.transform.parent, meta, () => (_view as ArtMenuView)?.OpenCaptainSelect());
            _online?.SetConfig(() => _config); // Playtest (3 Oct, owner): the 1v1 draft lists the content

            var selected = 0;
            for (var i = 0; i < _captains.Count; i++)
                if (_captains[i].legacyId == RoguePirates.V4.Progression.CaptainChoice.LegacyId) selected = i;
            _view.CaptainChanged += OnFocus;
            _view.PlayRequested += OnPlay;
            _view.Show(_entries, selected, Hint());
            OnFocus(_view.SelectedIndex);
        }

        void OnDestroy()
        {
            if (_view != null)
            {
                _view.CaptainChanged -= OnFocus;
                _view.PlayRequested -= OnPlay;
            }
            _services?.Dispose();
        }

        void BuildEntries()
        {
            _entries.Clear();
            foreach (var c in _captains)
            {
                var state = _locks.StateOf(c);
                _entries.Add(new CaptainMenuEntry
                {
                    Key = c.captainKey,
                    ArtId = (CaptainId)c.legacyId,
                    Label = c.displayName,
                    Theme = c.themeColor,
                    Locked = state.Locked,
                    LockReason = state.Reason,
                    ActiveName = c.active != null ? c.active.displayName : null,
                    ActiveSummary = c.activeSummary,
                    PassiveSummary = c.passiveSummary,
                });
            }
        }

        string Hint()
        {
            var rule = _config?.Single<UnlockConfig>()?.startingWeaponRule ?? StartingWeaponRule.CaptainWeapon;
            return rule == StartingWeaponRule.CaptainWeapon
                ? "Captain for this voyage: each brings a starting weapon; more weapons from level-ups"
                : "Captain for this voyage: all start with Swivel Guns; more weapons from level-ups";
        }

        void OnFocus(int index)
        {
            if (index < 0 || index >= _captains.Count) return;
            _focused = index;
            var state = _locks.StateOf(_captains[index]);
            if (!state.Locked) RoguePirates.V4.Progression.CaptainChoice.LegacyId = _captains[index].legacyId;
            var (label, enabled) = PlayButton(state, Currency);
            _view.SetPlayButton(label, enabled);
        }

        /// <summary>Set Sail for an open Captain, a Rum unlock when that is all that is missing, else locked.</summary>
        public static (string label, bool enabled) PlayButton(CaptainLockState state, string currency) =>
            !state.Locked ? ("SET SAIL", true)
            : state.CanBuy ? ($"UNLOCK: {state.RumCost} {currency}", true)
            : ("LOCKED", false);

        string Currency => (_config?.Single<RewardConfig>()?.currencyName ?? "Rum").ToUpperInvariant();

        void OnPlay()
        {
            if (_focused < 0 || _focused >= _captains.Count) return;
            var captain = _captains[_focused];
            var state = _locks.StateOf(captain);
            if (state.Locked)
            {
                if (!state.CanBuy) return;
                _unlocks.Purchase(captain.captainKey);
                BuildEntries();
                _view.Refresh(_entries);
                OnFocus(_focused);
                return;
            }
            RoguePirates.V4.Progression.CaptainChoice.LegacyId = captain.legacyId;
            // Playtest (3 Oct, owner, round 7: a solo voyage came up as a 1v1): a voyage leaves any 1v1 lobby first.
            if (RoguePirates.V4.Online.PvpSession.Exists && RoguePirates.V4.Online.PvpSession.Instance.Lobby != null)
                _ = RoguePirates.V4.Online.PvpSession.Instance.Leave();
            RunRequest.ClearPvp();
            MenuServices.BeginSaltwindExperiment();
            SceneManager.LoadScene(V4SceneNames.SaltwindRun);
        }

        static void EnsureMenuCamera()
        {
            if (Camera.main != null) return;
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.1f, 0.16f, 1f);
            cameraGo.AddComponent<AudioListener>();
        }

        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
        }

        void BuildCanvas()
        {
            var canvasGo = new GameObject("v4_ui_main_menu_canvas");
            menuCanvas = canvasGo.AddComponent<Canvas>();
            menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
        }
    }
}
