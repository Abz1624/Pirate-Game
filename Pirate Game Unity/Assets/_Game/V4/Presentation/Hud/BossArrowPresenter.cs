using System;
using RoguePirates.V4.Foundation.UI;
using RoguePirates.V4.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// Playtest (3 Oct, owner): the Barnacle King was hard to find once summoned. While the boss is alive and off-screen,
    /// a red arrow with "BOSS" sits at the screen edge on the line from the screen centre to the boss, pointing at it.
    /// Hidden while the boss is on screen, dead or not summoned, and after the run ends. The boss's position is its view
    /// (the enemy view presenter knows which view is the boss); the stage view says when it is summoned or defeated.
    /// </summary>
    public sealed class BossArrowPresenter : MonoBehaviour
    {
        const float EdgeMargin = 70f;   // canvas units from the screen edge to the arrow's centre
        const float OnScreenInset = 0.04f; // the boss counts as on screen when inside this much of the viewport's edge

        IRunViews _views;
        Func<Transform> _boss;
        RectTransform _root, _arrow;
        CanvasGroup _group;
        float _pulse;

        public bool ArrowVisible => _group != null && _group.alpha > 0f;

        public static BossArrowPresenter Create(Transform canvas)
        {
            var existing = canvas.GetComponentInChildren<BossArrowPresenter>(true);
            if (existing != null) return existing;
            var go = new GameObject("V4BossArrow", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var p = go.AddComponent<BossArrowPresenter>();
            p.Build((RectTransform)go.transform);
            return p;
        }

        public void Bind(IRunViews views, Func<Transform> boss)
        {
            _views = views;
            _boss = boss;
        }

        void Build(RectTransform root)
        {
            _root = root;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            _group = root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            _arrow = (RectTransform)new GameObject("Arrow", typeof(RectTransform)).transform;
            _arrow.SetParent(root, false);
            _arrow.anchorMin = _arrow.anchorMax = new Vector2(0.5f, 0.5f);
            _arrow.pivot = new Vector2(0.5f, 0.5f);
            _arrow.sizeDelta = new Vector2(56f, 56f);
            var image = _arrow.gameObject.AddComponent<Image>();
            image.sprite = MinimapUiSprites.Triangle; // points down; rotated towards the boss
            image.color = new Color(0.95f, 0.18f, 0.12f, 0.95f);
            image.raycastTarget = false;
            var outline = _arrow.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(root, false);
            _label = (RectTransform)labelGo.transform;
            _label.anchorMin = _label.anchorMax = new Vector2(0.5f, 0.5f);
            _label.sizeDelta = new Vector2(90f, 30f);
            var text = labelGo.AddComponent<TextMeshProUGUI>();
            text.text = "BOSS";
            text.fontSize = 22f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.outlineWidth = 0.25f;
            text.outlineColor = new Color32(0, 0, 0, 255);
            text.raycastTarget = false;
        }

        RectTransform _label;

        void LateUpdate()
        {
            var boss = _boss != null ? _boss() : null;
            var stage = _views?.Stage;
            var run = _views?.Run;
            var cam = Camera.main;
            var show = boss != null && boss.gameObject.activeInHierarchy && cam != null
                       && (stage == null || (stage.BossSummoned && !stage.BossDefeated))
                       && (run == null || !run.IsRunEnded);
            if (show) show = Place(cam, boss.position);
            var alpha = show ? 1f : 0f;
            if (!Mathf.Approximately(_group.alpha, alpha)) _group.alpha = alpha;
            if (!show) return;
            _pulse += Time.unscaledDeltaTime * 6f;
            var s = 1f + Mathf.Sin(_pulse) * 0.08f;
            _arrow.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>Puts the arrow on the screen edge towards the boss; false when the boss is on screen.</summary>
        bool Place(Camera cam, Vector3 world)
        {
            var vp = cam.WorldToViewportPoint(world);
            var behind = vp.z < 0f;
            if (!behind && vp.x > OnScreenInset && vp.x < 1f - OnScreenInset && vp.y > OnScreenInset && vp.y < 1f - OnScreenInset) return false;
            var dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
            if (behind) dir = -dir; // a point behind the camera projects mirrored
            var rect = _root.rect;
            dir.x *= rect.width;
            dir.y *= rect.height;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
            dir.Normalize();
            // The point where the ray from the centre meets the edge box (the screen less the margin).
            var halfW = Mathf.Max(1f, rect.width * 0.5f - EdgeMargin);
            var halfH = Mathf.Max(1f, rect.height * 0.5f - EdgeMargin);
            var t = Mathf.Min(Mathf.Abs(dir.x) > 0.0001f ? halfW / Mathf.Abs(dir.x) : float.MaxValue,
                              Mathf.Abs(dir.y) > 0.0001f ? halfH / Mathf.Abs(dir.y) : float.MaxValue);
            var pos = dir * t;
            _arrow.anchoredPosition = pos;
            _arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);
            // Just inside the arrow, towards the centre, clear of it whichever side it is on (the label is wide, not tall).
            _label.anchoredPosition = pos - dir * (36f + Mathf.Abs(dir.x) * 45f + Mathf.Abs(dir.y) * 15f);
            return true;
        }
    }
}
