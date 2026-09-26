using System;
using System.Collections;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>
    /// Deterministic JSON of a config object: fields in declaration order, floats as round-trip "R" in invariant
    /// culture, enums by name, references to other configs by configId, other Unity objects by name. The SHA-256 of
    /// this text is the content hash; equal values give equal hashes on every machine.
    /// </summary>
    public static class CanonicalConfigWriter
    {
        public static string ToJson(object config)
        {
            var sb = new StringBuilder(1024);
            WriteObject(sb, config, root: true);
            return sb.ToString();
        }

        public static string Hash(object config) => Sha256(ToJson(config));

        public static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new StringBuilder(64);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        static void WriteObject(StringBuilder sb, object obj, bool root = false)
        {
            if (obj == null) { sb.Append("null"); return; }
            sb.Append('{');
            var first = true;
            foreach (var f in ConfigFieldWalker.SerializedFields(obj.GetType()))
            {
                if (!first) sb.Append(',');
                first = false;
                WriteString(sb, f.Name);
                sb.Append(':');
                WriteValue(sb, f.GetValue(obj), f.FieldType);
            }
            sb.Append('}');
        }

        static void WriteValue(StringBuilder sb, object value, Type type)
        {
            var kind = ConfigFieldWalker.KindOf(type);
            switch (kind)
            {
                case ConfigFieldKind.Float:
                    sb.Append(type == typeof(float)
                        ? ((float)value).ToString("R", CultureInfo.InvariantCulture)
                        : ((double)value).ToString("R", CultureInfo.InvariantCulture));
                    return;
                case ConfigFieldKind.Int: sb.Append(Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)); return;
                case ConfigFieldKind.Bool: sb.Append((bool)value ? "true" : "false"); return;
                case ConfigFieldKind.String: if (value == null) sb.Append("null"); else WriteString(sb, (string)value); return;
                case ConfigFieldKind.Enum: WriteString(sb, value.ToString()); return;
                case ConfigFieldKind.Vector2:
                case ConfigFieldKind.Vector3:
                case ConfigFieldKind.Color: WriteString(sb, ConfigFieldWalker.Format(value)); return;
                case ConfigFieldKind.Curve: WriteCurve(sb, value as AnimationCurve); return;
                case ConfigFieldKind.AssetReference:
                    var asset = value as V4ConfigAsset;
                    if (asset == null) sb.Append("null"); else WriteString(sb, "ref:" + asset.configId);
                    return;
                case ConfigFieldKind.UnityReference:
                    var unityObject = value as UnityEngine.Object;
                    if (unityObject == null) sb.Append("null"); else WriteString(sb, "obj:" + unityObject.name);
                    return;
                case ConfigFieldKind.Nested: WriteObject(sb, value); return;
                case ConfigFieldKind.List:
                    if (!(value is IList list)) { sb.Append("null"); return; }
                    var element = ConfigFieldWalker.ElementType(type);
                    sb.Append('[');
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        WriteValue(sb, list[i], element);
                    }
                    sb.Append(']');
                    return;
                default: sb.Append("null"); return;
            }
        }

        static void WriteCurve(StringBuilder sb, AnimationCurve curve)
        {
            if (curve == null) { sb.Append("null"); return; }
            sb.Append('[').Append((int)curve.preWrapMode).Append(',').Append((int)curve.postWrapMode);
            foreach (var k in curve.keys)
            {
                sb.Append(",[").Append(F(k.time)).Append(',').Append(F(k.value)).Append(',')
                  .Append(F(k.inTangent)).Append(',').Append(F(k.outTangent)).Append(',')
                  .Append((int)k.weightedMode).Append(',').Append(F(k.inWeight)).Append(',').Append(F(k.outWeight)).Append(']');
            }
            sb.Append(']');
        }

        static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
