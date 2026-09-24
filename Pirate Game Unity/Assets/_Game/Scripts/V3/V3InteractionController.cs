using HighSeas.Core;

using HighSeas.POI;

using TMPro;

using UnityEngine;

using UnityEngine.EventSystems;

using UnityEngine.InputSystem;

using UnityEngine.UI;



namespace HighSeas.V3

{

    public enum InteractionResult

    {

        None,

        Started,

        InProgress,

        Succeeded,

        Failed,

        Cancelled

    }



    /// <summary>Unified hold/prompt interaction for desktop E and touch button.</summary>

    public sealed class V3InteractionController : MonoBehaviour

    {

        public const float DefaultRange = 4f;

        public const float ChestHoldSeconds = 0.6f;

        public const float BossHoldSeconds = 0.7f;



        static V3InteractionController _instance;

        public static V3InteractionController Instance => _instance;



        POIInteractable _target;

        float _holdTimer;

        bool _holding;

        bool _touchHolding;

        TextMeshProUGUI _promptText;

        Button _touchButton;

        CanvasGroup _promptGroup;

        RectTransform _promptRect;

        RectTransform _promptTextRect;

        enum PromptLayoutKind
        {
            BottomBar,
            CenterCompact,
            SalvageChestPopup
        }

        PromptLayoutKind _promptLayout = PromptLayoutKind.BottomBar;

        int _salvageChestDismissedIndex = -1;

        int _salvageChestInsufficientFundsNotifiedIndex = -1;

        bool _salvageChestPauseActive;

        RunState _salvageChestResumeState = RunState.Playing;



        public static V3InteractionController Ensure(Transform canvasRoot)

        {

            if (_instance != null) return _instance;



            var go = new GameObject("V3InteractionController");

            go.transform.SetParent(canvasRoot, false);

            _instance = go.AddComponent<V3InteractionController>();

            _instance.BuildUi(canvasRoot);

            return _instance;

        }



        void Awake()

        {

            if (_instance != null && _instance != this)

            {

                Destroy(gameObject);

                return;

            }



            _instance = this;

        }



        void BuildUi(Transform canvasRoot)

        {

            var panel = new GameObject("PLACEHOLDER_v3_ui_interaction_prompt");

            panel.transform.SetParent(canvasRoot, false);

            var rect = panel.AddComponent<RectTransform>();

            _promptRect = rect;

            ApplyBottomPromptAnchor();

            rect.sizeDelta = new Vector2(520f, 72f);



            var bg = panel.AddComponent<Image>();

            bg.color = new Color(0.06f, 0.1f, 0.16f, 0.92f);

            _promptGroup = panel.AddComponent<CanvasGroup>();



            var textGo = new GameObject("Prompt");

            textGo.transform.SetParent(panel.transform, false);

            var textRect = textGo.AddComponent<RectTransform>();

            textRect.anchorMin = Vector2.zero;

            textRect.anchorMax = Vector2.one;

            textRect.offsetMin = new Vector2(16f, 8f);

            textRect.offsetMax = new Vector2(-120f, -8f);

            _promptTextRect = textRect;

            _promptText = textGo.AddComponent<TextMeshProUGUI>();

            _promptText.fontSize = 22;

            _promptText.alignment = TextAlignmentOptions.MidlineLeft;

            _promptText.color = V3PlaceholderFactory.UiText;



            var buttonGo = new GameObject("TouchInteract");

            buttonGo.transform.SetParent(panel.transform, false);

            var buttonRect = buttonGo.AddComponent<RectTransform>();

            buttonRect.anchorMin = new Vector2(1f, 0.5f);

            buttonRect.anchorMax = new Vector2(1f, 0.5f);

            buttonRect.anchoredPosition = new Vector2(-60f, 0f);

            buttonRect.sizeDelta = new Vector2(96f, 48f);

            _touchButton = buttonGo.AddComponent<Button>();

            var buttonImage = buttonGo.AddComponent<Image>();

            buttonImage.color = V3PlaceholderFactory.UiAccent;

            _touchButton.targetGraphic = buttonImage;



            var labelGo = new GameObject("Label");

            labelGo.transform.SetParent(buttonGo.transform, false);

            var labelRect = labelGo.AddComponent<RectTransform>();

            labelRect.anchorMin = Vector2.zero;

            labelRect.anchorMax = Vector2.one;

            labelRect.offsetMin = Vector2.zero;

            labelRect.offsetMax = Vector2.zero;

            var label = labelGo.AddComponent<TextMeshProUGUI>();

            label.text = "USE";

            label.alignment = TextAlignmentOptions.Center;

            label.fontSize = 20;



            var trigger = buttonGo.AddComponent<EventTrigger>();

            AddPointerEvent(trigger, EventTriggerType.PointerDown, _ => _touchHolding = true);

            AddPointerEvent(trigger, EventTriggerType.PointerUp, _ => _touchHolding = false);

            AddPointerEvent(trigger, EventTriggerType.PointerExit, _ => _touchHolding = false);

            _touchButton.onClick.AddListener(OnTouchInteractPressed);

            SetPromptVisible(false);

        }



        static void AddPointerEvent(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)

        {

            var entry = new EventTrigger.Entry { eventID = type };

            entry.callback.AddListener(callback);

            trigger.triggers.Add(entry);

        }



        public void RegisterTarget(POIInteractable interactable) => _target = interactable;



        public void ClearTarget(POIInteractable interactable)

        {

            if (_target == interactable)

            {

                _target = null;

                CancelHold();

                EndSalvageChestChoicePause();

            }

        }



        void Update()

        {

            var run = RunManager.Instance;

            if (run != null && run.IsRunEnded)

            {

                EndSalvageChestChoicePause();

                CancelHold();

                return;

            }

            if (run != null && run.IsPaused && run.PauseReason != PauseReason.POIChoice)

            {

                _salvageChestPauseActive = false;

                CancelHold();

                return;

            }



            RefreshNearestTarget();

            if (_target == null)

            {

                EndSalvageChestChoicePause();

                SetPromptVisible(false);

                return;

            }

            UpdatePromptLayoutForTarget();



            var holdDuration = _target.RequiredHoldSeconds;

            var label = _target.PromptLabel;

            var interactHeld = IsInteractHeld();
            var keyboard = Keyboard.current;



            if (holdDuration <= 0f)

            {

                if (_target.UsesChargeInteraction)

                {

                    SetPromptVisible(_target.IsPlayerInRange);

                    var charge = FindFirstObjectByType<POIManager>()?.GetSignalBuoyCharge01(_target.Index) ?? 0f;

                    _promptText.text = $"{label} — charging {charge:0%}";

                    return;

                }



                if (_target.UsesProximityAmbush)

                {

                    SetPromptVisible(_target.IsPlayerInRange);

                    _promptText.text = _target.PromptLabel;

                    return;

                }



                if (_target.ShowsSalvageChestPrice)
                {
                    if (!_target.CanInteract)
                    {
                        SetPromptVisible(false);
                        EndSalvageChestChoicePause();
                        return;
                    }

                    if (!_target.IsPlayerInRange)
                    {
                        if (_salvageChestDismissedIndex == _target.Index)
                            _salvageChestDismissedIndex = -1;
                        _salvageChestInsufficientFundsNotifiedIndex = -1;
                        SetPromptVisible(false);
                        EndSalvageChestChoicePause();
                        return;
                    }

                    if (_salvageChestDismissedIndex == _target.Index)
                    {
                        SetPromptVisible(false);
                        EndSalvageChestChoicePause();
                        return;
                    }

                    if (!CanAffordSalvageChest())
                    {
                        SetPromptVisible(false);
                        EndSalvageChestChoicePause();
                        if (_salvageChestInsufficientFundsNotifiedIndex != _target.Index)
                        {
                            _salvageChestInsufficientFundsNotifiedIndex = _target.Index;
                            var price = ResolveSalvageChestPrice();
                            V3ToastHud.Instance?.Show($"Not enough funds — need {price} gold.", 2.5f);
                        }

                        return;
                    }

                    _salvageChestInsufficientFundsNotifiedIndex = -1;
                    var chestPrice = ResolveSalvageChestPrice();
                    SetPromptVisible(true);
                    BeginSalvageChestChoicePause();
                    _promptText.text =
                        $"<b>Salvage Chest</b>\n<size=120%>{chestPrice} gold</size>\nTake (E)  ·  Skip (R)";
                    if (keyboard != null)
                    {
                        if (keyboard.eKey.wasPressedThisFrame)
                        {
                            _target.TryCompleteInteraction();
                            EndSalvageChestChoicePause();
                        }
                        else if (keyboard.rKey.wasPressedThisFrame)
                        {
                            _salvageChestDismissedIndex = _target.Index;
                            EndSalvageChestChoicePause();
                        }
                    }

                    return;
                }



                SetPromptVisible(false);

                if (_target.IsPlayerInRange)

                    _target.TryCompleteInteraction();

                return;

            }



            if (interactHeld)

            {

                _holding = true;

                _holdTimer += Time.deltaTime;

                SetPromptVisible(true);

                _promptText.text = $"{label} — hold {Mathf.Clamp01(_holdTimer / holdDuration):0%}";

                if (_holdTimer >= holdDuration)

                {

                    _target.TryCompleteInteraction();

                    CancelHold();

                }

            }

            else if (_holding)

            {

                CancelHold();

            }

            else

            {

                SetPromptVisible(true);

                _promptText.text = $"{label} — hold E or USE ({holdDuration:0.#}s)";

            }

        }



        void RefreshNearestTarget()

        {

            if (_target != null && _target.IsPlayerInRange)

                return;



            _target = null;

            var all = FindObjectsByType<POIInteractable>(FindObjectsSortMode.None);

            var bestDist = float.MaxValue;

            POIInteractable best = null;

            var playerPos = FindFirstObjectByType<Player.PlayerShipController>()?.Position ?? Vector3.zero;



            for (var i = 0; i < all.Length; i++)

            {

                var candidate = all[i];

                if (!candidate.CanInteract) continue;

                var dist = Vector3.Distance(playerPos, candidate.WorldPosition);

                if (dist > candidate.InteractRange || dist >= bestDist) continue;

                bestDist = dist;

                best = candidate;

            }



            _target = best;

        }



        bool IsInteractHeld()

        {

            var keyboard = Keyboard.current;

            if (keyboard != null && keyboard.eKey.isPressed)

                return true;

            return _touchHolding;

        }



        void OnTouchInteractPressed()

        {

            if (_target == null) return;

            if (_target.RequiredHoldSeconds <= 0f)
            {
                if (_target.ShowsSalvageChestPrice)
                {
                    if (_target.IsPlayerInRange && CanAffordSalvageChest())
                    {
                        _target.TryCompleteInteraction();
                        EndSalvageChestChoicePause();
                    }
                    return;
                }

                _target.TryCompleteInteraction();
            }

        }



        void CancelHold()

        {

            _holding = false;

            _holdTimer = 0f;

        }



        void UpdatePromptLayoutForTarget()
        {
            if (_target == null || _promptRect == null) return;
            var desired = _target.ShowsSalvageChestPrice && CanAffordSalvageChest()
                ? PromptLayoutKind.SalvageChestPopup
                : _target.RequiredHoldSeconds > 0f
                    ? PromptLayoutKind.CenterCompact
                    : PromptLayoutKind.BottomBar;
            if (desired == _promptLayout) return;
            _promptLayout = desired;
            switch (desired)
            {
                case PromptLayoutKind.SalvageChestPopup:
                    ApplySalvageChestPopupAnchor();
                    break;
                case PromptLayoutKind.CenterCompact:
                    ApplyCenterPromptAnchor();
                    break;
                default:
                    ApplyBottomPromptAnchor();
                    break;
            }
        }

        void ApplyBottomPromptAnchor()
        {
            if (_promptRect == null) return;
            _promptRect.anchorMin = new Vector2(0.5f, 0f);
            _promptRect.anchorMax = new Vector2(0.5f, 0f);
            _promptRect.pivot = new Vector2(0.5f, 0f);
            _promptRect.anchoredPosition = new Vector2(0f, 150f);
            _promptRect.sizeDelta = new Vector2(520f, 72f);
            ApplyCompactPromptTextLayout();
            if (_touchButton != null)
            {
                _touchButton.gameObject.SetActive(true);
                var tr = _touchButton.GetComponent<RectTransform>();
                tr.anchorMin = new Vector2(1f, 0.5f);
                tr.anchorMax = new Vector2(1f, 0.5f);
                tr.pivot = new Vector2(0.5f, 0.5f);
                tr.anchoredPosition = new Vector2(-60f, 0f);
                tr.sizeDelta = new Vector2(96f, 48f);
            }
        }

        void ApplyCenterPromptAnchor()
        {
            if (_promptRect == null) return;
            _promptRect.anchorMin = new Vector2(0.5f, 0.5f);
            _promptRect.anchorMax = new Vector2(0.5f, 0.5f);
            _promptRect.pivot = new Vector2(0.5f, 0.5f);
            _promptRect.anchoredPosition = Vector2.zero;
            _promptRect.sizeDelta = new Vector2(560f, 88f);
            ApplyCompactPromptTextLayout();
            if (_touchButton != null)
            {
                _touchButton.gameObject.SetActive(true);
                var tr = _touchButton.GetComponent<RectTransform>();
                tr.anchorMin = new Vector2(1f, 0.5f);
                tr.anchorMax = new Vector2(1f, 0.5f);
                tr.pivot = new Vector2(0.5f, 0.5f);
                tr.anchoredPosition = new Vector2(-60f, 0f);
                tr.sizeDelta = new Vector2(96f, 48f);
            }
        }

        void ApplySalvageChestPopupAnchor()
        {
            if (_promptRect == null) return;
            const float inset = 0.35f;
            _promptRect.anchorMin = new Vector2(inset, inset);
            _promptRect.anchorMax = new Vector2(1f - inset, 1f - inset);
            _promptRect.pivot = new Vector2(0.5f, 0.5f);
            _promptRect.anchoredPosition = Vector2.zero;
            _promptRect.sizeDelta = Vector2.zero;
            ApplySalvageChestPromptTextLayout();
            if (_touchButton != null)
            {
                _touchButton.gameObject.SetActive(true);
                var tr = _touchButton.GetComponent<RectTransform>();
                tr.anchorMin = new Vector2(0.5f, 0f);
                tr.anchorMax = new Vector2(0.5f, 0f);
                tr.pivot = new Vector2(0.5f, 0f);
                tr.anchoredPosition = new Vector2(0f, 28f);
                tr.sizeDelta = new Vector2(200f, 64f);
            }
        }

        void ApplyCompactPromptTextLayout()
        {
            if (_promptText == null || _promptTextRect == null) return;
            _promptTextRect.anchorMin = Vector2.zero;
            _promptTextRect.anchorMax = Vector2.one;
            _promptTextRect.offsetMin = new Vector2(16f, 8f);
            _promptTextRect.offsetMax = new Vector2(-120f, -8f);
            _promptText.fontSize = 22;
            _promptText.alignment = TextAlignmentOptions.MidlineLeft;
            _promptText.enableWordWrapping = true;
        }

        void ApplySalvageChestPromptTextLayout()
        {
            if (_promptText == null || _promptTextRect == null) return;
            _promptTextRect.anchorMin = new Vector2(0f, 0.22f);
            _promptTextRect.anchorMax = Vector2.one;
            _promptTextRect.offsetMin = new Vector2(28f, 12f);
            _promptTextRect.offsetMax = new Vector2(-28f, -20f);
            _promptText.fontSize = 28;
            _promptText.alignment = TextAlignmentOptions.Center;
            _promptText.enableWordWrapping = true;
            _promptText.richText = true;
        }

        static POIManager ResolvePoiManager() =>
            Object.FindFirstObjectByType<POIManager>();

        static int ResolveSalvageChestPrice() =>
            ResolvePoiManager()?.GetSalvageChestQuotePrice() ?? 0;

        static bool CanAffordSalvageChest() =>
            ResolvePoiManager()?.CanAffordSalvageChest() ?? false;

        void SetPromptVisible(bool visible)

        {

            if (_promptGroup == null) return;

            _promptGroup.alpha = visible ? 1f : 0f;

            _promptGroup.blocksRaycasts = visible;

            _promptGroup.interactable = visible;

        }

        void BeginSalvageChestChoicePause()
        {
            if (_salvageChestPauseActive) return;
            var run = RunManager.Instance;
            if (run == null || run.IsRunEnded) return;
            if (run.IsPaused) return;

            _salvageChestResumeState = run.State;
            _salvageChestPauseActive = true;
            run.SetPause(PauseReason.POIChoice);
            run.SetState(RunState.POIChoicePaused);
        }

        void EndSalvageChestChoicePause()
        {
            if (!_salvageChestPauseActive) return;
            _salvageChestPauseActive = false;

            var run = RunManager.Instance;
            if (run == null || run.PauseReason != PauseReason.POIChoice) return;

            run.SetPause(PauseReason.None);
            var resume = _salvageChestResumeState;
            if (resume is RunState.POIChoicePaused or RunState.LevelUpPaused)
                resume = RunState.Playing;
            run.SetState(resume);
        }

    }

}


