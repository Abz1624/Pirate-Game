using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>What kind of editor/value a serialized field is; drives hashing, presets and console rows.</summary>
    public enum ConfigFieldKind { Float, Int, Bool, String, Enum, Vector2, Vector3, Color, Curve, AssetReference, UnityReference, Nested, List }

    /// <summary>One leaf or container field reached while walking a config object.</summary>
    public readonly struct ConfigField
    {
        public readonly string Path;       // e.g. "phases[2].pools[0].share"
        public readonly string Name;
        public readonly ConfigFieldKind Kind;
        public readonly Type Type;
        public readonly FieldInfo Info;
        public readonly object Owner;      // object holding the field (list elements: the list itself)
        public readonly int Index;         // list element index, else -1
        public readonly string Tooltip;
        public readonly RangeAttribute Range;

        public ConfigField(string path, string name, ConfigFieldKind kind, Type type, FieldInfo info, object owner, int index, string tooltip, RangeAttribute range)
        {
            Path = path; Name = name; Kind = kind; Type = type; Info = info; Owner = owner; Index = index; Tooltip = tooltip; Range = range;
        }

        public object GetValue() => Index >= 0 ? ((IList)Owner)[Index] : Info.GetValue(Owner);
    }

    /// <summary>
    /// Enumerates the serialized fields of config objects in declaration order (Unity serialization rules: public or
    /// [SerializeField], not [NonSerialized], serializable types). Single source of field discovery for the canonical
    /// writer, presets, the console and coverage tests.
    /// </summary>
    public static class ConfigFieldWalker
    {
        static readonly Dictionary<Type, FieldInfo[]> Cache = new Dictionary<Type, FieldInfo[]>();

        public static FieldInfo[] SerializedFields(Type type)
        {
            if (Cache.TryGetValue(type, out var cached)) return cached;
            var chain = new List<Type>();
            for (var t = type; t != null && t != typeof(ScriptableObject) && t != typeof(object) && t != typeof(UnityEngine.Object); t = t.BaseType)
                chain.Insert(0, t); // base class fields first, like the Unity inspector
            var fields = new List<FieldInfo>();
            foreach (var t in chain)
            {
                var own = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(IsSerialized)
                    .OrderBy(f => f.MetadataToken); // declaration order
                fields.AddRange(own);
            }
            return Cache[type] = fields.ToArray();
        }

        static bool IsSerialized(FieldInfo f)
        {
            if (f.IsStatic || f.IsInitOnly || f.IsDefined(typeof(NonSerializedAttribute), false)) return false;
            if (!f.IsPublic && !f.IsDefined(typeof(SerializeField), false)) return false;
            return KindOf(f.FieldType).HasValue;
        }

        public static ConfigFieldKind? KindOf(Type t)
        {
            if (t == typeof(float) || t == typeof(double)) return ConfigFieldKind.Float;
            if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)) return ConfigFieldKind.Int;
            if (t == typeof(bool)) return ConfigFieldKind.Bool;
            if (t == typeof(string)) return ConfigFieldKind.String;
            if (t.IsEnum) return ConfigFieldKind.Enum;
            if (t == typeof(Vector2)) return ConfigFieldKind.Vector2;
            if (t == typeof(Vector3)) return ConfigFieldKind.Vector3;
            if (t == typeof(Color)) return ConfigFieldKind.Color;
            if (t == typeof(AnimationCurve)) return ConfigFieldKind.Curve;
            if (typeof(V4ConfigAsset).IsAssignableFrom(t)) return ConfigFieldKind.AssetReference;
            if (typeof(UnityEngine.Object).IsAssignableFrom(t)) return ConfigFieldKind.UnityReference;
            if (t.IsArray && t.GetArrayRank() == 1) return KindOf(t.GetElementType()).HasValue ? ConfigFieldKind.List : (ConfigFieldKind?)null;
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
                return KindOf(t.GetGenericArguments()[0]).HasValue ? ConfigFieldKind.List : (ConfigFieldKind?)null;
            if ((t.IsClass || (t.IsValueType && !t.IsPrimitive)) && t.IsDefined(typeof(SerializableAttribute), false)) return ConfigFieldKind.Nested;
            return null;
        }

        public static Type ElementType(Type listType) => listType.IsArray ? listType.GetElementType() : listType.GetGenericArguments()[0];

        /// <summary>Visits every field, depth first, in declaration order. Lists yield the list, then each element.</summary>
        public static void Walk(object root, Action<ConfigField> visit)
        {
            if (root == null) return;
            WalkObject(root, string.Empty, visit);
        }

        static void WalkObject(object obj, string prefix, Action<ConfigField> visit)
        {
            foreach (var f in SerializedFields(obj.GetType()))
            {
                var path = prefix.Length == 0 ? f.Name : prefix + "." + f.Name;
                var kind = KindOf(f.FieldType).Value;
                var tooltip = f.GetCustomAttribute<TooltipAttribute>()?.tooltip;
                var range = f.GetCustomAttribute<RangeAttribute>();
                var field = new ConfigField(path, f.Name, kind, f.FieldType, f, obj, -1, tooltip, range);
                visit(field);
                var value = f.GetValue(obj);
                if (kind == ConfigFieldKind.Nested && value != null) WalkObject(value, path, visit);
                else if (kind == ConfigFieldKind.List && value is IList list) WalkList(list, ElementType(f.FieldType), path, f, visit);
            }
        }

        static void WalkList(IList list, Type elementType, string path, FieldInfo info, Action<ConfigField> visit)
        {
            var elementKind = KindOf(elementType).Value;
            for (var i = 0; i < list.Count; i++)
            {
                var elementPath = $"{path}[{i}]";
                visit(new ConfigField(elementPath, $"[{i}]", elementKind, elementType, info, list, i, null, null));
                var element = list[i];
                if (elementKind == ConfigFieldKind.Nested && element != null) WalkObject(element, elementPath, visit);
            }
        }

        /// <summary>Every leaf field path a type can have (lists shown with [*]); used by coverage tests.</summary>
        public static IEnumerable<(string path, ConfigFieldKind kind)> Schema(Type type, string prefix = "")
        {
            foreach (var f in SerializedFields(type))
            {
                var path = prefix.Length == 0 ? f.Name : prefix + "." + f.Name;
                var kind = KindOf(f.FieldType).Value;
                yield return (path, kind);
                if (kind == ConfigFieldKind.Nested)
                    foreach (var child in Schema(f.FieldType, path)) yield return child;
                else if (kind == ConfigFieldKind.List)
                {
                    var element = ElementType(f.FieldType);
                    var elementKind = KindOf(element).Value;
                    yield return (path + "[*]", elementKind);
                    if (elementKind == ConfigFieldKind.Nested)
                        foreach (var child in Schema(element, path + "[*]")) yield return child;
                }
            }
        }

        // ---- values as invariant strings (presets, console, change log) -------------------------------------

        public static string Format(object value)
        {
            switch (value)
            {
                case null: return string.Empty;
                case float f: return f.ToString("R", CultureInfo.InvariantCulture);
                case double d: return d.ToString("R", CultureInfo.InvariantCulture);
                case bool b: return b ? "true" : "false";
                case Vector2 v: return $"{Format(v.x)},{Format(v.y)}";
                case Vector3 v: return $"{Format(v.x)},{Format(v.y)},{Format(v.z)}";
                case Color c: return $"{Format(c.r)},{Format(c.g)},{Format(c.b)},{Format(c.a)}";
                case V4ConfigAsset a: return a.configId ?? string.Empty;
                case UnityEngine.Object o: return o != null ? o.name : string.Empty;
                case IFormattable fm: return fm.ToString(null, CultureInfo.InvariantCulture);
                default: return value.ToString();
            }
        }

        public static bool TryParse(Type type, string text, out object value)
        {
            value = null;
            text = text?.Trim() ?? string.Empty;
            var inv = CultureInfo.InvariantCulture;
            try
            {
                if (type == typeof(float)) { value = float.Parse(text, NumberStyles.Float, inv); return true; }
                if (type == typeof(double)) { value = double.Parse(text, NumberStyles.Float, inv); return true; }
                if (type == typeof(int)) { value = int.Parse(text, NumberStyles.Integer, inv); return true; }
                if (type == typeof(long)) { value = long.Parse(text, NumberStyles.Integer, inv); return true; }
                if (type == typeof(short)) { value = short.Parse(text, NumberStyles.Integer, inv); return true; }
                if (type == typeof(byte)) { value = byte.Parse(text, NumberStyles.Integer, inv); return true; }
                if (type == typeof(bool)) { value = bool.Parse(text); return true; }
                if (type == typeof(string)) { value = text; return true; }
                if (type.IsEnum)
                {
                    value = Enum.Parse(type, text, true);
                    return Enum.IsDefined(type, value); // reject numbers that name no member
                }
                if (type == typeof(Vector2)) { var p = Floats(text, 2); value = new Vector2(p[0], p[1]); return true; }
                if (type == typeof(Vector3)) { var p = Floats(text, 3); value = new Vector3(p[0], p[1], p[2]); return true; }
                if (type == typeof(Color)) { var p = Floats(text, 4); value = new Color(p[0], p[1], p[2], p[3]); return true; }
            }
            catch (Exception) { }
            return false;
        }

        static float[] Floats(string text, int count)
        {
            var parts = text.Split(',');
            if (parts.Length != count) throw new FormatException();
            return parts.Select(p => float.Parse(p, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
        }
    }
}
