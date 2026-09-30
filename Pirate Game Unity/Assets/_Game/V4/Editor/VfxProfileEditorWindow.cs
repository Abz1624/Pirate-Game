using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Presentation;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// The artists' VFX editor (demo, 30 Sep 2026): one profile per weapon, the player ship and each enemy, with the
    /// moments it can dress (muzzle, projectile, trail, hit, death, spawn): prefab, replace-or-add, scale, tint, offset and
    /// lifetime. Edits save to Resources/V4VfxProfiles and apply at once in Play mode; Preview plays the moment at the ship.
    /// </summary>
    public sealed class VfxProfileEditorWindow : EditorWindow
    {
        const string AssetPath = "Assets/_Game/V4/Resources/V4VfxProfiles.asset";

        VfxProfileLibrary _library;
        SerializedObject _so;
        int _selected = -1;
        string _search = "";
        Vector2 _listScroll, _detailScroll;
        readonly Dictionary<VfxSlotKind, bool> _open = new Dictionary<VfxSlotKind, bool>();

        [MenuItem("Tools/Rogue Pirates/VFX Editor")]
        public static void Open() => GetWindow<VfxProfileEditorWindow>("VFX Editor").minSize = new Vector2(640, 420);

        /// <summary>The library asset (created, with every weapon, the ship and every enemy, when missing).</summary>
        public static VfxProfileLibrary LoadOrCreate()
        {
            var library = AssetDatabase.LoadAssetAtPath<VfxProfileLibrary>(AssetPath);
            if (library != null) return library;
            library = CreateInstance<VfxProfileLibrary>();
            AssetDatabase.CreateAsset(library, AssetPath);
            Sync(library);
            AssetDatabase.SaveAssets();
            return library;
        }

        /// <summary>Adds an entry for every weapon, the player ship and every enemy that has none (existing entries are kept).</summary>
        public static int Sync(VfxProfileLibrary library)
        {
            var list = (library.profiles ?? new VfxProfile[0]).Where(p => p != null).ToList();
            var added = 0;
            void Add(VfxSubject subject, string id, string name)
            {
                if (string.IsNullOrEmpty(id) || list.Any(p => p.subject == subject && p.id == id)) return;
                list.Add(new VfxProfile { subject = subject, id = id, displayName = string.IsNullOrEmpty(name) ? id : name });
                added++;
            }
            Add(VfxSubject.PlayerShip, VfxProfileLibrary.PlayerShipId, "Player ship");
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponConfig"))
            {
                var w = AssetDatabase.LoadAssetAtPath<WeaponConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (w != null) Add(VfxSubject.Weapon, w.weaponId, w.displayName);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDefinition"))
            {
                var e = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (e != null) Add(VfxSubject.Enemy, e.id, e.displayName);
            }
            library.profiles = list.OrderBy(p => p.subject).ThenBy(p => p.displayName).ToArray();
            EditorUtility.SetDirty(library);
            return added;
        }

        void OnEnable()
        {
            _library = LoadOrCreate();
            _so = new SerializedObject(_library);
        }

        void OnGUI()
        {
            if (_library == null || _so == null) OnEnable();
            _so.Update();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(120));
                if (GUILayout.Button("Add missing entries", EditorStyles.toolbarButton, GUILayout.Width(130)))
                {
                    var n = Sync(_library);
                    _so.Update();
                    ShowNotification(new GUIContent(n + " added"));
                }
                if (GUILayout.Button("Select asset", EditorStyles.toolbarButton, GUILayout.Width(90))) Selection.activeObject = _library;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawList();
                DrawDetail();
            }
            if (_so.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(_library);
        }

        void DrawList()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_listScroll, GUILayout.Width(220)))
            {
                _listScroll = scroll.scrollPosition;
                VfxSubject? group = null;
                for (var i = 0; i < _library.profiles.Length; i++)
                {
                    var p = _library.profiles[i];
                    if (p == null) continue;
                    var label = string.IsNullOrEmpty(p.displayName) ? p.id : p.displayName;
                    if (_search.Length > 0 && label.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                        (p.id ?? "").IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (group != p.subject)
                    {
                        group = p.subject;
                        EditorGUILayout.LabelField(p.subject == VfxSubject.Weapon ? "Weapons" : p.subject == VfxSubject.Enemy ? "Enemies" : "Player ship", EditorStyles.boldLabel);
                    }
                    var customised = VfxProfile.SlotsFor(p.subject).Any(k => p.Slot(k).prefab != null || p.Slot(k).useTint || !Mathf.Approximately(p.Slot(k).scale, 1f));
                    var style = i == _selected ? EditorStyles.helpBox : EditorStyles.label;
                    if (GUILayout.Button((customised ? "● " : "   ") + label, style)) { _selected = i; GUI.FocusControl(null); }
                }
            }
        }

        void DrawDetail()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_detailScroll))
            {
                _detailScroll = scroll.scrollPosition;
                if (_selected < 0 || _selected >= _library.profiles.Length || _library.profiles[_selected] == null)
                {
                    EditorGUILayout.HelpBox("Pick a weapon, the player ship or an enemy on the left.\n\n" +
                        "Each moment can play your prefab instead of the game's effect (Replace default) or on top of it. " +
                        "With no prefab, Scale resizes the game's own effect. Changes save at once and show live in Play mode; " +
                        "use Preview (Play mode) to fire a moment in front of the ship.", MessageType.Info);
                    return;
                }
                var profile = _library.profiles[_selected];
                var prop = _so.FindProperty("profiles").GetArrayElementAtIndex(_selected);
                EditorGUILayout.LabelField(string.IsNullOrEmpty(profile.displayName) ? profile.id : profile.displayName, EditorStyles.largeLabel);
                EditorGUILayout.LabelField(profile.subject + "  ·  id: " + profile.id, EditorStyles.miniLabel);
                EditorGUILayout.Space();
                foreach (var kind in VfxProfile.SlotsFor(profile.subject)) DrawSlot(profile, prop, kind);
            }
        }

        void DrawSlot(VfxProfile profile, SerializedProperty profileProp, VfxSlotKind kind)
        {
            var slotProp = profileProp.FindPropertyRelative(kind.ToString().ToLowerInvariant());
            if (!_open.TryGetValue(kind, out var open)) open = true;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _open[kind] = open = EditorGUILayout.Foldout(open, kind.ToString(), true, EditorStyles.foldoutHeader);
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(!Application.isPlaying))
                        if (GUILayout.Button(new GUIContent("Preview", Application.isPlaying ? "Play this moment at the ship" : "Enter Play mode to preview"), GUILayout.Width(70)))
                            Preview(profile, kind);
                }
                if (!open) return;
                EditorGUILayout.LabelField(VfxProfile.SlotHelp(profile.subject, kind), EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("enabled"));
                using (new EditorGUI.DisabledScope(!slotProp.FindPropertyRelative("enabled").boolValue))
                {
                    EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("prefab"));
                    if (kind != VfxSlotKind.Projectile && kind != VfxSlotKind.Trail)
                        EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("replaceDefault"));
                    EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("scale"));
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var useTint = slotProp.FindPropertyRelative("useTint");
                        useTint.boolValue = EditorGUILayout.Toggle("Tint", useTint.boolValue);
                        using (new EditorGUI.DisabledScope(!useTint.boolValue))
                            EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("tint"), GUIContent.none);
                    }
                    if (kind != VfxSlotKind.Projectile) EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("offset"));
                    if (kind != VfxSlotKind.Projectile && kind != VfxSlotKind.Trail)
                        EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("lifetime"));
                }
            }
        }

        void Preview(VfxProfile profile, VfxSlotKind kind)
        {
            _so.ApplyModifiedProperties();
            var root = FindFirstObjectByType<V4PresentationRoot>();
            if (root == null || root.VfxProfiles == null)
            {
                ShowNotification(new GUIContent("Start a run first"));
                return;
            }
            var slot = profile.Slot(kind);
            if (slot.prefab == null && kind != VfxSlotKind.Trail)
            {
                ShowNotification(new GUIContent("No prefab: the game's default effect plays in the run"));
                return;
            }
            root.VfxProfiles.Preview(profile.subject, profile.id, kind);
        }
    }
}
